using System.Text;
using NUnit.Framework;
using TMPro;
using UIU.Simulator.Gameplay.Activities;
using UIU.Simulator.Gameplay.Library;
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
        private InteractionController interactionController;
        private GameObject stationObject;
        private LibraryStudyInteractable station;
        private GameObject summaryObject;
        private DailySummaryUI summaryUi;

        [SetUp]
        public void SetUp()
        {
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
            saveState.SetDayProgressForTesting(1, 1);
            playerStats.ApplyServerState(50f, 50f, StatUpdateSource.InitialHydration);

            GameObject cameraObject = new GameObject("FirstPersonCamera");
            cameraObject.transform.SetParent(playerObject.transform, false);
            cameraObject.AddComponent<Camera>();

            stationObject = new GameObject("LibraryStudyStation");
            stationObject.AddComponent<BoxCollider>();
            station = stationObject.AddComponent<LibraryStudyInteractable>();

            summaryObject = new GameObject("LibrarySummary");
            summaryUi = summaryObject.AddComponent<DailySummaryUI>();
        }

        [TearDown]
        public void TearDown()
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
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked));
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
                new DaySummaryActivity(ActivityIds.Breakfast, ActivityStatus.Completed, "RICE", 5, 0),
                new DaySummaryActivity(ActivityIds.LibraryStudy, ActivityStatus.InProgress, "STARTED", 0, 0),
                new DaySummaryActivity(ActivityIds.LibraryStudy, ActivityStatus.Completed, "COMPLETED", 0, 3)
            };
            summaryUi.ShowForTesting(new DayFinalizeResult(1, 1, activities, 5, 3, 55, 53));

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
            public int CompletedScore { get; private set; } = -1;
            public bool FailComplete { get; set; }
            public bool ApplyConfirmedReputation { get; set; }

            public void RequestLibraryStudyStart(System.Action<LibraryStudySessionResult> onSuccess, System.Action onFailure)
            {
                StartCalled = true;
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
