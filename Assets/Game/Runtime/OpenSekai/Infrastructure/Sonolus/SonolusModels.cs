using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace OpenSekai.Infrastructure.Sonolus
{
    // What the client reads from a Sonolus server. Parsing is tolerant: a missing or mistyped
    // field reads as empty, never throws, because servers add fields and leave optional ones out.
    // Only what the game shows or downloads is kept; the raw level JSON is kept for the package's
    // sonolus-source.json. Spec: https://wiki.sonolus.com/custom-server-specs/

    /// <summary>A Sonolus resource locator: an optional SHA-1 of the bytes and where to get them.</summary>
    public sealed class SonolusSrl
    {
        public SonolusSrl(string hash, string url)
        {
            Hash = hash;
            Url = url;
        }

        public string Hash { get; }
        public string Url { get; }

        public bool IsEmpty => string.IsNullOrEmpty(Url);

        internal static SonolusSrl From(JToken token)
        {
            if (!(token is JObject value))
            {
                return null;
            }

            string url = Json.String(value, "url");
            return string.IsNullOrEmpty(url) ? null : new SonolusSrl(Json.String(value, "hash"), url);
        }
    }

    /// <summary>A small labelled chip on an item: a title, an icon name, or both.</summary>
    public sealed class SonolusTag
    {
        public SonolusTag(string title, string icon)
        {
            Title = title;
            Icon = icon;
        }

        public string Title { get; }
        public string Icon { get; }

        internal static List<SonolusTag> ListFrom(JToken token)
        {
            var tags = new List<SonolusTag>();
            if (token is JArray array)
            {
                foreach (JToken item in array)
                {
                    if (item is JObject tag)
                    {
                        string title = Json.String(tag, "title");
                        string icon = Json.String(tag, "icon");
                        if (!string.IsNullOrEmpty(title) || !string.IsNullOrEmpty(icon))
                        {
                            tags.Add(new SonolusTag(title, icon));
                        }
                    }
                }
            }

            return tags;
        }
    }

    /// <summary>One level as the list, the sections and the detail page describe it.</summary>
    public sealed class SonolusLevel
    {
        public string Name { get; private set; }
        public string Source { get; private set; }
        public int Version { get; private set; }
        public double Rating { get; private set; }
        public string Title { get; private set; }
        public string Artists { get; private set; }
        public string Author { get; private set; }
        public IReadOnlyList<SonolusTag> Tags { get; private set; }
        public string EngineName { get; private set; }
        public string EngineTitle { get; private set; }
        public int EngineVersion { get; private set; }
        public SonolusSrl Cover { get; private set; }
        public SonolusSrl Bgm { get; private set; }
        public SonolusSrl Preview { get; private set; }
        public SonolusSrl Data { get; private set; }

        /// <summary>The level object exactly as the server sent it.</summary>
        public JObject Raw { get; private set; }

        /// <summary>The rating as the server would print it: whole numbers without decimals.</summary>
        public string RatingText => Rating == Math.Floor(Rating)
            ? ((long)Rating).ToString(CultureInfo.InvariantCulture)
            : Rating.ToString("0.#", CultureInfo.InvariantCulture);

        public static SonolusLevel From(JToken token)
        {
            if (!(token is JObject value))
            {
                return null;
            }

            string name = Json.String(value, "name");
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            var engine = value["engine"] as JObject;
            return new SonolusLevel
            {
                Name = name,
                Source = Json.String(value, "source"),
                Version = (int)Json.Number(value, "version"),
                Rating = Json.Number(value, "rating"),
                Title = Json.String(value, "title") ?? name,
                Artists = Json.String(value, "artists") ?? string.Empty,
                Author = Json.String(value, "author") ?? string.Empty,
                Tags = SonolusTag.ListFrom(value["tags"]),
                EngineName = engine != null ? Json.String(engine, "name") : null,
                EngineTitle = engine != null ? Json.String(engine, "title") : null,
                EngineVersion = engine != null ? (int)Json.Number(engine, "version") : 0,
                Cover = SonolusSrl.From(value["cover"]),
                Bgm = SonolusSrl.From(value["bgm"]),
                Preview = SonolusSrl.From(value["preview"]),
                Data = SonolusSrl.From(value["data"]),
                Raw = value
            };
        }

        internal static List<SonolusLevel> ListFrom(JToken token)
        {
            var levels = new List<SonolusLevel>();
            if (token is JArray array)
            {
                foreach (JToken item in array)
                {
                    SonolusLevel level = From(item);
                    if (level != null)
                    {
                        levels.Add(level);
                    }
                }
            }

            return levels;
        }
    }

    /// <summary>A row of items on a server's level home page (newest, random, staff picks...).</summary>
    public sealed class SonolusSection
    {
        public string Title { get; private set; }
        public string Icon { get; private set; }
        public string Description { get; private set; }
        public string ItemType { get; private set; }
        public IReadOnlyList<SonolusLevel> Levels { get; private set; }

        /// <summary>Optional search that lists the whole section, as a query string.</summary>
        public string SearchValues { get; private set; }

        internal static List<SonolusSection> ListFrom(JToken token)
        {
            var sections = new List<SonolusSection>();
            if (!(token is JArray array))
            {
                return sections;
            }

            foreach (JToken item in array)
            {
                if (!(item is JObject value))
                {
                    continue;
                }

                string type = Json.String(value, "itemType");
                if (type != null && type != "level")
                {
                    continue;
                }

                var section = new SonolusSection
                {
                    Title = Json.String(value, "title") ?? string.Empty,
                    Icon = Json.String(value, "icon"),
                    Description = Json.String(value, "description"),
                    ItemType = type ?? "level",
                    Levels = SonolusLevel.ListFrom(value["items"]),
                    SearchValues = Json.String(value, "searchValues")
                };
                if (section.Levels.Count > 0)
                {
                    sections.Add(section);
                }
            }

            return sections;
        }
    }

    /// <summary>One choice of a select or multi search option.</summary>
    public sealed class SonolusOptionValue
    {
        public SonolusOptionValue(string name, string title)
        {
            Name = name;
            Title = title;
        }

        public string Name { get; }
        public string Title { get; }
    }

    /// <summary>
    /// One field of a search form. Types the game can show: text, textArea, slider, toggle,
    /// select and multi; any other type is kept but not drawn (and not sent).
    /// </summary>
    public sealed class SonolusSearchOption
    {
        public string Query { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public string Type { get; private set; }
        public bool Required { get; private set; }

        /// <summary>Default value as text; for multi, the default flags joined by commas.</summary>
        public string Default { get; private set; }
        public string Placeholder { get; private set; }
        public int Limit { get; private set; }
        public double Min { get; private set; }
        public double Max { get; private set; }
        public double Step { get; private set; }
        public string Unit { get; private set; }
        public IReadOnlyList<SonolusOptionValue> Values { get; private set; }

        /// <summary>Default flags of a multi option, one per value.</summary>
        public IReadOnlyList<bool> DefaultFlags { get; private set; }

        public bool IsSupported =>
            Type == "text" || Type == "textArea" || Type == "slider" || Type == "toggle" || Type == "select" || Type == "multi";

        internal static SonolusSearchOption From(JObject value)
        {
            string type = Json.String(value, "type") ?? string.Empty;
            var values = new List<SonolusOptionValue>();
            if (value["values"] is JArray array)
            {
                foreach (JToken item in array)
                {
                    if (item is JObject choice)
                    {
                        string name = Json.String(choice, "name");
                        if (name != null)
                        {
                            values.Add(new SonolusOptionValue(name, Json.String(choice, "title") ?? name));
                        }
                    }
                    else if (item.Type == JTokenType.String)
                    {
                        // Older servers list bare titles; their index is the value.
                        values.Add(new SonolusOptionValue(values.Count.ToString(CultureInfo.InvariantCulture), (string)item));
                    }
                }
            }

            var flags = new List<bool>();
            JToken def = value["def"];
            string defaultText;
            if (def is JArray defArray)
            {
                foreach (JToken flag in defArray)
                {
                    flags.Add(flag.Type == JTokenType.Boolean && (bool)flag);
                }

                defaultText = null;
            }
            else if (def == null || def.Type == JTokenType.Null)
            {
                defaultText = null;
            }
            else if (def.Type == JTokenType.Boolean)
            {
                defaultText = (bool)def ? "1" : "0";
            }
            else if (def.Type == JTokenType.Float || def.Type == JTokenType.Integer)
            {
                defaultText = ((double)def).ToString("R", CultureInfo.InvariantCulture);
            }
            else
            {
                defaultText = def.ToString();
            }

            double step = Json.Number(value, "step");
            return new SonolusSearchOption
            {
                Query = Json.String(value, "query") ?? string.Empty,
                Name = Json.String(value, "name") ?? string.Empty,
                Description = Json.String(value, "description"),
                Type = type,
                Required = Json.Bool(value, "required"),
                Default = defaultText,
                Placeholder = Json.String(value, "placeholder"),
                Limit = (int)Json.Number(value, "limit"),
                Min = Json.Number(value, "min"),
                Max = Json.Number(value, "max"),
                Step = step > 0d ? step : 1d,
                Unit = Json.String(value, "unit"),
                Values = values,
                DefaultFlags = flags
            };
        }
    }

    /// <summary>A search form the server offers (Sonolus calls these "searches").</summary>
    public sealed class SonolusSearch
    {
        public string Type { get; private set; }
        public string Title { get; private set; }
        public string Icon { get; private set; }
        public IReadOnlyList<SonolusSearchOption> Options { get; private set; }

        internal static List<SonolusSearch> ListFrom(JToken token)
        {
            var searches = new List<SonolusSearch>();
            if (!(token is JArray array))
            {
                return searches;
            }

            foreach (JToken item in array)
            {
                if (!(item is JObject value))
                {
                    continue;
                }

                var options = new List<SonolusSearchOption>();
                if (value["options"] is JArray list)
                {
                    foreach (JToken option in list)
                    {
                        if (option is JObject field)
                        {
                            options.Add(SonolusSearchOption.From(field));
                        }
                    }
                }

                string type = Json.String(value, "type");
                if (string.IsNullOrEmpty(type))
                {
                    continue;
                }

                searches.Add(new SonolusSearch
                {
                    Type = type,
                    Title = Json.String(value, "title") ?? type,
                    Icon = Json.String(value, "icon"),
                    Options = options
                });
            }

            return searches;
        }
    }

    /// <summary>The server's own card: <c>GET /sonolus/info</c>.</summary>
    public sealed class SonolusServerInfo
    {
        public string Title { get; private set; }
        public string Description { get; private set; }
        public SonolusSrl Banner { get; private set; }

        /// <summary>False when the server's button list leaves levels out.</summary>
        public bool HasLevels { get; private set; }

        public static SonolusServerInfo From(JToken token)
        {
            var value = token as JObject ?? new JObject();
            bool hasLevels = true;
            if (value["buttons"] is JArray buttons)
            {
                hasLevels = false;
                foreach (JToken button in buttons)
                {
                    if (button is JObject entry && Json.String(entry, "type") == "level")
                    {
                        hasLevels = true;
                    }
                }
            }

            return new SonolusServerInfo
            {
                Title = Json.String(value, "title") ?? string.Empty,
                Description = Json.String(value, "description"),
                Banner = SonolusSrl.From(value["banner"]),
                HasLevels = hasLevels
            };
        }
    }

    /// <summary>The level home page: <c>GET /sonolus/levels/info</c>.</summary>
    public sealed class SonolusLevelsInfo
    {
        public IReadOnlyList<SonolusSection> Sections { get; private set; }
        public IReadOnlyList<SonolusSearch> Searches { get; private set; }
        public SonolusSrl Banner { get; private set; }

        public static SonolusLevelsInfo From(JToken token)
        {
            var value = token as JObject ?? new JObject();
            return new SonolusLevelsInfo
            {
                Sections = SonolusSection.ListFrom(value["sections"]),
                Searches = SonolusSearch.ListFrom(value["searches"]),
                Banner = SonolusSrl.From(value["banner"])
            };
        }
    }

    /// <summary>
    /// One page of a paged list. A server pages either by number (<see cref="PageCount"/> pages)
    /// or by cursor (a negative page count, and <see cref="Cursor"/> fetches what follows).
    /// </summary>
    public sealed class SonolusPage<T>
    {
        public SonolusPage(IReadOnlyList<T> items, int pageCount, string cursor)
        {
            Items = items;
            PageCount = pageCount;
            Cursor = cursor;
        }

        public IReadOnlyList<T> Items { get; }
        public int PageCount { get; }
        public string Cursor { get; }

        public bool UsesCursor => PageCount < 0;

        /// <summary>Whether another page follows <paramref name="page"/>.</summary>
        public bool HasMoreAfter(int page) => UsesCursor ? !string.IsNullOrEmpty(Cursor) : page + 1 < PageCount;
    }

    /// <summary>A leaderboard the detail page lists.</summary>
    public sealed class SonolusLeaderboard
    {
        public SonolusLeaderboard(string name, string title, string description)
        {
            Name = name;
            Title = title;
            Description = description;
        }

        public string Name { get; }
        public string Title { get; }
        public string Description { get; }
    }

    /// <summary>The level detail page: <c>GET /sonolus/levels/{name}</c>.</summary>
    public sealed class SonolusLevelDetails
    {
        public SonolusLevel Level { get; private set; }
        public string Description { get; private set; }
        public bool HasCommunity { get; private set; }
        public IReadOnlyList<SonolusLeaderboard> Leaderboards { get; private set; }
        public IReadOnlyList<SonolusSection> Sections { get; private set; }

        /// <summary>The whole response, kept in the package's sonolus-source.json.</summary>
        public JObject Raw { get; private set; }

        public static SonolusLevelDetails From(JToken token)
        {
            var value = token as JObject;
            SonolusLevel level = value != null ? SonolusLevel.From(value["item"]) : null;
            if (level == null)
            {
                return null;
            }

            var leaderboards = new List<SonolusLeaderboard>();
            if (value["leaderboards"] is JArray boards)
            {
                foreach (JToken board in boards)
                {
                    if (board is JObject entry)
                    {
                        string name = Json.String(entry, "name");
                        if (!string.IsNullOrEmpty(name))
                        {
                            leaderboards.Add(new SonolusLeaderboard(name, Json.String(entry, "title") ?? name, Json.String(entry, "description")));
                        }
                    }
                }
            }

            return new SonolusLevelDetails
            {
                Level = level,
                Description = Json.String(value, "description"),
                HasCommunity = Json.Bool(value, "hasCommunity"),
                Leaderboards = leaderboards,
                Sections = SonolusSection.ListFrom(value["sections"]),
                Raw = value
            };
        }
    }

    /// <summary>A comment on a level's community page.</summary>
    public sealed class SonolusComment
    {
        public string Name { get; private set; }
        public string Author { get; private set; }
        public DateTimeOffset? Time { get; private set; }
        public string Content { get; private set; }

        internal static List<SonolusComment> ListFrom(JToken token)
        {
            var comments = new List<SonolusComment>();
            if (!(token is JArray array))
            {
                return comments;
            }

            foreach (JToken item in array)
            {
                if (!(item is JObject value))
                {
                    continue;
                }

                double time = Json.Number(value, "time");
                comments.Add(new SonolusComment
                {
                    Name = Json.String(value, "name"),
                    Author = Json.String(value, "author") ?? string.Empty,
                    Time = time > 0d ? DateTimeOffset.FromUnixTimeMilliseconds((long)time) : (DateTimeOffset?)null,
                    Content = Json.String(value, "content") ?? string.Empty
                });
            }

            return comments;
        }
    }

    /// <summary>One line of a leaderboard: rank and value are display text the server formats.</summary>
    public sealed class SonolusRecord
    {
        public string Name { get; private set; }
        public string Rank { get; private set; }
        public string Player { get; private set; }
        public string Value { get; private set; }

        internal static List<SonolusRecord> ListFrom(JToken token)
        {
            var records = new List<SonolusRecord>();
            if (!(token is JArray array))
            {
                return records;
            }

            foreach (JToken item in array)
            {
                if (item is JObject value)
                {
                    records.Add(new SonolusRecord
                    {
                        Name = Json.String(value, "name"),
                        Rank = Json.String(value, "rank") ?? string.Empty,
                        Player = Json.String(value, "player") ?? string.Empty,
                        Value = Json.String(value, "value") ?? string.Empty
                    });
                }
            }

            return records;
        }
    }

    /// <summary>Lenient field readers: a missing or mistyped field reads as its empty value.</summary>
    internal static class Json
    {
        internal static string String(JObject value, string key)
        {
            JToken token = value[key];
            if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Object || token.Type == JTokenType.Array)
            {
                return null;
            }

            return token.Type == JTokenType.String ? (string)token : token.ToString();
        }

        internal static double Number(JObject value, string key)
        {
            JToken token = value[key];
            if (token == null)
            {
                return 0d;
            }

            if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
            {
                return (double)token;
            }

            return token.Type == JTokenType.String
                && double.TryParse((string)token, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
                ? parsed
                : 0d;
        }

        internal static bool Bool(JObject value, string key)
        {
            JToken token = value[key];
            return token != null && token.Type == JTokenType.Boolean && (bool)token;
        }
    }
}
