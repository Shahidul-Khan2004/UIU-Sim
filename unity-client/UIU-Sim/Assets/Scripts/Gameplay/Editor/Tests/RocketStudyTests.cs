using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UIU.Simulator.Gameplay.Library;
using UIU.Simulator.Gameplay.Library.RocketStudy;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace UIU.Simulator.Gameplay.Editor.Tests
{
    public sealed class RocketStudyTests
    {
        [TestCase(0f, 30f, 0)]
        [TestCase(3f, 30f, 10)]
        [TestCase(9f, 30f, 30)]
        [TestCase(15f, 30f, 50)]
        [TestCase(21f, 30f, 70)]
        [TestCase(27f, 30f, 90)]
        [TestCase(30f, 30f, 100)]
        [TestCase(60f, 30f, 100)]
        [TestCase(-1f, 30f, 0)]
        [TestCase(5f, 10f, 50)]
        [TestCase(1f, 0f, 0)]
        public void Score_UsesOnlyClampedSurvivalTime(float seconds, float duration, int score)
        {
            Assert.That(RocketStudySimulation.CalculateScore(seconds, duration), Is.EqualTo(score));
        }

        [Test]
        public void Ready_DoesNotStartTimer()
        {
            var run = NewRun();
            run.Advance(30f, false);
            Assert.That(run.Elapsed, Is.Zero);
            Assert.That(run.State, Is.EqualTo(RocketStudySimulation.RunState.Ready));
            Assert.That(run.Obstacles, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BoundaryCollision_FinishesExactlyOnce(bool thrust)
        {
            var run = NewRun();
            int callbacks = 0;
            run.Ended += result => { callbacks++; Assert.That(result.Completed, Is.True); };
            run.Begin();
            run.Advance(30f, thrust);
            float finishedAt = run.Elapsed;
            run.Advance(30f, thrust);
            run.Begin();
            run.Cancel();
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(run.State, Is.EqualTo(RocketStudySimulation.RunState.Finished));
            Assert.That(finishedAt, Is.InRange(0f, 3f));
            Assert.That(run.Score, Is.EqualTo(RocketStudySimulation.CalculateScore(finishedAt, 30f)));
            Assert.That(run.RocketBounds.yMin, Is.GreaterThanOrEqualTo(-RocketStudySimulation.Height / 2f));
            Assert.That(run.RocketBounds.yMax, Is.LessThanOrEqualTo(RocketStudySimulation.Height / 2f));
        }

        [Test]
        public void FullDuration_FinishesOnceWithExactly100_WithoutRealTimeWait()
        {
            var run = new RocketStudySimulation(new RocketStudySettings { gravity = 0f }, 42);
            int callbacks = 0;
            run.Ended += result => { callbacks++; Assert.That(result.Score, Is.EqualTo(100)); };
            run.Begin();
            run.Advance(60f, false);
            run.Advance(60f, false);
            run.Cancel();
            Assert.That(run.Elapsed, Is.EqualTo(30f));
            Assert.That(run.State, Is.EqualTo(RocketStudySimulation.RunState.Finished));
            Assert.That(callbacks, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Cancel_HasNoCompletedScoreOrDuplicateCallback(bool running)
        {
            var run = NewRun();
            int callbacks = 0;
            run.Ended += result => { callbacks++; Assert.That(result.Completed, Is.False); };
            if (running) { run.Begin(); run.Advance(0.2f, false); }
            run.Cancel();
            run.Cancel();
            run.Begin();
            run.Advance(30f, true);
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(run.State, Is.EqualTo(RocketStudySimulation.RunState.Cancelled));
            Assert.That(run.Obstacles, Is.Empty);
        }

        [TestCase(190f)]
        [TestCase(-190f)]
        public void ObstacleCollision_CannotTunnelAcrossOneLongFrame(float y)
        {
            var run = new RocketStudySimulation(new RocketStudySettings { gravity = 0f, obstacleSpeed = 400f }, 1);
            typeof(RocketStudySimulation).GetProperty(nameof(RocketStudySimulation.RocketY)).SetValue(run, y);
            run.Begin();
            run.Advance(30f, false);
            Assert.That(run.State, Is.EqualTo(RocketStudySimulation.RunState.Finished));
            Assert.That(run.Elapsed, Is.InRange(2f, 4f));
            Assert.That(run.Score, Is.LessThan(20));
        }

        [Test]
        public void Obstacles_AreBoundedReclaimedAndHaveOverlappingSafeGaps()
        {
            var run = new RocketStudySimulation(new RocketStudySettings { gravity = 0f }, 24);
            run.Begin();
            int firstId = -1;
            for (int frame = 0; frame < 30 * 60; frame++)
            {
                run.Advance(1f / 60f, false);
                if (run.Obstacles.Count > 0 && firstId < 0) firstId = run.Obstacles[0].Id;
                Assert.That(run.Obstacles.Count, Is.LessThanOrEqualTo(4));
                float? previous = null;
                foreach (var obstacle in run.Obstacles)
                {
                    Assert.That(obstacle.Bottom(run.GapSize).height, Is.GreaterThanOrEqualTo(36f));
                    Assert.That(obstacle.Top(run.GapSize).height, Is.GreaterThanOrEqualTo(36f));
                    if (previous.HasValue) Assert.That(Mathf.Abs(obstacle.GapCenter - previous.Value), Is.LessThanOrEqualTo(80.01f));
                    previous = obstacle.GapCenter;
                }
            }
            Assert.That(run.Obstacles.All(o => o.Id != firstId), Is.True);
            Assert.That(run.Score, Is.EqualTo(100));
        }

        [Test]
        public void DefaultTuning_AllowsSimpleKeyboardThrustToSurviveAcrossSeeds()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                var run = new RocketStudySimulation(new RocketStudySettings(), seed);
                run.Begin();
                for (int frame = 0; frame < 1801 && run.State == RocketStudySimulation.RunState.Running; frame++)
                    run.Advance(1f / 60f, run.RocketY + run.VerticalVelocity * 0.3f < 0f);
                Assert.That(run.Score, Is.EqualTo(100), $"Seed {seed}");
            }
        }

        [TestCase(280f, 36f)]
        [TestCase(102f, 36f)]
        [TestCase(520f, 36f)]
        [TestCase(280f, 0f)]
        [TestCase(280f, 1000f)]
        public void GeneratedGaps_RespectSharedBoundsAndMinimumTowers(float gap, float padding)
        {
            MethodInfo spawn = typeof(RocketStudySimulation).GetMethod("Spawn", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo previousGap = typeof(RocketStudySimulation).GetField("previousGap", BindingFlags.NonPublic | BindingFlags.Instance);
            for (int seed = 0; seed < 40; seed++)
            {
                var run = new RocketStudySimulation(new RocketStudySettings { gapSize = gap, verticalPadding = padding }, seed);
                // Invoke generation directly to cover long sequences independently of player deaths.
                for (int i = 0; i < 100; i++)
                {
                    // Force both clamped extremes as well as ordinary seeded random walks.
                    if (i == 1 || i == 2) previousGap.SetValue(run, i == 1 ? float.MinValue : float.MaxValue);
                    spawn.Invoke(run, null);
                    var obstacle = run.Obstacles.Last();
                    Rect bottom = obstacle.Bottom(run.GapSize), top = obstacle.Top(run.GapSize);
                    Assert.That(obstacle.GapCenter, Is.InRange(run.MinGapCenter, run.MaxGapCenter));
                    if (i == 1) Assert.That(obstacle.GapCenter, Is.EqualTo(run.MinGapCenter));
                    if (i == 2) Assert.That(obstacle.GapCenter, Is.EqualTo(run.MaxGapCenter));
                    Assert.That(bottom.yMin, Is.EqualTo(RocketStudySimulation.PlayAreaBottom).Within(0.001f));
                    Assert.That(top.yMax, Is.EqualTo(RocketStudySimulation.PlayAreaTop).Within(0.001f));
                    Assert.That(bottom.height, Is.GreaterThanOrEqualTo(run.MinimumTowerHeight - 0.001f));
                    Assert.That(top.height, Is.GreaterThanOrEqualTo(run.MinimumTowerHeight - 0.001f));
                    Assert.That(top.yMin - bottom.yMax, Is.EqualTo(run.GapSize).Within(0.001f));
                }
            }
        }

        [Test]
        public void SharedBounds_PreserveExistingSeededGapDifficulty()
        {
            for (int seed = 0; seed < 40; seed++)
            {
                var settings = new RocketStudySettings();
                var run = new RocketStudySimulation(settings, seed);
                var random = new System.Random(seed);
                float previous = 0f;
                float oldLimit = (RocketStudySimulation.Height - settings.gapSize) / 2f - settings.verticalPadding;
                for (int i = 0; i < 100; i++)
                {
                    float expected = i == 0 ? 0f : Mathf.Clamp(previous + ((float)random.NextDouble() * 2f - 1f) * 80f,
                        -oldLimit, oldLimit);
                    typeof(RocketStudySimulation).GetMethod("Spawn", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(run, null);
                    Assert.That(run.Obstacles.Last().GapCenter, Is.EqualTo(expected));
                    previous = expected;
                }
            }
        }

        [TestCase(0f)]
        [TestCase(0.5f)]
        [TestCase(32.5f)]
        [TestCase(96.5f)]
        [TestCase(244f)]
        [TestCase(458f)]
        public void Books_FillTowerInCorrectDirectionAndKeepEveryDetailInside(float height)
        {
            var host = new GameObject("Book bounds test", typeof(RectTransform));
            try
            {
                foreach (bool top in new[] { false, true })
                {
                    float min = top ? RocketStudySimulation.PlayAreaTop - height : RocketStudySimulation.PlayAreaBottom;
                    var bounds = new Rect(-36f, min, 72f, height);
                    typeof(LibraryRocketGameController).GetMethod("CreateBookStack", BindingFlags.NonPublic | BindingFlags.Static)
                        .Invoke(null, new object[] { host.transform, "Stack", bounds, top, 17 });
                    var stack = (RectTransform)host.transform.GetChild(host.transform.childCount - 1);
                    AssertRectEqual(BoundsIn(stack, host.transform), bounds);
                    Assert.That(stack.childCount, Is.EqualTo(Mathf.CeilToInt(height / 24f)));
                    float cursor = top ? bounds.yMax : bounds.yMin;
                    foreach (RectTransform book in stack)
                    {
                        Rect bookBounds = BoundsIn(book, host.transform);
                        AssertInside(bookBounds, bounds);
                        Assert.That(bookBounds.width, Is.GreaterThanOrEqualTo(bounds.width - 4f));
                        Assert.That(top ? bookBounds.yMax : bookBounds.yMin, Is.EqualTo(cursor).Within(0.001f));
                        cursor = top ? bookBounds.yMin : bookBounds.yMax;
                        Assert.That(book.Find("Pages"), Is.Not.Null);
                        Assert.That(book.Find("Spine"), Is.Not.Null);
                        foreach (RectTransform detail in book)
                            AssertInside(BoundsIn(detail, host.transform), bookBounds);
                    }
                    Assert.That(cursor, Is.EqualTo(top ? bounds.yMin : bounds.yMax).Within(0.001f));
                }
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void Controller_AlignsTowersWithSimulationAndKeepsReadoutOutsidePlayfield()
        {
            var host = new GameObject("Visual alignment test");
            try
            {
                var controller = host.AddComponent<LibraryRocketGameController>();
                typeof(LibraryRocketGameController).GetField("settings", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(controller, new RocketStudySettings { gravity = 0f });
                controller.Open(_ => { });
                var run = controller.Simulation;
                var area = host.GetComponentsInChildren<RectTransform>().Single(t => t.name == "PlayArea");
                var hud = host.GetComponentsInChildren<RectTransform>().Single(t => t.name == "Readout");
                Assert.That(area.rect.yMin, Is.EqualTo(RocketStudySimulation.PlayAreaBottom));
                Assert.That(area.rect.yMax, Is.EqualTo(RocketStudySimulation.PlayAreaTop));
                Assert.That(BoundsIn(hud, area).Overlaps(area.rect), Is.False, "HUD must not conceal the actual top boundary.");
                run.Begin();
                for (int i = 0; i < 30; i++)
                {
                    run.Advance(1f, false);
                    Render(controller);
                    var pairs = area.Find("Obstacles").Cast<Transform>().ToArray();
                    Assert.That(pairs.Length, Is.EqualTo(run.Obstacles.Count));
                    for (int j = 0; j < pairs.Length; j++)
                    {
                        var bottom = (RectTransform)pairs[j].Find("Bottom book stack");
                        var top = (RectTransform)pairs[j].Find("Top book stack");
                        Rect bottomBounds = run.Obstacles[j].Bottom(run.GapSize), topBounds = run.Obstacles[j].Top(run.GapSize);
                        AssertRectEqual(BoundsIn(bottom, area), bottomBounds);
                        AssertRectEqual(BoundsIn(top, area), topBounds);
                        foreach (RectTransform visual in bottom.GetComponentsInChildren<RectTransform>())
                            AssertInside(BoundsIn(visual, area), bottomBounds);
                        foreach (RectTransform visual in top.GetComponentsInChildren<RectTransform>())
                            AssertInside(BoundsIn(visual, area), topBounds);
                    }
                }
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void Controller_RemovesAllBookDescendantsOnExpiryAndCancellation()
        {
            var host = new GameObject("Book cleanup test");
            try
            {
                var controller = host.AddComponent<LibraryRocketGameController>();
                typeof(LibraryRocketGameController).GetField("settings", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(controller, new RocketStudySettings { gravity = 0f });
                controller.Open(_ => { });
                controller.Simulation.Begin();
                controller.Simulation.Advance(1.1f, false);
                Render(controller);
                var firstPair = host.GetComponentsInChildren<RectTransform>().First(t => t.name == "Study Distractions");
                var descendants = firstPair.GetComponentsInChildren<RectTransform>();
                Assert.That(descendants.Count(t => t.name == "Book"), Is.GreaterThan(0));
                controller.Simulation.Advance(7f, false);
                Render(controller);
                Assert.That(descendants.All(t => t == null), Is.True);
                Assert.That(controller.Simulation.Obstacles.All(o => o.Id != 0), Is.True);
                controller.Simulation.Cancel();
                Assert.That(host.GetComponentsInChildren<RectTransform>().Any(t => t.name == "Book"), Is.False);
                Assert.That(controller.Simulation.Obstacles, Is.Empty);
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void BookVariation_IsDeterministicAndUsesMultipleCoverStyles()
        {
            var host = new GameObject("Book variation test", typeof(RectTransform));
            try
            {
                var create = typeof(LibraryRocketGameController).GetMethod("CreateBookStack", BindingFlags.NonPublic | BindingFlags.Static);
                for (int i = 0; i < 2; i++)
                    create.Invoke(null, new object[] { host.transform, "Stack", new Rect(-36f, -280f, 72f, 244f), false, 17 });
                var first = host.transform.GetChild(0).GetComponentsInChildren<Image>();
                var second = host.transform.GetChild(1).GetComponentsInChildren<Image>();
                Assert.That(first.Length, Is.EqualTo(second.Length));
                for (int i = 0; i < first.Length; i++)
                {
                    Assert.That(first[i].color, Is.EqualTo(second[i].color));
                    AssertRectEqual(BoundsIn(first[i].rectTransform, host.transform), BoundsIn(second[i].rectTransform, host.transform));
                }
                var books = first.Where(b => b.name == "Book").ToArray();
                Assert.That(books.Select(b => b.color).Distinct().Count(), Is.InRange(3, 4));
                Assert.That(books.Select(b => b.rectTransform.rect.height).Distinct().Count(), Is.GreaterThan(1));
                Assert.That(books.Select(b => b.rectTransform.rect.width).Distinct().Count(), Is.GreaterThan(1));
            }
            finally { Object.DestroyImmediate(host); }
        }

        private static void Render(LibraryRocketGameController controller) =>
            typeof(LibraryRocketGameController).GetMethod("Render", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(controller, null);

        private static Rect BoundsIn(RectTransform rect, Transform space)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3 min = space.InverseTransformPoint(corners[0]);
            Vector3 max = space.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static void AssertRectEqual(Rect actual, Rect expected)
        {
            Assert.That(actual.xMin, Is.EqualTo(expected.xMin).Within(0.001f));
            Assert.That(actual.xMax, Is.EqualTo(expected.xMax).Within(0.001f));
            Assert.That(actual.yMin, Is.EqualTo(expected.yMin).Within(0.001f));
            Assert.That(actual.yMax, Is.EqualTo(expected.yMax).Within(0.001f));
        }

        private static void AssertInside(Rect inner, Rect outer)
        {
            Assert.That(inner.xMin, Is.GreaterThanOrEqualTo(outer.xMin - 0.001f));
            Assert.That(inner.xMax, Is.LessThanOrEqualTo(outer.xMax + 0.001f));
            Assert.That(inner.yMin, Is.GreaterThanOrEqualTo(outer.yMin - 0.001f));
            Assert.That(inner.yMax, Is.LessThanOrEqualTo(outer.yMax + 0.001f));
        }

        [Test]
        public void Controller_CleansObstacleViewsAndCreatesNoCameraPlayerOrEventSystem()
        {
            GameObject host = new GameObject("Rocket controller test");
            int eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length;
            try
            {
                var controller = host.AddComponent<LibraryRocketGameController>();
                typeof(LibraryRocketGameController).GetField("settings", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(controller, new RocketStudySettings { gravity = 0f });
                controller.Open(_ => { });
                var run = controller.Simulation;
                run.Begin();
                for (int i = 0; i < 30; i++)
                {
                    run.Advance(1f, false);
                    typeof(LibraryRocketGameController).GetMethod("Render", BindingFlags.NonPublic | BindingFlags.Instance)
                        .Invoke(controller, null);
                    Assert.That(host.GetComponentsInChildren<RectTransform>().Count(t => t.name == "Study Distractions"),
                        Is.EqualTo(run.Obstacles.Count));
                }
                Assert.That(host.GetComponentsInChildren<Canvas>().Length, Is.EqualTo(1));
                Assert.That(host.GetComponentsInChildren<Camera>(), Is.Empty);
                Assert.That(host.GetComponentsInChildren<AudioListener>(), Is.Empty);
                Assert.That(host.GetComponentsInChildren<PlayerMovement>(), Is.Empty);
                Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length, Is.EqualTo(eventSystems));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void Scene_IsEnabledAndContainsOnlyController()
        {
            Assert.That(EditorBuildSettings.scenes.Any(s => s.path == RocketLibraryStudyMinigame.ScenePath && s.enabled), Is.True);
            GameObject host = new GameObject("Rocket adapter test");
            Scene scene = default;
            try
            {
                var adapter = host.AddComponent<RocketLibraryStudyMinigame>();
                // Availability is exercised in Play Mode below; EditMode does not run Awake.
                adapter.enabled = false;
                Assert.That(adapter.IsAvailable, Is.False);
                adapter.enabled = true;
                scene = EditorSceneManager.OpenScene(RocketLibraryStudyMinigame.ScenePath, OpenSceneMode.Additive);
                Assert.That(adapter.IsAvailable, Is.False, "Never duplicate a loaded minigame scene.");
                Assert.That(scene.GetRootGameObjects().Length, Is.EqualTo(1));
                Assert.That(scene.GetRootGameObjects()[0].GetComponent<LibraryRocketGameController>(), Is.Not.Null);
                EditorSceneManager.CloseScene(scene, true);
                scene = default;
                typeof(RocketLibraryStudyMinigame).GetField("scenePath", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(adapter, "Assets/Scenes/MissingRocket.unity");
                Assert.That(adapter.IsAvailable, Is.False);
            }
            finally
            {
                if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
                Object.DestroyImmediate(host);
            }
        }

        private static RocketStudySimulation NewRun() => new RocketStudySimulation(new RocketStudySettings(), 1);
    }
}
