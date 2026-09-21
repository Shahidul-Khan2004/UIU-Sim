using NUnit.Framework;
using TMPro;
using UIU.Simulator.Gameplay.Activities;
using UIU.Simulator.Gameplay.Classroom;
using UIU.Simulator.Gameplay.Player;
using UIU.Simulator.Gameplay.UI;
using UIU.Simulator.UI;
using UnityEngine;
using UnityEngine.UI;

namespace UIU.Simulator.Gameplay.Editor.Tests
{
    [TestFixture]
    public sealed class AttendIcsClassroomTests
    {
        private GameObject playerObject;
        private DailyActivityState dailyActivityState;
        private PlayerSaveState saveState;
        private StatsHUD statsHud;
        private GameObject classroomObject;
        private IcsClassroomInteractable classroom;

        [SetUp]
        public void SetUp()
        {
            if (PlayerSaveState.Instance != null)
            {
                Object.DestroyImmediate(PlayerSaveState.Instance.gameObject);
            }

            playerObject = new GameObject("IcsTestPlayer");
            playerObject.AddComponent<PlayerStats>();
            dailyActivityState = playerObject.AddComponent<DailyActivityState>();
            statsHud = playerObject.AddComponent<StatsHUD>();
            saveState = playerObject.AddComponent<PlayerSaveState>();
            saveState.SetDayProgressForTesting(1, 1);
            saveState.SetIdentityForTesting("STUDENT", "CSE", true);
            dailyActivityState.SetGetIdCardStatusForTesting(ActivityStatus.Completed, "COMPLETED");

            classroomObject = new GameObject("IcsClassroom");
            classroomObject.AddComponent<BoxCollider>();
            classroom = classroomObject.AddComponent<IcsClassroomInteractable>();
            dailyActivityState.IcsClassroom = classroom;
        }

        [TearDown]
        public void TearDown()
        {
            if (playerObject != null)
            {
                Object.DestroyImmediate(playerObject);
            }

            if (classroomObject != null)
            {
                Object.DestroyImmediate(classroomObject);
            }

            if (ClassroomChoiceUI.Instance != null)
            {
                Object.DestroyImmediate(ClassroomChoiceUI.Instance.gameObject);
            }

            if (ClassroomLectureUI.Instance != null)
            {
                Object.DestroyImmediate(ClassroomLectureUI.Instance.gameObject);
            }

            if (DailySummaryUI.Instance != null)
            {
                Object.DestroyImmediate(DailySummaryUI.Instance.gameObject);
            }
        }

        [Test]
        public void Objective_UsesConfiguredRoomAndFloor()
        {
            SetClassroomConfig(classroom, "205", 2);
            Assert.That(
                classroom.BuildObjectiveDescription(),
                Is.EqualTo("Go to Room 205 on Floor 2 to attend Introduction to Computer Science."));
        }

        [Test]
        public void Objective_MissingSetup_ReportsClearly()
        {
            SetClassroomConfig(classroom, "", -1);
            Assert.That(classroom.HasClassroomConfigured, Is.False);
            Assert.That(dailyActivityState.BuildAttendIcsObjectiveDescription(), Does.Contain("Inspector"));
        }

        [Test]
        public void Hud_ShowsConfiguredClassroomObjective_ForCseStudentDay1()
        {
            SetClassroomConfig(classroom, "205", 2);
            dailyActivityState.SetAttendIcsStatusForTesting(ActivityStatus.Pending);

            Transform icsRoot = FindChild(statsHud.transform, "IcsObjectiveBlock");
            Assert.That(icsRoot, Is.Not.Null);
            Assert.That(icsRoot.gameObject.activeSelf, Is.True);

            TextMeshProUGUI description = FindLabel(statsHud.transform, "IcsDescription");
            Assert.That(description.text, Does.Contain("Room 205"));
            Assert.That(description.text, Does.Contain("Floor 2"));
        }

        [Test]
        public void Hud_HidesIcs_ForBbaStudent()
        {
            SetClassroomConfig(classroom, "205", 2);
            saveState.SetIdentityForTesting("STUDENT", "BBA", true);
            dailyActivityState.SetAttendIcsStatusForTesting(ActivityStatus.Pending);

            Transform icsRoot = FindChild(statsHud.transform, "IcsObjectiveBlock");
            Assert.That(icsRoot.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void Hud_HidesIcs_ForFaculty()
        {
            SetClassroomConfig(classroom, "205", 2);
            saveState.SetIdentityForTesting("FACULTY", "CSE", true);
            dailyActivityState.SetAttendIcsStatusForTesting(ActivityStatus.Pending);

            Transform icsRoot = FindChild(statsHud.transform, "IcsObjectiveBlock");
            Assert.That(icsRoot.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void IneligiblePlayer_CannotOpenClassroomChoices()
        {
            SetClassroomConfig(classroom, "205", 2);
            saveState.SetIdentityForTesting("STUDENT", "BBA", true);

            string result = classroom.Interact();
            Assert.That(result, Does.Contain("CSE"));
            Assert.That(ClassroomChoiceUI.IsOpen, Is.False);
        }

        [Test]
        public void Summary_ShowsProxyDistinctLabelAndTotals()
        {
            GameObject summaryObject = new GameObject("Summary");
            DailySummaryUI summaryUi = summaryObject.AddComponent<DailySummaryUI>();

            var summary = new DayFinalizeResult(
                1,
                1,
                new[]
                {
                    new DaySummaryActivity(ActivityIds.AttendIcs, ActivityStatus.Completed, "PROXY", 5, 0)
                },
                5,
                0,
                55,
                50);

            summaryUi.ShowForTesting(summary);
            TextMeshProUGUI body = FindLabel(summaryUi.transform, "Body");
            Assert.That(body.text, Does.Contain("Proxy — Punched ID and Left"));
            Assert.That(body.text, Does.Contain("Aura: +5"));
            Assert.That(body.text, Does.Contain("Academic Reputation: 0"));

            Object.DestroyImmediate(summaryObject);
        }

        [Test]
        public void Summary_LeftEarly_ShowsNetReputation()
        {
            GameObject summaryObject = new GameObject("Summary");
            DailySummaryUI summaryUi = summaryObject.AddComponent<DailySummaryUI>();

            var summary = new DayFinalizeResult(
                1,
                1,
                new[]
                {
                    new DaySummaryActivity(ActivityIds.AttendIcs, ActivityStatus.Missed, "LEFT_EARLY", 0, -1)
                },
                0,
                -1,
                50,
                49);

            summaryUi.ShowForTesting(summary);
            TextMeshProUGUI body = FindLabel(summaryUi.transform, "Body");
            Assert.That(body.text, Does.Contain("Left Early"));
            Assert.That(body.text, Does.Contain("Academic Reputation: -1"));

            Object.DestroyImmediate(summaryObject);
        }

        [Test]
        public void ResetForNewDay_ClearsIcs()
        {
            dailyActivityState.SetAttendIcsStatusForTesting(
                ActivityStatus.Completed,
                "COMPLETED",
                0,
                12,
                90);
            dailyActivityState.ResetForNewDay(2);

            Assert.That(dailyActivityState.AttendIcsStatus, Is.EqualTo(ActivityStatus.Pending));
            Assert.That(dailyActivityState.AttendIcsMilestoneSeconds, Is.EqualTo(0));
            Assert.That(dailyActivityState.IsAttendIcsResolved, Is.False);
        }

        [Test]
        public void ChoiceMenu_UnlocksCursorAndDisablesLook()
        {
            playerObject.AddComponent<CharacterController>();
            PlayerMovement movement = playerObject.AddComponent<PlayerMovement>();
            FirstPersonLook look = playerObject.AddComponent<FirstPersonLook>();
            InteractionController interaction = playerObject.AddComponent<InteractionController>();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            ClassroomChoiceUI choice = ClassroomChoiceUI.EnsureExists();
            choice.Show("INTRODUCTION TO COMPUTER SCIENCE", "Room: 202", () => { }, () => { }, () => { });

            Assert.That(ClassroomChoiceUI.IsOpen, Is.True);
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
            Assert.That(Cursor.visible, Is.True);
            Assert.That(movement.enabled, Is.False);
            Assert.That(look.enabled, Is.False);
            Assert.That(interaction.enabled, Is.False);

            CanvasScaler scaler = choice.GetComponentInChildren<CanvasScaler>(true);
            Assert.That(scaler, Is.Not.Null);
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));

            RectTransform panel = FindChild(choice.transform, "ChoicePanel") as RectTransform;
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.sizeDelta.x, Is.InRange(550f, 700f));
        }

        [Test]
        public void ChoiceMenu_Back_RestoresGameplayControls()
        {
            playerObject.AddComponent<CharacterController>();
            PlayerMovement movement = playerObject.AddComponent<PlayerMovement>();
            FirstPersonLook look = playerObject.AddComponent<FirstPersonLook>();
            InteractionController interaction = playerObject.AddComponent<InteractionController>();

            ClassroomChoiceUI choice = ClassroomChoiceUI.EnsureExists();
            choice.Show("INTRODUCTION TO COMPUTER SCIENCE", "Room: 202", () => { }, () => { }, () => { });
            choice.Hide(restoreGameplay: true);

            Assert.That(ClassroomChoiceUI.IsOpen, Is.False);
            Assert.That(movement.enabled, Is.True);
            Assert.That(look.enabled, Is.True);
            Assert.That(interaction.enabled, Is.True);
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked));
            Assert.That(Cursor.visible, Is.False);
        }

        [Test]
        public void ChoiceMenu_HideWithoutRestore_KeepsCursorUsableForLectureHandoff()
        {
            playerObject.AddComponent<CharacterController>();
            playerObject.AddComponent<PlayerMovement>();
            playerObject.AddComponent<FirstPersonLook>();

            ClassroomChoiceUI choice = ClassroomChoiceUI.EnsureExists();
            choice.Show("INTRODUCTION TO COMPUTER SCIENCE", "Room: 202", () => { }, () => { }, () => { });
            choice.Hide(restoreGameplay: false);

            Assert.That(ClassroomChoiceUI.IsOpen, Is.False);
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
            Assert.That(Cursor.visible, Is.True);
        }

        [Test]
        public void LectureCountdown_UsesExistingElapsedTime()
        {
            Assert.That(
                ClassroomLectureUI.FormatNextMilestoneCountdown(0f, 90f, 0),
                Is.EqualTo("Next milestone in 30 seconds"));
            Assert.That(
                ClassroomLectureUI.FormatNextMilestoneCountdown(20f, 90f, 0),
                Is.EqualTo("Next milestone in 10 seconds"));
            Assert.That(
                ClassroomLectureUI.FormatNextMilestoneCountdown(29f, 90f, 0),
                Is.EqualTo("Next milestone in 1 second"));
            Assert.That(
                ClassroomLectureUI.FormatNextMilestoneCountdown(35f, 90f, 30),
                Is.EqualTo("Next milestone in 25 seconds"));
            Assert.That(
                ClassroomLectureUI.FormatNextMilestoneCountdown(45f, 90f, 30),
                Is.EqualTo("Next milestone in 15 seconds"));
            Assert.That(
                ClassroomLectureUI.FormatNextMilestoneCountdown(64f, 90f, 60),
                Is.EqualTo("Next milestone in 26 seconds"));
            Assert.That(
                ClassroomLectureUI.FormatNextMilestoneCountdown(85f, 90f, 60),
                Is.EqualTo("Next milestone in 5 seconds"));
            Assert.That(
                ClassroomLectureUI.FormatNextMilestoneCountdown(89f, 90f, 60),
                Is.EqualTo("Next milestone in 1 second"));
            Assert.That(
                ClassroomLectureUI.FormatNextMilestoneCountdown(90f, 90f, 90),
                Is.EqualTo("All milestones completed!"));
        }

        [Test]
        public void LectureConfirmedReward_TracksMilestonesOnly()
        {
            Assert.That(ClassroomLectureUI.ConfirmedReputationReward(0), Is.EqualTo(0));
            Assert.That(ClassroomLectureUI.ConfirmedReputationReward(29), Is.EqualTo(0));
            Assert.That(ClassroomLectureUI.ConfirmedReputationReward(30), Is.EqualTo(4));
            Assert.That(ClassroomLectureUI.ConfirmedReputationReward(60), Is.EqualTo(8));
            Assert.That(ClassroomLectureUI.ConfirmedReputationReward(90), Is.EqualTo(12));
        }

        [Test]
        public void LecturePanel_BuildsMilestoneRowsAndLeaveButton()
        {
            ClassroomLectureUI lecture = ClassroomLectureUI.EnsureExists();

            Assert.That(FindChild(lecture.transform, "Milestone30"), Is.Not.Null);
            Assert.That(FindChild(lecture.transform, "Milestone60"), Is.Not.Null);
            Assert.That(FindChild(lecture.transform, "Milestone90"), Is.Not.Null);
            Assert.That(FindChild(lecture.transform, "NextMilestone"), Is.Not.Null);
            Assert.That(FindChild(lecture.transform, "CurrentReward"), Is.Not.Null);
            Assert.That(FindChild(lecture.transform, "LeaveButton"), Is.Not.Null);

            CanvasScaler scaler = lecture.GetComponentInChildren<CanvasScaler>(true);
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));

            RectTransform panel = FindChild(lecture.transform, "LecturePanel") as RectTransform;
            Assert.That(panel.sizeDelta.x, Is.InRange(550f, 700f));
        }

        private static void SetClassroomConfig(IcsClassroomInteractable target, string room, int floor)
        {
            var type = typeof(IcsClassroomInteractable);
            type.GetField("classroomNumber", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(target, room);
            type.GetField("floor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(target, floor);
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }

            return null;
        }

        private static TextMeshProUGUI FindLabel(Transform root, string name)
        {
            Transform child = FindChild(root, name);
            Assert.That(child, Is.Not.Null, $"Missing label {name}");
            return child.GetComponent<TextMeshProUGUI>();
        }
    }
}
