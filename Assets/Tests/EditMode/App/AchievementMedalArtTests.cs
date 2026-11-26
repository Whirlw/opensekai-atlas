using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenSekai.App.Library;
using OpenSekai.App.UI;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.Tests.EditMode
{
    /// <summary>The achievements' emblems, special medals and avatars, as drawings and on the widgets that wear them.</summary>
    public sealed class AchievementMedalArtTests
    {
        private static readonly string[] _special = AchievementGoals.All.Where(goal => goal.MedalTier == MedalTiers.SPECIAL)
            .Select(goal => goal.Id).ToArray();

        /// <summary>The profile's plate, the picker's, a home chip and a reward thumbnail.</summary>
        private static readonly (string Name, PlateSpace Space)[] _spaces =
        {
            ("profile", Plate(488f / 62f, 1.4f)), ("picker", Plate(5f, 1.1f)), ("chip", new PlateSpace(4f, true, -2f, 0.72f, 0.4f)),
            ("thumb", new PlateSpace(2f, true, -1f, 1f, 1f))
        };

        private GameObject _canvas;

        [SetUp]
        public void SetUp()
        {
            _canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            ((RectTransform)_canvas.transform).sizeDelta = new Vector2(1920f, 1080f);
        }

        [TearDown]
        public void TearDown()
        {
            MedalClock.Held = null;
            UnityEngine.Object.DestroyImmediate(_canvas);
        }

        [Test]
        public void EveryGoalHasAnEmblemAndOnlyTheSpecialOnesAFaceOfTheirOwn()
        {
            Assert.That(_special.Length, Is.EqualTo(11));
            foreach (AchievementGoal goal in AchievementGoals.All)
            {
                AchievementLook look = AchievementCopy.For(goal.Id);
                Assert.That(look.Glyph, Is.EqualTo(AchievementGlyph.Emblem), goal.Id);
                bool special = _special.Contains(goal.Id);
                Assert.That(SpecialPlateArt.Has(look.Emblem), Is.EqualTo(special), goal.Id);
                Assert.That(special || MedalEmblemArt.Has(look.Emblem), Is.True, goal.Id);
                MedalLook medal = MedalLook.Of(look);
                Assert.That(medal.Art, Is.EqualTo(look.Emblem), goal.Id);
                if (!goal.GivesMedal) continue;
                Assert.That((medal.Label, medal.Grade, medal.Tier), Is.EqualTo((goal.MedalName, goal.MedalGrade, goal.MedalFrameTier)), goal.Id);
            }
        }

        [Test]
        public void ASeriesKeepsOneColorAndEmblemExceptTheRankingScores()
        {
            foreach (IGrouping<string, AchievementGoal> steps in AchievementGoals.All.GroupBy(goal => goal.Series))
            {
                List<AchievementLook> looks = steps.Where(goal => !_special.Contains(goal.Id)).Select(goal => AchievementCopy.For(goal.Id)).ToList();
                if (looks.Count == 0) continue;
                bool ranking = steps.Key == "B30";
                Assert.That(looks.Select(look => look.Emblem).Distinct().Count(), Is.EqualTo(ranking ? 6 : 1), steps.Key);
                Assert.That(looks.Select(look => look.Tint).Distinct().Count(), Is.EqualTo(ranking ? 6 : 1), steps.Key);
            }

            Assert.That(AchievementCopy.For("B30-04").Tint, Is.EqualTo(RankingTiers.Color(4)), "the color of the tier it is named after");
        }

        [Test]
        public void CopyFitsItsRow()
        {
            foreach (AchievementGoal goal in AchievementGoals.All)
            {
                AchievementLook look = AchievementCopy.For(goal.Id);
                Assert.That(look.Goal, Does.Not.EndWith("。"), goal.Id);
                // Two half-width characters are one full-width; the row has room for about thirty of those.
                Assert.That(Width(look.Goal), Is.LessThanOrEqualTo(30f), goal.Id + " " + look.Goal);
                Assert.That(Width(look.Rule), Is.InRange(1f, 68f), goal.Id + " " + look.Rule);
                Assert.That(look.Latin, Is.Not.Empty.And.Not.EqualTo(goal.Id), goal.Id);
            }

            Assert.That(AchievementCopy.For("EAR-05").Goal, Does.Contain("15 局").And.Contain("99.5%"));
            Assert.That(AchievementCopy.For("FC-04").Goal, Is.EqualTo("在 10 张 Lv.30+、1,000 键以上的谱面取得 FULL COMBO"));
            Assert.That(AchievementCopy.Count(new AchievementProgress(AchievementGoals.Find("DAY-03"), 4)), Is.EqualTo("4 / 30 天"));
        }

        [Test]
        public void ALegacyMedalKeepsItsMark()
        {
            MedalLook look = MedalLook.Of(AchievementCopy.For("manual_hits_50000"));
            Assert.That((look.Mark, look.Art, look.Label), Is.EqualTo(("50K", (string)null, "五万键之路")));
        }

        [Test]
        public void EveryDrawingIsSoundAndStaysNearItsMedal([ValueSource(nameof(_special))] string id)
        {
            var mesh = new VectorMesh();
            foreach ((string name, PlateSpace space) in _spaces)
            {
                SpecialPlateArt art = SpecialPlateArt.Create(id);
                art.SetSpace(space);
                Action<VectorMesh>[] still = { art.Ground, art.Frame };
                foreach (Action<VectorMesh> draw in still)
                {
                    mesh.Begin(1f, 0.02f);
                    draw(mesh);
                    Check(mesh, space.HalfWidth + SpecialPlateArt.MARGIN + 0.1f, 0.5f + SpecialPlateArt.MARGIN, id + " " + name);
                }

                for (float time = 0f; time < 9f; time += 0.37f)
                {
                    mesh.Begin(1f, 0.02f);
                    art.GroundMotion(mesh, time);
                    art.FrameMotion(mesh, time);
                    Check(mesh, space.HalfWidth + SpecialPlateArt.MARGIN + 0.2f, 0.5f + SpecialPlateArt.MARGIN + 0.15f, id + " " + name + " at " + time);
                    Assert.That(mesh.Vertices.Count, Is.LessThan(5000), id + " " + name + " is drawn every frame");
                }
            }

            SpecialPlateArt plain = SpecialPlateArt.Create(id);
            mesh.Begin(1f, 0.02f);
            plain.Emblem(mesh);
            Check(mesh, 0.72f, 0.6f, id + " emblem");
            Assert.That(mesh.Indices, Is.Not.Empty);
            mesh.Begin(1f, 0.02f);
            plain.Disc(mesh);
            plain.DiscMotion(mesh, 1.3f);
            Check(mesh, 0.85f, 0.85f, id + " disc");
            Assert.That(mesh.Indices, Is.Not.Empty);
        }

        [Test]
        public void TheSpecialMedalsDifferFromOneAnotherAndMove()
        {
            var seen = new HashSet<string>();
            foreach (string id in _special)
            {
                foreach ((string name, PlateSpace space) in _spaces)
                {
                    SpecialPlateArt art = SpecialPlateArt.Create(id);
                    art.SetSpace(space);
                    string still = Print(mesh =>
                    {
                        art.Ground(mesh);
                        art.Frame(mesh);
                    });
                    Assert.That(seen.Add(name + still), Is.True, id + " looks like another one as a " + name);
                    Assert.That(Print(mesh => art.FrameMotion(mesh, 0.4f)), Is.Not.EqualTo(Print(mesh => art.FrameMotion(mesh, 0.9f))), id + " " + name);
                }

                SpecialPlateArt disc = SpecialPlateArt.Create(id);
                Assert.That(Print(mesh => disc.DiscMotion(mesh, 0.4f)), Is.Not.EqualTo(Print(mesh => disc.DiscMotion(mesh, 0.9f))), id);
            }
        }

        [Test]
        public void ALetterUpAddsAPeakOrACrystal()
        {
            foreach (string series in new[] { "FCLVL", "APLVL" })
            {
                int before = 0;
                for (int level = 11; level <= 14; level++)
                {
                    SpecialPlateArt art = SpecialPlateArt.Create(series + "-" + level);
                    art.SetSpace(_spaces[1].Space);
                    var mesh = new VectorMesh();
                    mesh.Begin(1f, 0.02f);
                    art.Frame(mesh);
                    Assert.That(mesh.Vertices.Count, Is.GreaterThan(before), series + "-" + level);
                    before = mesh.Vertices.Count;
                }
            }
        }

        [Test]
        public void APlateTakesASpecialMedalAndGivesItUpAgain()
        {
            var plate = new MedalPlate(_canvas.transform, "Plate", new Vector2(400f, 80f));
            Assert.That(Graphics(plate.Root), Is.Empty, "nothing is built for a plain medal");
            plate.SetLook(MedalLook.Of(AchievementCopy.For("FCLVL-12")));
            plate.SetLocked(false);
            Assert.That(Graphics(plate.Root).Count(graphic => graphic.enabled), Is.EqualTo(4));
            Assert.That(Graphics(plate.Root).Count(graphic => graphic.enabled && graphic.IsAnimated), Is.EqualTo(2));
            Assert.That(Image(plate.Root, "Rim").enabled || Image(plate.Root, "Frame").enabled, Is.False, "its frame is its own");
            Assert.That(plate.Label.enableVertexGradient, Is.True);

            plate.SetLook(MedalLook.Of(AchievementCopy.For("CODA-01")));
            Assert.That(Image(plate.Root, "Frame").enabled, Is.True, "终乐章 keeps 虹阶's frame");
            Assert.That(Image(plate.Root, "GradeLine").enabled, Is.False, "the final barline divides its grade");

            plate.SetLook(MedalLook.Of(AchievementCopy.For("SET-05")));
            Assert.That(Graphics(plate.Root).Any(graphic => graphic.enabled), Is.False);
            Assert.That(Image(plate.Root, "Frame").enabled, Is.True);
            Assert.That(plate.Label.enableVertexGradient, Is.False);
            plate.SetEmpty("未佩戴奖牌");
            Assert.That(plate.Label.text, Is.EqualTo("未佩戴奖牌"));
            plate.Dispose();
        }

        [Test]
        public void ASpecialMedalNotEarnedStandsStill()
        {
            var plate = new MedalPlate(_canvas.transform, "Plate", new Vector2(400f, 80f));
            plate.SetLook(MedalLook.Of(AchievementCopy.For("PRISM-01")));
            plate.SetLocked(true);
            Assert.That(Graphics(plate.Root).Count(graphic => graphic.enabled), Is.EqualTo(2));
            plate.Dispose();
        }

        [Test]
        public void AChipIsAsWideAsASpecialMedalsTextAsks()
        {
            var chip = new MedalChip(_canvas.transform, "Chip");
            chip.SetLook(MedalLook.Of(AchievementCopy.For("ENCORE-01")));
            float wide = chip.FitWidth;
            Assert.That(Graphics(chip.Root).Count(graphic => graphic.enabled), Is.EqualTo(4));
            chip.SetWidth(wide - 40f);
            Assert.That(chip.Root.sizeDelta.x, Is.EqualTo(wide - 40f));
            chip.SetLook(MedalLook.Of(AchievementCopy.For("FCLVL-11")));
            Assert.That(chip.FitWidth, Is.LessThan(wide));
            chip.SetEmpty("未佩戴奖牌");
            Assert.That(Graphics(chip.Root).Any(graphic => graphic.enabled), Is.False);
        }

        [Test]
        public void ADiscWearsTheSpecialFaceOnceReached()
        {
            var medal = new AchievementMedal(_canvas.transform, "Disc", 108f);
            AchievementLook look = AchievementCopy.For("APLVL-13");
            medal.Bind(look, AchievementState.InProgress, 0f);
            Assert.That(Graphics(medal.Root).Where(graphic => graphic.enabled).Select(graphic => graphic.name), Is.EqualTo(new[] { "MedalEmblem" }));
            Assert.That(Image(medal.Root, "MedalTrack").enabled, Is.True);
            medal.Bind(look, AchievementState.Claimable, 1f);
            Assert.That(Graphics(medal.Root).Count(graphic => graphic.enabled), Is.EqualTo(3));
            Assert.That(Image(medal.Root, "MedalTrack").enabled || Image(medal.Root, "MedalArc").enabled, Is.False, "its rim takes the ring's place");
            medal.Bind(AchievementCopy.For("APLVL-10"), AchievementState.Unlocked, 1f);
            Assert.That(Graphics(medal.Root).Count(graphic => graphic.enabled), Is.EqualTo(1));
            Assert.That(Image(medal.Root, "MedalArc").enabled, Is.True);
        }

        [Test]
        public void EveryAvatarAnAchievementGivesIsDrawn()
        {
            var mesh = new VectorMesh();
            foreach (AchievementGoal goal in AchievementGoals.All.Where(goal => goal.AvatarId != null))
            {
                Assert.That(ProfileAvatars.IsKnown(goal.AvatarId), Is.True, goal.AvatarId);
                ProfileAvatars.Avatar avatar = ProfileAvatars.Find(goal.AvatarId);
                Assert.That(avatar.Kind == ProfileAvatars.Emblem.Art, Is.EqualTo(AvatarArt.Has(goal.AvatarId)), goal.AvatarId);
                if (avatar.Kind != ProfileAvatars.Emblem.Art) continue;
                mesh.Begin(1f, 0.02f);
                AvatarArt.Draw(mesh, goal.AvatarId, VectorMesh.Ink.Of(avatar.Color.r, avatar.Color.g, avatar.Color.b));
                Assert.That(mesh.Indices, Is.Not.Empty, goal.AvatarId);
                Check(mesh, 0.75f, 0.85f, goal.AvatarId);
            }

            Assert.That(ProfileAvatars.All.Select(avatar => avatar.Id), Is.Unique);
            var view = new ProfileAvatarView(_canvas.transform, "Avatar", 112f);
            view.SetAvatar("prism_eye");
            Assert.That(Graphics(view.Root).Single().enabled, Is.True);
            view.SetAvatar("halo");
            Assert.That(Graphics(view.Root), Is.Empty);
        }

        [Test]
        public void AStrokeLeavesOutWhatCannotBeSeen()
        {
            var mesh = new VectorMesh();
            VectorMesh.Path Line()
            {
                VectorMesh.Path path = mesh.NewPath();
                path.Add(0f, 0f);
                path.Add(10f, 0f);
                path.Subdivide(0.1f, false);
                return path;
            }

            mesh.Begin(1f, 0.02f);
            mesh.Stroke(Line(), false, 0.1f, VectorMesh.Ink.White(1f));
            int whole = mesh.Vertices.Count;
            mesh.Begin(1f, 0.02f);
            mesh.Stroke(Line(), false, 0.1f, new VectorMesh.Comet(VectorMesh.Ink.White(1f), VectorMesh.Ink.White(1f), 0.5f, 0.1f, false));
            Assert.That(mesh.Vertices.Count, Is.InRange(8, whole / 4));
            Check(mesh, 10f, 1f, "a comet");
            Assert.That(mesh.Vertices.Min(vertex => vertex.X), Is.GreaterThan(3.8f));
        }

        private static PlateSpace Plate(float aspect, float text)
        {
            float half = aspect * 0.5f - MedalArt.INSET;
            return new PlateSpace(aspect, false, -half + 0.83f, half - 0.93f, -half + 1.04f + text);
        }

        private static string Print(Action<VectorMesh> draw)
        {
            var mesh = new VectorMesh();
            mesh.Begin(1f, 0.02f);
            draw(mesh);
            return string.Join(";", mesh.Vertices.Select(vertex => vertex.X.ToString("F3") + "," + vertex.Y.ToString("F3") + "," + vertex.Ink.A.ToString("F2")));
        }

        private static void Check(VectorMesh mesh, float reachX, float reachY, string what)
        {
            Assert.That(mesh.Indices.Count % 3, Is.Zero, what);
            Assert.That(mesh.Indices.All(index => index >= 0 && index < mesh.Vertices.Count), Is.True, what);
            foreach (VectorMesh.Vertex vertex in mesh.Vertices)
            {
                bool near = Math.Abs(vertex.X) <= reachX && Math.Abs(vertex.Y) <= reachY;
                // An edge that has faded out may lie further off.
                Assert.That(near || vertex.Ink.A < 0.004f && Math.Abs(vertex.X) + Math.Abs(vertex.Y) < 100f, Is.True,
                    what + " has a point at " + vertex.X + ", " + vertex.Y + " alpha " + vertex.Ink.A);
                Assert.That(vertex.Ink.A, Is.InRange(0f, 1.0001f), what);
            }
        }

        private static float Width(string text) => text.Sum(character => character < 0x2000 ? 0.5f : 1f);

        /// <summary>Every drawing but the emblem in a plate's band.</summary>
        private static VectorGraphic[] Graphics(RectTransform root) =>
            root.GetComponentsInChildren<VectorGraphic>(true).Where(graphic => graphic.transform.parent.name != "Face").ToArray();

        private static Image Image(RectTransform root, string name) =>
            root.GetComponentsInChildren<Image>(true).Single(image => image.name == name);
    }
}
