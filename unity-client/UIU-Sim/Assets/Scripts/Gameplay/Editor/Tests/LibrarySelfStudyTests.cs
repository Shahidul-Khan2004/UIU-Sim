using System;
using System.Collections;
using System.Text;
using NUnit.Framework;
using TMPro;
using UIU.Simulator.Gameplay.Activities;
using UIU.Simulator.Gameplay.Library;
using UIU.Simulator.Gameplay.Player;
using UIU.Simulator.Gameplay.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace UIU.Simulator.Gameplay.Editor.Tests
{
    [TestFixture]
    public sealed class LibrarySelfStudyTests
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
        private LibrarySelfStudyInteractable station;
        private RecordingSelfStudySync sync;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return new EnterPlayMode();
            if (PlayerSaveState.Instance != null)
            {
                Object.DestroyImmediate(PlayerSaveState.Instance.gameObject);
            }

            if (LibrarySelfStudyUI.Instance != null)
            {
                Object.DestroyImmediate(LibrarySelfStudyUI.Instance.gameObject);
            }

            playerObject = new GameObject("LibrarySelfStudyPlayer");
            playerObject.AddComponent<CharacterController>();
            playerStats = playerObject.AddComponent<PlayerStats>();
            dailyActivityState = playerObject.AddComponent<DailyActivityState>();
            statsHud = playerObject.AddComponent<StatsHUD>();
            playerMovement = playerObject.AddComponent<PlayerMovement>();
            firstPersonLook = playerObject.AddComponent<FirstPersonLook>();
            interactionController = playerObject.AddComponent<InteractionController>();
            saveState = playerObject.AddComponent<PlayerSaveState>();
            saveState.enabled = false;
            saveState.SetDayProgressForTesting(1, 1);
            saveState.SetIdentityForTesting("STUDENT", "CSE");
            playerStats.ApplyServerState(50f, 50f, StatUpdateSource.InitialHydration);

            GameObject cameraObject = new GameObject("FirstPersonCamera");
            cameraObject.transform.SetParent(playerObject.transform, false);
            cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<CameraFollow>();

            stationObject = new GameObject("LibraryTable_01");
            stationObject.AddComponent<BoxCollider>();
            station = stationObject.AddComponent<LibrarySelfStudyInteractable>();

            sync = new RecordingSelfStudySync(playerStats, dailyActivityState);
            StudyMaterialManager.ListOverrideForTesting = () => new[]
            {
                new StudyMaterial("Data_Structures.pdf", "Data Structures", "/tmp/Data_Structures.pdf")
            };
            StudyMaterialManager.OpenOverrideForTesting = _ => true;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            StudyMaterialManager.ListOverrideForTesting = null;
            StudyMaterialManager.OpenOverrideForTesting = null;

            if (LibrarySelfStudyUI.Instance != null)
            {
                Object.DestroyImmediate(LibrarySelfStudyUI.Instance.gameObject);
            }

            SystemNotificationUI notification = Object.FindFirstObjectByType<SystemNotificationUI>();
            if (notification != null)
            {
                Object.DestroyImmediate(notification.gameObject);
            }

            if (playerObject != null) Object.DestroyImmediate(playerObject);
            if (stationObject != null) Object.DestroyImmediate(stationObject);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator Interactable_ShowsStudyPrompt()
        {
            Assert.That(station.InteractionPrompt, Is.EqualTo("Study"));
            yield break;
        }

        [UnityTest]
        public IEnumerator Interact_OpensYesNoConfirm()
        {
            station.Interact();
            yield return null;

            Assert.That(LibrarySelfStudyUI.IsOpen, Is.True);
            TextMeshProUGUI prompt = FindLabel(LibrarySelfStudyUI.Instance.transform, "Prompt");
            Assert.That(prompt.text, Is.EqualTo("Do you want to study?"));
            Assert.That(FindButton(LibrarySelfStudyUI.Instance.transform, "YesButton"), Is.Not.Null);
            Assert.That(FindButton(LibrarySelfStudyUI.Instance.transform, "NoButton"), Is.Not.Null);
            Assert.That(playerMovement.enabled, Is.False);
            Assert.That(firstPersonLook.enabled, Is.False);
            Assert.That(interactionController.enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator No_ClosesAndRestoresControls()
        {
            station.Interact();
            yield return null;
            FindButton(LibrarySelfStudyUI.Instance.transform, "NoButton").onClick.Invoke();
            yield return null;

            Assert.That(LibrarySelfStudyUI.IsOpen, Is.False);
            Assert.That(playerMovement.enabled, Is.True);
            Assert.That(interactionController.enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator Yes_ShowsPdfList()
        {
            station.Interact();
            yield return null;
            FindButton(LibrarySelfStudyUI.Instance.transform, "YesButton").onClick.Invoke();
            yield return null;

            Assert.That(LibrarySelfStudyUI.IsOpen, Is.True);
            TextMeshProUGUI title = FindLabel(LibrarySelfStudyUI.Instance.transform, "MaterialsTitle");
            Assert.That(title.text, Is.EqualTo("SELECT STUDY MATERIAL"));
            Button material = FindButton(LibrarySelfStudyUI.Instance.transform, "MaterialButton_0");
            Assert.That(material, Is.Not.Null);
            Assert.That(material.GetComponentInChildren<TextMeshProUGUI>().text, Is.EqualTo("Data Structures"));
            RectTransform panel = FindChild(LibrarySelfStudyUI.Instance.transform, "MaterialsPanel") as RectTransform;
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.sizeDelta.x, Is.EqualTo(640f));
            Assert.That(FindChild(LibrarySelfStudyUI.Instance.transform, "MaterialsScroll"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator SelectPdf_OpensAndStartsSession()
        {
            StudyMaterial opened = default;
            StudyMaterialManager.OpenOverrideForTesting = material =>
            {
                opened = material;
                return true;
            };

            LibrarySelfStudyUI ui = LibrarySelfStudyUI.EnsureExists();
            ui.SetProgressSyncForTesting(sync);
            ui.SetDurationForTesting(90f);

            station.Interact();
            yield return null;
            FindButton(ui.transform, "YesButton").onClick.Invoke();
            yield return null;
            FindButton(ui.transform, "MaterialButton_0").onClick.Invoke();
            yield return null;

            Assert.That(opened.FileName, Is.EqualTo("Data_Structures.pdf"));
            Assert.That(sync.StartCalls, Is.EqualTo(1));
            Assert.That(FindLabel(ui.transform, "Header").text, Is.EqualTo("SELF STUDY"));
            Assert.That(FindLabel(ui.transform, "MaterialName").text, Is.EqualTo("Data Structures"));
            Assert.That(FindLabel(ui.transform, "Status").text, Does.Contain("Studying"));
            Assert.That(FindLabel(ui.transform, "TimeRemainingCaption").text, Is.EqualTo("Time Remaining"));
            Assert.That(FindLabel(ui.transform, "Timer").text, Does.Contain("/ 90 min"));
            Assert.That(FindLabel(ui.transform, "CurrentReward").text, Does.Contain("Academic Reputation"));
            Assert.That(FindChild(ui.transform, "ProgressFill"), Is.Not.Null);
            Assert.That(FindLabel(ui.transform, "Milestone30").text, Does.Contain("+1 Academic Reputation"));
            Assert.That(playerMovement.enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator Session_ClaimsMilestonesAndCompletes()
        {
            LibrarySelfStudyUI ui = LibrarySelfStudyUI.EnsureExists();
            ui.SetProgressSyncForTesting(sync);
            ui.SetDurationForTesting(0.3f);

            station.Interact();
            yield return null;
            FindButton(ui.transform, "YesButton").onClick.Invoke();
            yield return null;
            FindButton(ui.transform, "MaterialButton_0").onClick.Invoke();

            float deadline = Time.realtimeSinceStartup + 3f;
            while (!dailyActivityState.IsLibrarySelfStudyCompleted && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(dailyActivityState.IsLibrarySelfStudyCompleted, Is.True);
            Assert.That(sync.MilestoneCalls, Is.EqualTo(3));
            Assert.That(playerStats.AcademicReputation, Is.EqualTo(53f));
            Assert.That(playerStats.Aura, Is.EqualTo(50f));
            Assert.That(LibrarySelfStudyUI.IsOpen, Is.False);
            Assert.That(playerMovement.enabled, Is.True);
            string notification = ConcatenateLabels(SystemNotificationUI.Instance.transform);
            Assert.That(notification, Does.Contain("Study session completed"));
            Assert.That(notification, Does.Contain("You have studied enough for today. Good job."));
        }

        [UnityTest]
        public IEnumerator LongMaterialName_DoesNotOverflowCard()
        {
            const string longName =
                "An Extremely Long Introduction To Computer Science WEB Discrete Mathematics And Applications Extra Extra Extra";
            StudyMaterialManager.ListOverrideForTesting = () => new[]
            {
                new StudyMaterial("long.pdf", longName, "/tmp/long.pdf")
            };

            station.Interact();
            yield return null;
            FindButton(LibrarySelfStudyUI.Instance.transform, "YesButton").onClick.Invoke();
            yield return null;

            Button material = FindButton(LibrarySelfStudyUI.Instance.transform, "MaterialButton_0");
            Assert.That(material, Is.Not.Null);
            TextMeshProUGUI label = material.GetComponentInChildren<TextMeshProUGUI>();
            Assert.That(label.overflowMode, Is.EqualTo(TextOverflowModes.Ellipsis));
            Assert.That(label.maxVisibleLines, Is.EqualTo(2));
            Assert.That(label.textWrappingMode, Is.EqualTo(TextWrappingModes.Normal));

            RectTransform panel = FindChild(LibrarySelfStudyUI.Instance.transform, "MaterialsPanel") as RectTransform;
            RectTransform buttonRect = material.GetComponent<RectTransform>();
            Assert.That(panel, Is.Not.Null);
            Assert.That(buttonRect.rect.width, Is.LessThanOrEqualTo(panel.rect.width));
        }

        [UnityTest]
        public IEnumerator Leave_ShowsInterruptionNotification()
        {
            LibrarySelfStudyUI ui = LibrarySelfStudyUI.EnsureExists();
            ui.SetProgressSyncForTesting(sync);
            ui.SetDurationForTesting(90f);

            station.Interact();
            yield return null;
            FindButton(ui.transform, "YesButton").onClick.Invoke();
            yield return null;
            FindButton(ui.transform, "MaterialButton_0").onClick.Invoke();
            yield return null;
            FindButton(ui.transform, "LeaveButton").onClick.Invoke();
            yield return null;

            Assert.That(LibrarySelfStudyUI.IsOpen, Is.False);
            Assert.That(playerMovement.enabled, Is.True);
            Assert.That(dailyActivityState.IsLibrarySelfStudyUsedToday, Is.True);
            string notification = ConcatenateLabels(SystemNotificationUI.Instance.transform);
            Assert.That(notification, Does.Contain(LibrarySelfStudyInteractable.FinishedStudyingMessage));

            string retry = station.Interact();
            Assert.That(retry, Is.EqualTo(LibrarySelfStudyInteractable.StudiedEnoughMessage));
            Assert.That(LibrarySelfStudyUI.IsOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator Started_BlocksRepeatStudy()
        {
            dailyActivityState.SetLibrarySelfStudyStatusForTesting(ActivityStatus.InProgress, "STARTED", 0, 0);
            string response = station.Interact();
            yield return null;

            Assert.That(response, Is.EqualTo(LibrarySelfStudyInteractable.StudiedEnoughMessage));
            Assert.That(LibrarySelfStudyUI.IsOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator Completed_BlocksRepeatStudy()
        {
            dailyActivityState.SetLibrarySelfStudyStatusForTesting(ActivityStatus.Completed, "COMPLETED", 3, 90);
            string response = station.Interact();
            yield return null;

            Assert.That(response, Is.EqualTo(LibrarySelfStudyInteractable.StudiedEnoughMessage));
            Assert.That(LibrarySelfStudyUI.IsOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator Hud_DoesNotListLibrarySelfStudy()
        {
            dailyActivityState.SetGetIdCardStatusForTesting(ActivityStatus.Completed, "COMPLETED");
            dailyActivityState.SetLibrarySelfStudyStatusForTesting(ActivityStatus.Completed, "COMPLETED", 3, 90);
            statsHud.enabled = false;
            statsHud.enabled = true;
            yield return null;

            string hud = ConcatenateLabels(statsHud.transform);
            Assert.That(hud, Does.Not.Contain("Library Self Study"));
            Assert.That(hud, Does.Not.Contain("Self Study"));
            Assert.That(hud, Does.Contain("Have Breakfast"));
        }

        [UnityTest]
        public IEnumerator NextDay_ClearsSelfStudy()
        {
            dailyActivityState.SetLibrarySelfStudyStatusForTesting(ActivityStatus.Completed, "COMPLETED", 3, 90);
            dailyActivityState.ResetForNewDay(2);
            Assert.That(dailyActivityState.IsLibrarySelfStudyCompleted, Is.False);
            Assert.That(dailyActivityState.IsLibrarySelfStudyUsedToday, Is.False);
            Assert.That(dailyActivityState.LibrarySelfStudyStatus, Is.EqualTo(ActivityStatus.Pending));
            yield break;
        }

        [UnityTest]
        public IEnumerator Faculty_CannotStudy()
        {
            saveState.SetIdentityForTesting("FACULTY", "CSE");
            string response = station.Interact();
            yield return null;
            Assert.That(response, Is.EqualTo(LibrarySelfStudyInteractable.StudentsOnlyMessage));
            Assert.That(LibrarySelfStudyUI.IsOpen, Is.False);
        }

        [Test]
        public void DisplayName_PrettifiesFileName()
        {
            Assert.That(
                StudyMaterialManager.ToDisplayName("Introduction_To_Computer_Science_-_WEB.pdf"),
                Is.EqualTo("Introduction To Computer Science WEB"));
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
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChild(root.GetChild(i), name);
                if (found != null) return found;
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

        private sealed class RecordingSelfStudySync : ILibrarySelfStudyProgressSync
        {
            private readonly PlayerStats stats;
            private readonly DailyActivityState activities;
            private int milestoneSeconds;
            private long elapsedMs;

            public RecordingSelfStudySync(PlayerStats stats, DailyActivityState activities)
            {
                this.stats = stats;
                this.activities = activities;
            }

            public int StartCalls { get; private set; }
            public int MilestoneCalls { get; private set; }

            public void RequestLibrarySelfStudyStart(Action<LibrarySelfStudySessionResult> onSuccess, Action onFailure)
            {
                StartCalls++;
                activities.ApplyServerActivity(new ActivityRecord(
                    ActivityIds.LibrarySelfStudy,
                    ActivityStatus.InProgress,
                    "STARTED",
                    0,
                    0,
                    1,
                    0));
                onSuccess?.Invoke(Result(ActivityStatus.InProgress, "STARTED", 0, 0, false, false, true, 0));
            }

            public void RequestLibrarySelfStudyPause(Action<LibrarySelfStudySessionResult> onSuccess, Action onFailure)
            {
                onSuccess?.Invoke(Result(ActivityStatus.InProgress, "STARTED", milestoneSeconds, 0, true, false, false, elapsedMs));
            }

            public void RequestLibrarySelfStudyAbandon(Action<LibrarySelfStudySessionResult> onSuccess, Action onFailure)
            {
                activities.ApplyServerActivity(new ActivityRecord(
                    ActivityIds.LibrarySelfStudy,
                    ActivityStatus.Completed,
                    "ABANDONED",
                    0,
                    (int)(stats.AcademicReputation - 50f),
                    1,
                    milestoneSeconds));
                onSuccess?.Invoke(Result(ActivityStatus.Completed, "ABANDONED", milestoneSeconds, 0, false, true, false, elapsedMs));
            }

            public void RequestLibrarySelfStudyResume(Action<LibrarySelfStudySessionResult> onSuccess, Action onFailure)
            {
                onSuccess?.Invoke(Result(ActivityStatus.InProgress, "STARTED", milestoneSeconds, 0, true, false, true, elapsedMs));
            }

            public void RequestLibrarySelfStudyMilestone(
                int seconds,
                Action<LibrarySelfStudySessionResult> onSuccess,
                Action onFailure)
            {
                MilestoneCalls++;
                milestoneSeconds = seconds;
                elapsedMs = seconds * 1000L;
                int reward = 1;
                float nextRep = Mathf.Min(100f, stats.AcademicReputation + reward);
                stats.ApplyServerState(stats.Aura, nextRep, StatUpdateSource.GameplayMutation);
                ActivityStatus status = seconds >= 90 ? ActivityStatus.Completed : ActivityStatus.InProgress;
                string outcome = seconds >= 90 ? "COMPLETED" : "STARTED";
                activities.ApplyServerActivity(new ActivityRecord(
                    ActivityIds.LibrarySelfStudy,
                    status,
                    outcome,
                    0,
                    (int)(nextRep - 50f),
                    1,
                    seconds));
                onSuccess?.Invoke(Result(status, outcome, seconds, reward, false, seconds >= 90, seconds < 90, elapsedMs));
            }

            private LibrarySelfStudySessionResult Result(
                ActivityStatus status,
                string outcome,
                int milestone,
                int applied,
                bool alreadyApplied,
                bool alreadyCompleted,
                bool sessionActive,
                long activeMs)
            {
                return new LibrarySelfStudySessionResult(
                    new ActivityRecord(ActivityIds.LibrarySelfStudy, status, outcome, 0, (int)(stats.AcademicReputation - 50f), 1, milestone),
                    applied,
                    alreadyApplied,
                    alreadyCompleted,
                    sessionActive,
                    activeMs,
                    stats.Aura,
                    stats.AcademicReputation);
            }
        }
    }
}
