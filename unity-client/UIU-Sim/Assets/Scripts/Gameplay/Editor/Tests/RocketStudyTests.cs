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
