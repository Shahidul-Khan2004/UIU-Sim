using System;
using System.Collections;
using UnityEngine.TestTools;
using System.Text;
using Object = UnityEngine.Object;
using NUnit.Framework;
using TMPro;
using UIU.Simulator.Gameplay.Activities;
using UIU.Simulator.Gameplay.Library;
using UIU.Simulator.Gameplay.Library.RocketStudy;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UIU.Simulator.Gameplay.Player;
using UIU.Simulator.Gameplay.UI;
using UIU.Simulator.UI;
using UnityEngine;
using UnityEngine.UI;

namespace UIU.Simulator.Gameplay.Editor.Tests
{
    [TestFixture]
    public sealed class LibraryStudyTests
    {
        private GameObject playerObject;
        private DailyActivityState dailyActivityState;
        private PlayerSaveState saveState;
        private PlayerStats playerStats;
        private StatsHUD statsHud;
        private PlayerMovement playerMovement;
        private FirstPersonLook firstPersonLook;
        private CameraFollow cameraFollow;
        private InteractionController interactionController;
        private GameObject stationObject;
        private LibraryStudyInteractable station;
        private GameObject summaryObject;
        private DailySummaryUI summaryUi;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // These integration tests require MonoBehaviour Awake/OnEnable and real modal ownership.
            yield return new EnterPlayMode();
            if (PlayerSaveState.Instance != null)
            {
                Object.DestroyImmediate(PlayerSaveState.Instance.gameObject);
            }

            if (LibraryStudyUI.Instance != null)
            {
                Object.DestroyImmediate(LibraryStudyUI.Instance.gameObject);
            }

            if (DailySummaryUI.Instance != null)
            {
                Object.DestroyImmediate(DailySummaryUI.Instance.gameObject);
            }

            playerObject = new GameObject("LibraryStudyPlayer");
            playerObject.AddComponent<CharacterController>();
            playerStats = playerObject.AddComponent<PlayerStats>();
            dailyActivityState = playerObject.AddComponent<DailyActivityState>();
            statsHud = playerObject.AddComponent<StatsHUD>();
            playerMovement = playerObject.AddComponent<PlayerMovement>();
            firstPersonLook = playerObject.AddComponent<FirstPersonLook>();
            interactionController = playerObject.AddComponent<InteractionController>();
            saveState = playerObject.AddComponent<PlayerSaveState>();
            saveState.enabled = false; // No live hydration/network requests in this fixture.
            saveState.SetDayProgressForTesting(1, 1);
            playerStats.ApplyServerState(50f, 50f, StatUpdateSource.InitialHydration);

            GameObject cameraObject = new GameObject("FirstPersonCamera");
            cameraObject.transform.SetParent(playerObject.transform, false);
            cameraObject.AddComponent<Camera>();
            cameraFollow = cameraObject.AddComponent<CameraFollow>();

            stationObject = new GameObject("LibraryStudyStation");
            stationObject.AddComponent<BoxCollider>();
            station = stationObject.AddComponent<LibraryStudyInteractable>();

            summaryObject = new GameObject("LibrarySummary");
            summaryUi = summaryObject.AddComponent<DailySummaryUI>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (LibraryStudyUI.Instance != null)
            {
                LibraryStudyUI.Instance.Hide(restoreGameplay: true);
                Object.DestroyImmediate(LibraryStudyUI.Instance.gameObject);
            }

            SystemNotificationUI notification = Object.FindFirstObjectByType<SystemNotificationUI>();
            if (notification != null)
            {
                Object.DestroyImmediate(notification.gameObject);
            }

            if (summaryObject != null)
            {
                Object.DestroyImmediate(summaryObject);
            }

            if (stationObject != null)
            {
                Object.DestroyImmediate(stationObject);
            }

            if (playerObject != null)
            {
                Object.DestroyImmediate(playerObject);
            }
            yield return new ExitPlayMode();
        }

        [Test]
        public void Interact_OpensCompactModal()
        {
            string response = station.Interact();

            Assert.That(response, Is.Null);
            Assert.That(LibraryStudyUI.IsOpen, Is.True);
            Assert.That(playerMovement.enabled, Is.False);
            Assert.That(firstPersonLook.enabled, Is.False);
            Assert.That(interactionController.enabled, Is.False);
            Assert.That(Cursor.visible, Is.True);

            TextMeshProUGUI title = FindLabel(LibraryStudyUI.Instance.transform, "Title");
            TextMeshProUGUI prompt = FindLabel(LibraryStudyUI.Instance.transform, "Prompt");
            TextMeshProUGUI limit = FindLabel(LibraryStudyUI.Instance.transform, "Limit");
            Assert.That(title.text, Is.EqualTo("LIBRARY STUDY"));
            Assert.That(prompt.text, Is.EqualTo("Spend some time studying?"));
            Assert.That(limit.text, Is.EqualTo("You can study once per day."));
        }

        [Test]
        public void Back_RestoresGameplay()
        {
            station.Interact();
            Button back = FindButton(LibraryStudyUI.Instance.transform, "BackButton");
            back.onClick.Invoke();

            Assert.That(LibraryStudyUI.IsOpen, Is.False);
            Assert.That(playerMovement.enabled, Is.True);
            Assert.That(firstPersonLook.enabled, Is.True);
            Assert.That(interactionController.enabled, Is.True);
            Assert.That(Cursor.visible, Is.False);
            // A batch -nographics editor has no window to lock; verify actual lock in manual Play Mode.
            if (!Application.isBatchMode) Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked));
        }

        [Test]
        public void CompletedStudy_ShowsStudiedEnoughMessage()
        {
            dailyActivityState.SetLibraryStudyStatusForTesting(ActivityStatus.Completed, "COMPLETED", 3);

            string response = station.Interact();

            Assert.That(response, Is.EqualTo("You have studied enough for today."));
            Assert.That(LibraryStudyUI.IsOpen, Is.False);
            Assert.That(playerStats.AcademicReputation, Is.EqualTo(50f));
        }

        [Test]
        public void Hud_DoesNotListLibraryStudyAsObjective()
        {
            dailyActivityState.SetGetIdCardStatusForTesting(ActivityStatus.Completed, "COMPLETED");
            dailyActivityState.SetLibraryStudyStatusForTesting(ActivityStatus.Completed, "COMPLETED", 3);
            statsHud.enabled = false;
            statsHud.enabled = true;

            string hud = ConcatenateLabels(statsHud.transform);
            Assert.That(hud, Does.Not.Contain("Library Study"));
            Assert.That(hud, Does.Not.Contain("Study at Library"));
            Assert.That(hud, Does.Contain("Have Breakfast"));
        }

        [Test]
        public void UnavailableAdapter_DoesNotConsumeAttempt()
        {
            var sync = new RecordingLibrarySync(playerStats, dailyActivityState);
            station.SetMinigameForTesting(new ScriptedLibraryStudyMinigame { IsAvailable = false, Score = 100 });
            station.SetProgressSyncForTesting(sync);

            station.Interact();
            FindButton(LibraryStudyUI.Instance.transform, "StartStudyButton").onClick.Invoke();

            Assert.That(sync.StartCalled, Is.False);
            Assert.That(sync.CompletedScore, Is.EqualTo(-1));
            Assert.That(dailyActivityState.LibraryStudyStatus, Is.EqualTo(ActivityStatus.Pending));
            Assert.That(playerStats.AcademicReputation, Is.EqualTo(50f));
            Assert.That(playerMovement.enabled, Is.True);
            Assert.That(LibraryStudyUI.IsOpen, Is.False);
        }

        [Test]
        public void ScriptedScores_SubmitNormalizedScoreWithoutLocalReward()
        {
            int[] scores = { 0, 25, 50, 75, 90, 100 };
            for (int i = 0; i < scores.Length; i++)
            {
                var sync = new RecordingLibrarySync(playerStats, dailyActivityState)
                {
                    ApplyConfirmedReputation = false
                };
                station.SetMinigameForTesting(new ScriptedLibraryStudyMinigame { IsAvailable = true, Score = scores[i] });
                station.SetProgressSyncForTesting(sync);

                station.Interact();
                FindButton(LibraryStudyUI.Instance.transform, "StartStudyButton").onClick.Invoke();

                Assert.That(sync.StartCalled, Is.True);
                Assert.That(sync.CompletedScore, Is.EqualTo(scores[i]));
                Assert.That(playerStats.AcademicReputation, Is.EqualTo(50f));
                Assert.That(playerMovement.enabled, Is.True);
            }
        }

        [Test]
        public void ConfirmedServerResult_RefreshesReputationAndCompletedState()
        {
            var sync = new PlayerProgressSyncHarness(playerObject, playerStats, dailyActivityState);
            station.SetMinigameForTesting(new ScriptedLibraryStudyMinigame { IsAvailable = true, Score = 75 });
            station.SetProgressSyncForTesting(sync);

            station.Interact();
            FindButton(LibraryStudyUI.Instance.transform, "StartStudyButton").onClick.Invoke();

            Assert.That(sync.SubmittedScore, Is.EqualTo(75));
            Assert.That(playerStats.AcademicReputation, Is.EqualTo(53f));
            Assert.That(playerStats.Aura, Is.EqualTo(50f));
            Assert.That(dailyActivityState.IsLibraryStudyCompleted, Is.True);
            Assert.That(dailyActivityState.LibraryStudyReputationDelta, Is.EqualTo(3));
        }

        [Test]
        public void FailedComplete_DoesNotGrantLocalReputation()
        {
            var sync = new RecordingLibrarySync(playerStats, dailyActivityState) { FailComplete = true };
            station.SetMinigameForTesting(new ScriptedLibraryStudyMinigame { IsAvailable = true, Score = 100 });
            station.SetProgressSyncForTesting(sync);

            station.Interact();
            FindButton(LibraryStudyUI.Instance.transform, "StartStudyButton").onClick.Invoke();

            Assert.That(sync.CompletedScore, Is.EqualTo(100));
            Assert.That(playerStats.AcademicReputation, Is.EqualTo(50f));
            Assert.That(playerStats.Aura, Is.EqualTo(50f));
            Assert.That(dailyActivityState.IsLibraryStudyCompleted, Is.False);
            Assert.That(playerMovement.enabled, Is.True);
        }

        [Test]
        public void Summary_ShowsCompletedStudyInGreen_AndOmitsUnfinished()
        {
            DaySummaryActivity[] activities =
            {
                new DaySummaryActivity(ActivityIds.Breakfast, ActivityStatus.Completed, "RICE", 3, 0),
                new DaySummaryActivity(ActivityIds.LibraryStudy, ActivityStatus.InProgress, "STARTED", 0, 0),
                new DaySummaryActivity(ActivityIds.LibraryStudy, ActivityStatus.Completed, "COMPLETED", 0, 3)
            };
            summaryUi.ShowForTesting(new DayFinalizeResult(1, 1, activities, 3, 3, 53, 53));

            Assert.That(FindChild(summaryUi.transform, "ActivityRow_2"), Is.Null);
            TextMeshProUGUI studyTitle = FindLabel(FindChild(summaryUi.transform, "ActivityRow_1"), "Title");
            TextMeshProUGUI studyDetails = FindLabel(FindChild(summaryUi.transform, "ActivityRow_1"), "Details");
            Assert.That(studyTitle.text, Does.Contain("Library Study"));
            Assert.That(studyTitle.color, Is.EqualTo(UiTheme.Success));
            Assert.That(studyTitle.color, Is.Not.EqualTo(UiTheme.Danger));
            Assert.That(studyDetails.text, Does.Contain("Academic: +3"));
            Assert.That(summaryUi.GetActivityBodyTextForTesting(), Does.Not.Contain("STARTED"));
        }

        [Test]
        public void NextDayAndNewGame_ClearLibraryStudy()
        {
            dailyActivityState.SetLibraryStudyStatusForTesting(ActivityStatus.Completed, "COMPLETED", 4);
            dailyActivityState.ResetForNewDay(2);
            Assert.That(dailyActivityState.IsLibraryStudyCompleted, Is.False);
            Assert.That(dailyActivityState.LibraryStudyStatus, Is.EqualTo(ActivityStatus.Pending));

            dailyActivityState.SetLibraryStudyStatusForTesting(ActivityStatus.Completed, "COMPLETED", 4);
            dailyActivityState.ResetForNewGame();
            Assert.That(dailyActivityState.LibraryStudyStatus, Is.EqualTo(ActivityStatus.Pending));
        }

        [Test]
        public void MissingAdapter_DoesNotCallStart()
        {
            var sync = new RecordingLibrarySync(playerStats, dailyActivityState);
            station.SetProgressSyncForTesting(sync);
            station.Interact();
            FindButton(LibraryStudyUI.Instance.transform, "StartStudyButton").onClick.Invoke();
            Assert.That(sync.StartCalls, Is.Zero);
            Assert.That(playerMovement.enabled, Is.True);
        }

        [Test]
        public void Cancel_DoesNotCompleteOrReward_AndCanLaunchAgain()
        {
            var sync = new RecordingLibrarySync(playerStats, dailyActivityState);
            station.SetProgressSyncForTesting(sync);
            station.SetMinigameForTesting(new ScriptedLibraryStudyMinigame { Cancel = true });
            for (int i = 0; i < 2; i++)
            {
                station.Interact();
                FindButton(LibraryStudyUI.Instance.transform, "StartStudyButton").onClick.Invoke();
                Assert.That(playerMovement.enabled, Is.True);
                Assert.That(cameraFollow.enabled, Is.True);
                Assert.That(LibraryStudyUI.BlocksGameplay, Is.False);
            }
            Assert.That(sync.StartCalls, Is.EqualTo(2));
            Assert.That(sync.CompleteCalls, Is.Zero);
            Assert.That(playerStats.AcademicReputation, Is.EqualTo(50f));
            Assert.That(dailyActivityState.IsLibraryStudyCompleted, Is.False);
        }

        [Test]
        public void FailedSave_RetainsExactScore_RetryDoesNotStartOrReplay()
        {
            var sync = new RecordingLibrarySync(playerStats, dailyActivityState) { FailComplete = true };
            var game = new DeferredMinigame();
            station.SetProgressSyncForTesting(sync);
            station.SetMinigameForTesting(game);
            station.Interact();
            FindButton(LibraryStudyUI.Instance.transform, "StartStudyButton").onClick.Invoke();
            game.Finish(LibraryStudyMinigameResult.Finished(73));
            Assert.That(playerMovement.enabled, Is.True);
            Assert.That(dailyActivityState.IsLibraryStudyCompleted, Is.False);
            station.Interact();
            Assert.That(FindLabel(LibraryStudyUI.Instance.transform, "Prompt").text, Does.Contain("73/100"));
            Assert.That(FindButton(LibraryStudyUI.Instance.transform, "StartStudyButton").GetComponentInChildren<TextMeshProUGUI>().text,
                Is.EqualTo("RETRY SAVE"));
            sync.FailComplete = false;
            FindButton(LibraryStudyUI.Instance.transform, "StartStudyButton").onClick.Invoke();
            Assert.That(sync.CompletedScore, Is.EqualTo(73));
            Assert.That(sync.StartCalls, Is.EqualTo(1));
            Assert.That(sync.CompleteCalls, Is.EqualTo(2));
            Assert.That(game.Launches, Is.EqualTo(1));
        }

        [TestCase(0, 0)]
        [TestCase(100, 5)]
        public void StartThenLaunch_SaveThenCleanup_RestoresAllControlsAndUsesServerReward(int score, int reward)
        {
            var sync = new DeferredLibrarySync(playerObject);
            var game = new DeferredMinigame { DeferClose = true };
            station.SetProgressSyncForTesting(sync);
            station.SetMinigameForTesting(game);
            station.Interact();
            FindButton(LibraryStudyUI.Instance.transform, "StartStudyButton").onClick.Invoke();
            Assert.That(game.Launches, Is.Zero, "Wait for START before opening the adapter.");
            Assert.That(LibraryStudyUI.IsOpen, Is.False);
            Assert.That(LibraryStudyUI.BlocksGameplay, Is.True);
            sync.ConfirmStart();
            Assert.That(game.Launches, Is.EqualTo(1));
            game.Finish(LibraryStudyMinigameResult.Finished(score));
            game.Finish(LibraryStudyMinigameResult.Finished(37)); // Misbehaving adapter duplicate is ignored.
            Assert.That(sync.Score, Is.EqualTo(score));
            Assert.That(sync.CompleteCalls, Is.EqualTo(1));
            Assert.That(playerStats.AcademicReputation, Is.EqualTo(50f));
            Assert.That(dailyActivityState.IsLibraryStudyCompleted, Is.False);
            Assert.That(playerMovement.enabled, Is.False);
            Assert.That(game.CloseCallback, Is.Null, "Scene stays up while COMPLETE is pending.");
            sync.ConfirmComplete(score, reward);
            Assert.That(playerStats.AcademicReputation, Is.EqualTo(50f + reward));
            Assert.That(playerStats.Aura, Is.EqualTo(50f));
            Assert.That(dailyActivityState.IsLibraryStudyCompleted, Is.True);
            Assert.That(game.CloseCallback, Is.Not.Null);
            Assert.That(playerMovement.enabled, Is.False, "Do not restore until unload completes.");
            game.CloseCallback();
            Assert.That(playerMovement.enabled && firstPersonLook.enabled && cameraFollow.enabled && interactionController.enabled, Is.True);
            // A batch -nographics editor has no window to lock; verify actual lock in manual Play Mode.
            if (!Application.isBatchMode) Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked));
            Assert.That(Cursor.visible, Is.False);
            Assert.That(station.Interact(), Is.EqualTo(LibraryStudyUI.StudiedEnoughMessage));
            Assert.That(sync.StartCalls, Is.EqualTo(1));
            Assert.That(game.Launches, Is.EqualTo(1));
            string notification = ConcatenateLabels(SystemNotificationUI.Instance.transform);
            Assert.That(notification, Does.Contain($"Score: {score}/100"));
            Assert.That(notification, Does.Contain($"Academic Reputation +{reward}"));
        }

        [Test]
        public void Menu_IsBlockedAcrossHiddenModalAndMinigame()
        {
            GameMenuManager menu = GameMenuManager.EnsureExists();
            try
            {
                var game = new DeferredMinigame();
                station.SetMinigameForTesting(game);
                station.SetProgressSyncForTesting(new RecordingLibrarySync(playerStats, dailyActivityState));
                station.Interact();
                menu.HandleEscape();
                Assert.That(GameMenuManager.IsOpen, Is.False);
                FindButton(LibraryStudyUI.Instance.transform, "StartStudyButton").onClick.Invoke();
                menu.HandleEscape();
                menu.Open();
                Assert.That(GameMenuManager.IsOpen, Is.False);
                Assert.That(playerMovement.enabled, Is.False);
                game.Finish(LibraryStudyMinigameResult.Cancelled());
                Assert.That(playerMovement.enabled, Is.True);
            }
            finally { Object.DestroyImmediate(menu.gameObject); }
        }

        [Test]
        public void FailedScore_IsDiscardedOnNextDay()
        {
            var sync = new RecordingLibrarySync(playerStats, dailyActivityState) { FailComplete = true };
            station.SetProgressSyncForTesting(sync);
            station.SetMinigameForTesting(new ScriptedLibraryStudyMinigame { Score = 73 });
            station.Interact();
            FindButton(LibraryStudyUI.Instance.transform, "StartStudyButton").onClick.Invoke();
            dailyActivityState.ResetForNewDay(2);
            saveState.SetDayProgressForTesting(1, 2);
            station.Interact();
            Assert.That(FindLabel(LibraryStudyUI.Instance.transform, "Prompt").text, Is.EqualTo("Spend some time studying?"));
            FindButton(LibraryStudyUI.Instance.transform, "StartStudyButton").onClick.Invoke();
            Assert.That(sync.StartCalls, Is.EqualTo(2));
        }

        [Test]
        public void StationDisabledDuringStart_IgnoresLateResponseAndRestoresControls()
        {
            var sync = new DeferredLibrarySync(playerObject);
            var game = new DeferredMinigame();
            station.SetProgressSyncForTesting(sync);
            station.SetMinigameForTesting(game);
            station.Interact();
            FindButton(LibraryStudyUI.Instance.transform, "StartStudyButton").onClick.Invoke();
            station.enabled = false;
            sync.ConfirmStart();
            Assert.That(game.Launches, Is.Zero);
            Assert.That(playerMovement.enabled, Is.True);
            Assert.That(LibraryStudyUI.BlocksGameplay, Is.False);
        }

        [UnityTest]
        public IEnumerator RealAdapter_FullRun_WaitsForServerThenUnloadsOnlyRocketScene()
        {
            Scene campus = SceneManager.GetActiveScene();
            var adapter = stationObject.AddComponent<RocketLibraryStudyMinigame>();
            Assert.That(adapter.IsAvailable, Is.True);
            var sync = new DeferredLibrarySync(playerObject);
            station.SetProgressSyncForTesting(sync);
            station.SetMinigameForTesting(adapter);
            station.Interact();
            FindButton(LibraryStudyUI.Instance.transform, "StartStudyButton").onClick.Invoke();
            Assert.That(IsRocketLoaded(), Is.False);
            sync.ConfirmStart();
            yield return WaitUntil(() => Object.FindFirstObjectByType<LibraryRocketGameController>()?.Simulation != null);
            var controller = Object.FindFirstObjectByType<LibraryRocketGameController>();
            Assert.That(controller.Simulation.State, Is.EqualTo(RocketStudySimulation.RunState.Ready));
            Assert.That(adapter.IsAvailable, Is.False);
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(campus));
            var run = controller.Simulation;
            run.Begin();
            for (int i = 0; i < 1801 && run.State == RocketStudySimulation.RunState.Running; i++)
                run.Advance(1f / 60f, run.RocketY + run.VerticalVelocity * 0.3f < 0f);
            Assert.That(sync.Score, Is.EqualTo(100));
            Assert.That(sync.CompleteCalls, Is.EqualTo(1));
            Assert.That(IsRocketLoaded(), Is.True);
            Assert.That(playerStats.AcademicReputation, Is.EqualTo(50f));
            Assert.That(playerMovement.enabled, Is.False);
            sync.ConfirmComplete(100, 5);
            yield return WaitUntil(() => !LibraryStudyUI.BlocksGameplay);
            Assert.That(IsRocketLoaded(), Is.False);
            Assert.That(Object.FindFirstObjectByType<LibraryRocketGameController>(), Is.Null);
            Assert.That(campus.isLoaded, Is.True);
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(campus));
            Assert.That(playerMovement.enabled && firstPersonLook.enabled && cameraFollow.enabled && interactionController.enabled, Is.True);
            Assert.That(playerStats.AcademicReputation, Is.EqualTo(55f));
            Assert.That(station.Interact(), Is.EqualTo(LibraryStudyUI.StudiedEnoughMessage));
            Assert.That(adapter.IsAvailable, Is.True);
        }

        [UnityTest]
        public IEnumerator RealAdapter_EscapeCancelsReady_WithoutOpeningGameMenuOrCompleting()
        {
            var sync = new RecordingLibrarySync(playerStats, dailyActivityState);
            var adapter = stationObject.AddComponent<RocketLibraryStudyMinigame>();
            station.SetProgressSyncForTesting(sync);
            station.SetMinigameForTesting(adapter);
            station.Interact();
            FindButton(LibraryStudyUI.Instance.transform, "StartStudyButton").onClick.Invoke();
            yield return WaitUntil(() => Object.FindFirstObjectByType<LibraryRocketGameController>()?.Simulation != null);
            var controller = Object.FindFirstObjectByType<LibraryRocketGameController>();
            SendKeyboardFrame(controller, Key.Escape);
            Assert.That(controller.Simulation.State, Is.EqualTo(RocketStudySimulation.RunState.Cancelled), "Escape must cancel immediately.");
            yield return WaitUntil(() => !LibraryStudyUI.BlocksGameplay);
            Assert.That(IsRocketLoaded(), Is.False);
            Assert.That(GameMenuManager.IsOpen, Is.False);
            Assert.That(sync.CompleteCalls, Is.Zero);
            Assert.That(dailyActivityState.IsLibraryStudyCompleted, Is.False);
            Assert.That(playerStats.AcademicReputation, Is.EqualTo(50f));
            Assert.That(playerMovement.enabled && firstPersonLook.enabled && cameraFollow.enabled && interactionController.enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator RealAdapter_KeyboardStartsAndThrusts_ThenEscapeCancelsRunning()
        {
            var sync = new RecordingLibrarySync(playerStats, dailyActivityState);
            var adapter = stationObject.AddComponent<RocketLibraryStudyMinigame>();
            station.SetProgressSyncForTesting(sync);
            station.SetMinigameForTesting(adapter);
            station.Interact();
            FindButton(LibraryStudyUI.Instance.transform, "StartStudyButton").onClick.Invoke();
            yield return WaitUntil(() => Object.FindFirstObjectByType<LibraryRocketGameController>()?.Simulation != null);
            var controller = Object.FindFirstObjectByType<LibraryRocketGameController>();
            var run = controller.Simulation;
            SendKeyboardFrame(controller); // Release launch input.
            SendKeyboardFrame(controller, Key.Space);
            Assert.That(run.State, Is.EqualTo(RocketStudySimulation.RunState.Running));
            SendKeyboardFrame(controller, Key.Space);
            Assert.That(run.VerticalVelocity, Is.GreaterThan(0f));
            Assert.That(run.RocketY, Is.GreaterThan(0f));
            SendKeyboardFrame(controller, Key.Escape);
            Assert.That(controller.Simulation.State, Is.EqualTo(RocketStudySimulation.RunState.Cancelled), "Escape must cancel immediately.");
            yield return WaitUntil(() => !LibraryStudyUI.BlocksGameplay);
            Assert.That(IsRocketLoaded(), Is.False);
            Assert.That(sync.CompleteCalls, Is.Zero);
            Assert.That(GameMenuManager.IsOpen, Is.False);
        }

        private static void SendKeyboardFrame(LibraryRocketGameController controller, params Key[] keys)
        {
            // A batch editor has no focused Game view. Pump the actual Input System event and
            // controller Update together so focus resets cannot discard the synthetic key state.
            InputSettings settings = InputSystem.settings;
            var previousBackground = settings.backgroundBehavior;
            var previousEditorInput = settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                // Register edge tracking before the first press on this newly created device.
                // Input System 1.20 documents that its first edge query can miss a fast press.
                _ = keyboard.escapeKey.wasPressedThisFrame;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
                InputSystem.Update();
                keyboard.MakeCurrent();
                if (Array.IndexOf(keys, Key.Escape) >= 0)
                    Assert.That(keyboard.escapeKey.wasPressedThisFrame, Is.True, "Synthetic Escape was not delivered.");
                typeof(LibraryRocketGameController).GetField("openedFrame", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .SetValue(controller, -1);
                GameMenuManager.Instance?.HandleEscape();
                typeof(LibraryRocketGameController).GetMethod("Update", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Invoke(controller, null);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                settings.backgroundBehavior = previousBackground;
                settings.editorInputBehaviorInPlayMode = previousEditorInput;
            }
        }

        [UnityTest]
        public IEnumerator RealAdapter_StationDisabledDuringLoading_CleansUpBeforeReleasingControls()
        {
            var sync = new RecordingLibrarySync(playerStats, dailyActivityState);
            var adapter = stationObject.AddComponent<RocketLibraryStudyMinigame>();
            station.SetProgressSyncForTesting(sync);
            station.SetMinigameForTesting(adapter);
            station.Interact();
            FindButton(LibraryStudyUI.Instance.transform, "StartStudyButton").onClick.Invoke();
            stationObject.SetActive(false);
            yield return WaitUntil(() => !LibraryStudyUI.BlocksGameplay);
            Assert.That(IsRocketLoaded(), Is.False);
            Assert.That(sync.CompleteCalls, Is.Zero);
            Assert.That(playerMovement.enabled && firstPersonLook.enabled && cameraFollow.enabled && interactionController.enabled, Is.True);
        }

        private static bool IsRocketLoaded() => SceneManager.GetSceneByPath(RocketLibraryStudyMinigame.ScenePath).isLoaded;

        private static IEnumerator WaitUntil(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, $"Timed out: rocketLoaded={IsRocketLoaded()}, lock={LibraryStudyUI.BlocksGameplay}, playing={Application.isPlaying}.");
        }

        private sealed class DeferredMinigame : ILibraryStudyMinigame, ILibraryStudyMinigameSession
        {
            public bool IsAvailable => true;
            public int Launches;
            public bool DeferClose;
            public Action CloseCallback;
            private Action<LibraryStudyMinigameResult> callback;
            public void Launch(Action<LibraryStudyMinigameResult> onFinished) { Launches++; callback = onFinished; }
            public void Finish(LibraryStudyMinigameResult result) => callback?.Invoke(result);
            public void Close(Action onClosed)
            {
                if (DeferClose) CloseCallback = onClosed;
                else onClosed?.Invoke();
            }
        }

        private sealed class DeferredLibrarySync : ILibraryStudyProgressSync
        {
            private readonly PlayerProgressSync parser;
            private Action<LibraryStudySessionResult> start, complete;
            public int Score, StartCalls, CompleteCalls;
            public DeferredLibrarySync(GameObject host)
            {
                parser = host.AddComponent<PlayerProgressSync>();
                parser.enabled = false;
            }
            public void RequestLibraryStudyStart(Action<LibraryStudySessionResult> onSuccess, Action onFailure)
            { StartCalls++; start = onSuccess; }
            public void RequestLibraryStudyComplete(int score, Action<LibraryStudySessionResult> onSuccess, Action onFailure)
            { Score = score; CompleteCalls++; complete = onSuccess; }
            public void ConfirmStart() => start(new LibraryStudySessionResult(
                new ActivityRecord(ActivityIds.LibraryStudy, ActivityStatus.InProgress, "STARTED", 0, 0, 1),
                0, 0, false, false, 50, 50));
            public void ConfirmComplete(int score, int reward)
            {
                string json = "{\"activityId\":\"LIBRARY_STUDY\",\"status\":\"COMPLETED\",\"outcome\":\"COMPLETED\"," +
                    $"\"score\":{score},\"auraDelta\":0,\"reputationDelta\":{reward},\"appliedReputationDelta\":{reward}," +
                    $"\"alreadyStarted\":true,\"alreadyCompleted\":false,\"dayNumber\":1,\"aura\":50,\"academicReputation\":{50 + reward}" + "}";
                Assert.That(parser.ApplyLibraryStudyResponseForTesting(json), Is.True);
                complete(new LibraryStudySessionResult(
                    new ActivityRecord(ActivityIds.LibraryStudy, ActivityStatus.Completed, "COMPLETED", 0, reward, 1),
                    score, reward, true, false, 50, 50 + reward));
            }
        }

        private static TextMeshProUGUI FindLabel(Transform root, string name)
        {
            Transform child = FindChild(root, name);
            return child != null ? child.GetComponent<TextMeshProUGUI>() : null;
        }

        private static Button FindButton(Transform root, string name)
        {
            Transform child = FindChild(root, name);
            return child != null ? child.GetComponent<Button>() : null;
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChild(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static string ConcatenateLabels(Transform root)
        {
            TextMeshProUGUI[] labels = root.GetComponentsInChildren<TextMeshProUGUI>(true);
            var builder = new StringBuilder();
            for (int i = 0; i < labels.Length; i++)
            {
                builder.Append(labels[i].text);
                builder.Append('\n');
            }

            return builder.ToString();
        }

        private sealed class RecordingLibrarySync : ILibraryStudyProgressSync
        {
            private readonly PlayerStats stats;
            private readonly DailyActivityState activities;

            public RecordingLibrarySync(PlayerStats stats, DailyActivityState activities)
            {
                this.stats = stats;
                this.activities = activities;
            }

            public bool StartCalled { get; private set; }
            public int StartCalls { get; private set; }
            public int CompleteCalls { get; private set; }
            public int CompletedScore { get; private set; } = -1;
            public bool FailComplete { get; set; }
            public bool ApplyConfirmedReputation { get; set; }

            public void RequestLibraryStudyStart(System.Action<LibraryStudySessionResult> onSuccess, System.Action onFailure)
            {
                StartCalled = true;
                StartCalls++;
                onSuccess?.Invoke(new LibraryStudySessionResult(
                    new ActivityRecord(ActivityIds.LibraryStudy, ActivityStatus.InProgress, "STARTED", 0, 0, 1),
                    0,
                    0,
                    false,
                    false,
                    stats.Aura,
                    stats.AcademicReputation));
            }

            public void RequestLibraryStudyComplete(
                int score,
                System.Action<LibraryStudySessionResult> onSuccess,
                System.Action onFailure)
            {
                CompleteCalls++;
                CompletedScore = score;
                if (FailComplete)
                {
                    onFailure?.Invoke();
                    return;
                }

                if (ApplyConfirmedReputation)
                {
                    activities.ApplyServerActivity(new ActivityRecord(
                        ActivityIds.LibraryStudy,
                        ActivityStatus.Completed,
                        "COMPLETED",
                        0,
                        3,
                        1));
                    stats.ApplyServerState(stats.Aura, 53f, StatUpdateSource.GameplayMutation);
                }

                onSuccess?.Invoke(new LibraryStudySessionResult(
                    new ActivityRecord(ActivityIds.LibraryStudy, ActivityStatus.Completed, "COMPLETED", 0, 3, 1),
                    score,
                    3,
                    true,
                    false,
                    stats.Aura,
                    stats.AcademicReputation));
            }
        }

        /// <summary>
        /// Uses the real PlayerProgressSync parser so a scripted score refreshes server-confirmed stats.
        /// </summary>
        private sealed class PlayerProgressSyncHarness : ILibraryStudyProgressSync
        {
            private readonly PlayerProgressSync sync;

            public PlayerProgressSyncHarness(GameObject host, PlayerStats stats, DailyActivityState activities)
            {
                sync = host.GetComponent<PlayerProgressSync>();
                if (sync == null)
                {
                    sync = host.AddComponent<PlayerProgressSync>();
                    sync.enabled = false;
                }

                stats.ApplyServerState(50f, 50f, StatUpdateSource.InitialHydration);
                activities.SetLibraryStudyStatusForTesting(ActivityStatus.Pending);
            }

            public int SubmittedScore { get; private set; } = -1;

            public void RequestLibraryStudyStart(System.Action<LibraryStudySessionResult> onSuccess, System.Action onFailure)
            {
                onSuccess?.Invoke(new LibraryStudySessionResult(
                    new ActivityRecord(ActivityIds.LibraryStudy, ActivityStatus.InProgress, "STARTED", 0, 0, 1),
                    0,
                    0,
                    false,
                    false,
                    50f,
                    50f));
            }

            public void RequestLibraryStudyComplete(
                int score,
                System.Action<LibraryStudySessionResult> onSuccess,
                System.Action onFailure)
            {
                SubmittedScore = score;
                const string json =
                    "{\"activityId\":\"LIBRARY_STUDY\",\"status\":\"COMPLETED\",\"outcome\":\"COMPLETED\",\"score\":75," +
                    "\"auraDelta\":0,\"reputationDelta\":3,\"appliedReputationDelta\":3,\"alreadyStarted\":true," +
                    "\"alreadyCompleted\":false,\"dayNumber\":1,\"aura\":50,\"academicReputation\":53}";
                bool applied = sync.ApplyLibraryStudyResponseForTesting(json);
                if (!applied)
                {
                    onFailure?.Invoke();
                    return;
                }

                onSuccess?.Invoke(new LibraryStudySessionResult(
                    new ActivityRecord(ActivityIds.LibraryStudy, ActivityStatus.Completed, "COMPLETED", 0, 3, 1),
                    score,
                    3,
                    true,
                    false,
                    50f,
                    53f));
            }
        }
    }
}
