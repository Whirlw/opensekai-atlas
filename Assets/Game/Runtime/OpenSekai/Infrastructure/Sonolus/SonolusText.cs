using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenSekai.Infrastructure.Sonolus
{
    /// <summary>
    /// Servers write some labels as Sonolus text constants (<c>#NEWEST</c>, <c>#KEYWORDS</c>...)
    /// that the client is expected to localize. Known publishers also have translations for their
    /// own display text. Query keys and values must always come from the original response.
    /// </summary>
    public static class SonolusText
    {
        private static readonly Dictionary<string, string> _table = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["#NEWEST"] = "最新",
            ["#OLDEST"] = "最早",
            ["#RANDOM"] = "随机",
            ["#POPULAR"] = "热门",
            ["#RECOMMENDED"] = "推荐",
            ["#FEATURED"] = "精选",
            ["#TRENDING"] = "趋势",
            ["#HOT"] = "热门",
            ["#NEW"] = "新",
            ["#DEFAULT"] = "默认",
            ["#KEYWORDS"] = "关键词",
            ["#KEYWORD"] = "关键词",
            ["#KEYWORDS_PLACEHOLDER"] = "输入关键词",
            ["#SEARCH"] = "搜索",
            ["#ADVANCED"] = "高级",
            ["#QUICK"] = "快速",
            ["#TITLE"] = "标题",
            ["#SUBTITLE"] = "副标题",
            ["#ARTISTS"] = "艺术家",
            ["#AUTHOR"] = "作者",
            ["#AUTHORS"] = "作者",
            ["#DESCRIPTION"] = "描述",
            ["#GENRE"] = "曲风",
            ["#CATEGORY"] = "分类",
            ["#TAGS"] = "标签",
            ["#TAG"] = "标签",
            ["#RATING"] = "等级",
            ["#MIN_RATING"] = "最低等级",
            ["#MAX_RATING"] = "最高等级",
            ["#RATING_MINIMUM"] = "最低等级",
            ["#RATING_MAXIMUM"] = "最高等级",
            ["#DIFFICULTY"] = "难度",
            ["#LENGTH"] = "时长",
            ["#ENGINE"] = "引擎",
            ["#SORT"] = "排序",
            ["#SORT_BY"] = "排序方式",
            ["#ORDER"] = "顺序",
            ["#ASCENDING"] = "升序",
            ["#DESCENDING"] = "降序",
            ["#LIKES"] = "点赞",
            ["#LIKE"] = "点赞",
            ["#COMMENTS"] = "评论",
            ["#COMMENT"] = "评论",
            ["#PLAYS"] = "游玩次数",
            ["#DOWNLOADS"] = "下载次数",
            ["#PUBLIC"] = "公开",
            ["#PRIVATE"] = "私有",
            ["#UNLISTED"] = "未列出",
            ["#UPLOAD"] = "上传",
            ["#ALL"] = "全部",
            ["#ANY"] = "任意",
            ["#NONE"] = "无",
            ["#ON"] = "开",
            ["#OFF"] = "关",
            ["#YES"] = "是",
            ["#NO"] = "否",
            ["#EASY"] = "EASY",
            ["#NORMAL"] = "NORMAL",
            ["#HARD"] = "HARD",
            ["#EXPERT"] = "EXPERT",
            ["#MASTER"] = "MASTER",
            ["#APPEND"] = "APPEND",
            ["#COLLABORATOR"] = "合作者",
            ["#STAFF_PICK"] = "精选",
            ["#TIME"] = "时间",
            ["#DATE"] = "日期",
            ["#PUBLISHED_AT"] = "发布时间",
            ["#CREATED_AT"] = "上传时间",
            ["#UPDATED_AT"] = "更新时间",
            ["#USER"] = "用户",
            ["#LEVEL"] = "谱面",
            ["#LEVELS"] = "谱面",
            ["#ANNOUNCEMENT"] = "公告"
        };

        private static readonly Dictionary<string, string> _untitledCharts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Advanced Search"] = "高级搜索",
            ["Enter text"] = "输入文字",
            ["Staff Pick"] = "精选",
            ["Don't Filter by Staff Pick"] = "不限",
            ["Only Staff Picks"] = "仅精选谱面",
            ["Only Not Staff Picks"] = "仅非精选谱面",
            ["Minimum Rating"] = "最低等级",
            ["Maximum Rating"] = "最高等级",
            ["Title Contains"] = "标题包含",
            ["Chart Author Name Contains"] = "谱师名称包含",
            ["Description Contains"] = "简介包含",
            ["Artists Contains"] = "艺术家名称包含",
            ["Minimum Likes"] = "最少点赞数",
            ["Maximum Likes"] = "最多点赞数",
            ["Minimum Comments"] = "最少评论数",
            ["Maximum Comments"] = "最多评论数",
            ["Tags (comma-separated)"] = "标签（用英文逗号分隔）",
            ["Enter tags"] = "输入标签",
            ["Sort By"] = "排序方式",
            ["Date Published"] = "发布时间",
            ["Date Uploaded"] = "上传时间",
            ["Rating"] = "等级",
            ["Likes"] = "点赞数",
            ["Comments Count"] = "评论数",
            ["Trending"] = "热度",
            ["Title (A-Z)"] = "标题（A–Z）",
            ["Sort Order"] = "排序顺序",
            ["Descending"] = "降序",
            ["Ascending"] = "升序",
            ["Random Staff Pick"] = "随机精选",
            ["Staff picks are charts we've reviewed and found both fun and playable!"] = "精选谱面由团队审核，兼顾趣味性和可玩性。",
            ["Arcade Score"] = "街机分数",
            ["Accuracy Score"] = "准确度分数",
            ["Arcade Score (no speed bonus)"] = "街机分数（无速度加成）",
            ["Ranked Match"] = "排位赛",
            ["Least Combo Breaks"] = "最少断连",
            ["Least Misses"] = "最少 MISS",
            ["Perfect Count"] = "PERFECT 数"
        };

        private static readonly Dictionary<string, string> _sbuga = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["#ADVANCED"] = "高级搜索",
            ["#ARTISTS"] = "演唱者",
            ["#CATEGORY"] = "演唱版本",
            ["Descending"] = "降序",
            ["Ascending"] = "升序",
            ["25時、ナイトコードで。"] = "25时，在 Nightcord。",
            ["ALL ORIGINAL ユニット"] = "全原创组合",
            ["ALLユニット"] = "全组合",
            ["バーチャル・シンガー"] = "虚拟歌手",
            ["Nightcord at 25:00 ver."] = "25时，在 Nightcord。版",
            ["COLORFUL LIVE ver."] = "COLORFUL LIVE 版",
            ["Instrumental ver."] = "纯伴奏版",
            ["Leo/need ver."] = "Leo/need 版",
            ["MORE MORE JUMP！ ver."] = "MORE MORE JUMP！版",
            ["VIRTUAL SINGER ver."] = "虚拟歌手版",
            ["Vivid BAD SQUAD ver."] = "Vivid BAD SQUAD 版",
            ["COLORFUL STAGE! The Movie ver."] = "《世界计划》剧场版",
            ["Ensemble Stars!! Crossover ver."] = "《偶像梦幻祭！！》联动版",
            ["Cover ver."] = "其他演唱版",
            ["April Fool's ver."] = "愚人节版",
            ["コネクトライブ(DAY1)ver."] = "Connect Live 第 1 天版",
            ["コネクトライブ(DAY1/DAY2)ver."] = "Connect Live 第 1、2 天版",
            ["Connect Live (DAY1 Night) ver."] = "Connect Live 第 1 天晚场版",
            ["Connect Live (DAY1 Day) ver."] = "Connect Live 第 1 天日场版",
            ["コネクトライブ(DAY2)ver."] = "Connect Live 第 2 天版",
            ["Connect Live (DAY2 Night) ver."] = "Connect Live 第 2 天晚场版",
            ["Connect Live (DAY2 Day) ver."] = "Connect Live 第 2 天日场版",
            ["Connect Live ver."] = "Connect Live 版",
            ["SEKAI ver."] = "SEKAI 版",
            ["Wonderlands×Showtime ver."] = "Wonderlands×Showtime 版"
        };

        /// <summary>Localizes Sonolus constants and known display text from this publisher.</summary>
        public static string Localize(string text, SonolusServerAddress server = null)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var translations = server?.Host switch
            {
                "untitledcharts.com" or "untitledcharts.com:443" => _untitledCharts,
                "sonolus.sbuga.com" or "sonolus.sbuga.com:443" => _sbuga,
                _ => null
            };
            if (translations != null && translations.TryGetValue(text, out string translated))
            {
                return translated;
            }

            if (text[0] != '#' || text.Length < 2)
            {
                return text;
            }

            if (_table.TryGetValue(text, out string localized))
            {
                return localized;
            }

            string words = text.Substring(1).Replace('_', ' ').ToLowerInvariant();
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(words);
        }
    }
}
