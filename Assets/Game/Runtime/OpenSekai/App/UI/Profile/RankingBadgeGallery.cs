using System;
using LitMotion;
using OpenSekai.Shared.Playback;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// 评级徽章, opened from the badge on the 个人信息 card: every Ranking Score tier in one row from
    /// 星尘 to 寰宇, each badge at the result page's size over its name, the score it starts at and
    /// where the player stands against it (reached, the current one ringed, or how far off). The
    /// badges run their idle motion, and a tap on one replays that tier's upgrade as the result page
    /// plays it, charging up as the tier below and revealing as its own. It only reads the score the
    /// page hands it and never touches the profile.
    /// </summary>
    internal sealed class RankingBadgeGallery : IDisposable
    {
        private const float CARD_WIDTH = 1780f;
        private const float CARD_HEIGHT = 584f;

        /// <summary>The result page's badge size, so the crests drawn for one serve the other.</summary>
        private const float BADGE_SIZE = 148f;

        /// <summary>The badge's center, down from the top of its tile.</summary>
        private const float BADGE_Y = 136f;

        private const float TILE_HEIGHT = 420f;
        private const float GAP = 14f;

        /// <summary>How long a replay runs on once the tier shows: the reveal, then the flare dying away.</summary>
        private const float REPLAY_TAIL = RankingBadge.UPGRADE_BURST + 0.2f;

        private const string HINT = "点按徽章可以重播它的升级特效";

        private readonly LibraryModal _modal;
        private readonly Tile[] _tiles;
        private MotionHandle _replay;
        private Tile _replaying;

        internal RankingBadgeGallery(RectTransform layer)
        {
            RectTransform root = LibraryUi.CreateRect("RankingBadges", layer);
            LibraryUi.Stretch(root);
            _modal = new LibraryModal(root, new Vector2(CARD_WIDTH, CARD_HEIGHT), "评级徽章", Close, withSubtitle: true);
            _tiles = new Tile[RankingTiers.Count];
            for (int tier = 0; tier < _tiles.Length; tier++)
            {
                _tiles[tier] = new Tile(_modal.Body, tier, _tiles.Length, this);
            }

            root.gameObject.SetActive(false);
        }

        internal bool IsOpen => _modal.IsOpen;

        /// <summary>The player's tier as last opened; -1 when the profile could not be read.</summary>
        internal int CurrentTier { get; private set; } = -1;

        internal int Count => _tiles.Length;

        /// <summary>Starts drawing every tier's crest, so the card opens with them already in.</summary>
        internal void Warm()
        {
            foreach (Tile tile in _tiles)
            {
                tile.Badge.Warm(tile.Tier);
            }
        }

        /// <param name="total">The player's Ranking Score; null when the profile could not be read.</param>
        internal void Open(long? total)
        {
            StopReplay();
            CurrentTier = total.HasValue ? Mathf.Clamp(RankingRules.Tier(total.Value), 0, _tiles.Length - 1) : -1;
            _modal.Subtitle.text = total.HasValue
                ? "当前 Ranking Score " + ProfileCopy.Number(total.Value) + "  ·  " + HINT
                : "档案暂时无法读取  ·  " + HINT;
            foreach (Tile tile in _tiles)
            {
                tile.Bind(total, CurrentTier);
            }

            _modal.Open();
        }

        internal void Close()
        {
            StopReplay();
            _modal.Close();
        }

        /// <summary>
        /// Holds <paramref name="tier"/>'s replay at <paramref name="time"/> seconds from the moment
        /// the tier shows (negative while it is still charging), without its sound; for tools.
        /// </summary>
        internal void SeekReplay(int tier, float time)
        {
            StopReplay();
            _replaying = _tiles[tier];
            _replaying.ApplyReplay(time, false);
        }

        /// <summary>The tier the badge on <paramref name="index"/>'s tile shows right now.</summary>
        internal int BadgeTier(int index) => _tiles[index].Badge.Tier;

        /// <summary>The line under <paramref name="index"/>'s score: reached, current or how far off.</summary>
        internal string Status(int index) => _tiles[index].Status;

        public void Dispose()
        {
            _replay.TryCancel();
            _modal.Dispose();
        }

        private void Replay(Tile tile)
        {
            StopReplay();
            _replaying = tile;
            const float START = -RankingBadge.UPGRADE_CHARGE;
            _replay = LMotion.Create(START, REPLAY_TAIL, REPLAY_TAIL - START)
                .WithScheduler(HomeMotion.Scheduler)
                .WithOnComplete(FinishReplay)
                .Bind(time => tile.ApplyReplay(time, true));
        }

        private void StopReplay()
        {
            _replay.TryCancel();
            FinishReplay();
        }

        private void FinishReplay()
        {
            _replay = default;
            _replaying?.Rest();
            _replaying = null;
        }

        /// <summary>One tier: its badge with a flare behind it for the replay, and its lines of text.</summary>
        private sealed class Tile
        {
            private readonly Image _background;
            private readonly Image _ring;
            private readonly Image _flare;
            private readonly Image _pill;
            private readonly TextMeshProUGUI _status;
            private readonly Color32 _tint;
            private bool _swapped;

            internal Tile(RectTransform body, int tier, int count, RankingBadgeGallery owner)
            {
                Tier = tier;
                // Equal widths across the row with an even gap between neighbours.
                Root = LibraryUi.CreateRect(RankingTiers.LatinName(tier), body);
                Root.pivot = new Vector2(0.5f, 1f);
                Root.anchorMin = new Vector2((float)tier / count, 1f);
                Root.anchorMax = new Vector2((float)(tier + 1) / count, 1f);
                Root.offsetMin = new Vector2(tier * GAP / count, -TILE_HEIGHT);
                Root.offsetMax = new Vector2(-(count - 1 - tier) * GAP / count, 0f);
                _tint = RankingTiers.IsPrismatic(tier) ? LibraryTheme.Colors.AllPerfectStops[1] : RankingTiers.Color(tier);

                _background = LibraryWidgets.Panel("Background", Root, LibraryTheme.Radius.ROW, LibraryTheme.Colors.Control);
                LibraryUi.Stretch(_background.rectTransform);
                _ring = LibraryWidgets.Shape("Ring", Root, LibraryShapes.Ring(LibraryTheme.Radius.ROW, 2.5f), Color.clear);
                LibraryUi.Stretch(_ring.rectTransform);
                // Silent: the replay plays the upgrade's own sound as the tier shows.
                PackWidgets.RowButton(Root, () => owner.Replay(this), UiCue.None, Root);

                Vector2 top = new Vector2(0.5f, 1f);
                Vector2 center = new Vector2(0.5f, 0.5f);
                _flare = LibraryUi.CreateImage("Flare", Root, Color.clear);
                _flare.sprite = LibraryShapes.Glow();
                _flare.raycastTarget = false;
                LibraryWidgets.Place(_flare.rectTransform, top, center, new Vector2(0f, -BADGE_Y), Vector2.one * (BADGE_SIZE * 2f));
                RectTransform slot = LibraryUi.CreateRect("Slot", Root);
                LibraryWidgets.Place(slot, top, center, new Vector2(0f, -BADGE_Y), Vector2.one * BADGE_SIZE);
                Badge = new RankingBadge(slot, BADGE_SIZE, animate: true);

                TextMeshProUGUI name = LibraryWidgets.Label("Name", Root, RankingTiers.Name(tier), 30f, LibraryTheme.Colors.TextPrimary,
                    FontStyles.Bold, TextAlignmentOptions.Center);
                RankingTiers.Paint(name, tier);
                LibraryWidgets.TopStrip(name.rectTransform, 8f, 8f, 252f, 42f);
                TextMeshProUGUI latin = LibraryWidgets.Label("Latin", Root, RankingTiers.LatinName(tier), 16f, LibraryTheme.Colors.TextMuted,
                    alignment: TextAlignmentOptions.Center);
                LibraryUi.UseSpacedCondensedFont(latin, 3f);
                LibraryWidgets.TopStrip(latin.rectTransform, 8f, 8f, 296f, 22f);
                TextMeshProUGUI start = LibraryWidgets.Label("Start", Root, ProfileCopy.Number(RankingTiers.Start(tier)) + " 分起", 19f,
                    LibraryTheme.Colors.TextSecondary, alignment: TextAlignmentOptions.Center);
                LibraryWidgets.TopStrip(start.rectTransform, 8f, 8f, 328f, 28f);

                const float STATUS_TOP = 372f;
                const float STATUS_HEIGHT = 32f;
                _pill = LibraryWidgets.Panel("Pill", Root, STATUS_HEIGHT * 0.5f, Color.clear);
                LibraryWidgets.Place(_pill.rectTransform, top, center, new Vector2(0f, -(STATUS_TOP + STATUS_HEIGHT * 0.5f)),
                    new Vector2(116f, STATUS_HEIGHT));
                _status = LibraryWidgets.Label("Status", Root, string.Empty, 17f, LibraryTheme.Colors.TextMuted, FontStyles.Bold,
                    TextAlignmentOptions.Center);
                LibraryWidgets.TopStrip(_status.rectTransform, 8f, 8f, STATUS_TOP, STATUS_HEIGHT);
            }

            internal int Tier { get; }

            internal RectTransform Root { get; }

            internal RankingBadge Badge { get; }

            internal string Status => _status.text;

            internal void Bind(long? total, int current)
            {
                bool isCurrent = Tier == current;
                _background.color = isCurrent ? HomeTheme.Colors.WithAlpha(_tint, 26) : LibraryTheme.Colors.Control;
                _ring.color = isCurrent ? (Color)Color.Lerp(_tint, Color.white, 0.25f) : Color.clear;
                _pill.color = isCurrent ? (Color)HomeTheme.Colors.WithAlpha(_tint, 44) : Color.clear;
                if (isCurrent)
                {
                    _status.text = "当前评级";
                    RankingTiers.Paint(_status, Tier);
                }
                else
                {
                    _status.enableVertexGradient = false;
                    _status.color = LibraryTheme.Colors.TextMuted;
                    _status.text = !total.HasValue ? string.Empty
                        : Tier < current ? "已达成"
                        : "还差 " + ProfileCopy.Number(RankingTiers.Start(Tier) - total.Value) + " 分";
                }

                Rest();
            }

            /// <summary>The badge at rest on its own tier.</summary>
            internal void Rest()
            {
                _swapped = false;
                Badge.SetTier(Tier);
                Badge.SetMotion(1f, 0f, 1f);
                _flare.color = Color.clear;
            }

            /// <param name="time">Seconds from the moment the tier shows; before it the badge charges as the tier below.</param>
            /// <param name="sound">Play the upgrade's sound as the tier shows.</param>
            internal void ApplyReplay(float time, bool sound)
            {
                bool swapped = time >= 0f;
                Badge.SetTier(swapped || Tier == 0 ? Tier : Tier - 1);
                Badge.SetUpgrade(time);
                if (sound && swapped && !_swapped)
                {
                    UiSound.Play(UiCue.Achievement);
                }

                _swapped = swapped;
                // A flare in the tier's color, brighter the higher the tier, as the result page's glow.
                float flare = swapped ? 1f - HomeMotion.Span(time, 0f, RankingBadge.UPGRADE_BURST + 0.1f) : 0f;
                _flare.color = HomeTheme.Colors.WithAlpha(_tint, (byte)Mathf.RoundToInt(flare * flare * (60f + 16f * Tier)));
            }
        }
    }
}
