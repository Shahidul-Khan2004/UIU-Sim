using System;
using System.Collections;
using System.Collections.Generic;
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

        private IcsClassroomLocationConfig locationConfigAsset;
        private IcsClassroomLocationConfig englishConfig;
        private IcsClassroomLocationConfig dmConfig;

        [SetUp]
        public void SetUp()
        {
            if (PlayerSaveState.Instance != null)
            {
                UnityEngine.Object.DestroyImmediate(PlayerSaveState.Instance.gameObject);
            }

            if (StatsHUD.Instance != null)
            {
                UnityEngine.Object.DestroyImmediate(StatsHUD.Instance.gameObject);
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
                UnityEngine.Object.DestroyImmediate(playerObject);
            }

            if (classroomObject != null)
            {
                UnityEngine.Object.DestroyImmediate(classroomObject);
            }

            if (locationConfigAsset != null)
            {
                UnityEngine.Object.DestroyImmediate(locationConfigAsset);
                locationConfigAsset = null;
            }

            if (englishConfig != null)
            {
                UnityEngine.Object.DestroyImmediate(englishConfig);
                englishConfig = null;
            }

            if (dmConfig != null)
            {
                UnityEngine.Object.DestroyImmediate(dmConfig);
                dmConfig = null;
            }

            if (ClassroomChoiceUI.Instance != null)
            {
                UnityEngine.Object.DestroyImmediate(ClassroomChoiceUI.Instance.gameObject);
            }

            if (ClassroomLectureUI.Instance != null)
            {
                UnityEngine.Object.DestroyImmediate(ClassroomLectureUI.Instance.gameObject);
            }

            if (DailySummaryUI.Instance != null)
            {
                UnityEngine.Object.DestroyImmediate(DailySummaryUI.Instance.gameObject);
            }

            SystemNotificationUI notification = UnityEngine.Object.FindFirstObjectByType<SystemNotificationUI>();
            if (notification != null)
            {
                UnityEngine.Object.DestroyImmediate(notification.gameObject);
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
        public void Hud_SingleInstance_SingleBackground_UnifiedObjectives()
        {
            SetClassroomConfig(classroom, "205", 2);
            dailyActivityState.SetAttendIcsStatusForTesting(ActivityStatus.Pending);
            statsHud.enabled = false;
            statsHud.enabled = true;

            Assert.That(UnityEngine.Object.FindObjectsByType<StatsHUD>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(statsHud.CountBackgroundImagesForTesting(), Is.EqualTo(1));
            Assert.That(statsHud.IsIcsInsideStatsPanelForTesting(), Is.True);

            Transform panel = FindChild(statsHud.transform, "StatsPanel");
            Assert.That(FindChild(panel, "IdCardObjectiveBlock"), Is.Not.Null);
            Assert.That(FindChild(panel, "BreakfastObjectiveBlock"), Is.Not.Null);
            Assert.That(FindChild(panel, "IcsObjectiveBlock"), Is.Not.Null);
            Assert.That(FindChild(panel, "IcsObjectiveBlock").gameObject.activeSelf, Is.True);
            Assert.That(FindChild(panel, "BreakfastObjectiveBlock").gameObject.activeSelf, Is.True);
        }

        [Test]
        public void Hud_IcsRemainsVisible_WhenClassroomDestroyed()
        {
            SetClassroomConfig(classroom, "205", 2);
            dailyActivityState.SetAttendIcsStatusForTesting(ActivityStatus.Pending);
            Assert.That(dailyActivityState.ShouldShowAttendIcsObjective(saveState), Is.True);

            UnityEngine.Object.DestroyImmediate(classroomObject);
            classroomObject = null;
            classroom = null;

            Assert.That(dailyActivityState.ShouldShowAttendIcsObjective(saveState), Is.True,
                "ICS objective must survive additive floor unload via cached classroom config.");

            statsHud.enabled = false;
            statsHud.enabled = true;
            Transform icsRoot = FindChild(statsHud.transform, "IcsObjectiveBlock");
            Assert.That(icsRoot.gameObject.activeSelf, Is.True);
            TextMeshProUGUI description = FindLabel(statsHud.transform, "IcsDescription");
            Assert.That(description.text, Does.Contain("Room 205"));
        }

        [Test]
        public void Objective_UsesPersistentLocationConfig_WhenClassroomNeverLoaded()
        {
            DestroyClassroomWithoutCache();
            locationConfigAsset = CreateLocationConfig("427", 4);
            dailyActivityState.SetLocationConfigForTesting(locationConfigAsset);

            Assert.That(
                dailyActivityState.BuildAttendIcsObjectiveDescription(),
                Is.EqualTo("Go to Room 427 on Floor 4 to attend Introduction to Computer Science."));
        }

        [Test]
        public void Hud_ShowsPersistentClassroomLocation_WhenFloor04IsUnloaded()
        {
            DestroyClassroomWithoutCache();
            locationConfigAsset = CreateLocationConfig("427", 4);
            dailyActivityState.SetLocationConfigForTesting(locationConfigAsset);
            dailyActivityState.SetAttendIcsStatusForTesting(ActivityStatus.Pending);

            statsHud.enabled = false;
            statsHud.enabled = true;

            Transform icsRoot = FindChild(statsHud.transform, "IcsObjectiveBlock");
            Assert.That(icsRoot, Is.Not.Null);
            Assert.That(icsRoot.gameObject.activeSelf, Is.True);
            TextMeshProUGUI description = FindLabel(statsHud.transform, "IcsDescription");
            Assert.That(description.text, Does.Contain("Room 427"));
            Assert.That(description.text, Does.Contain("Floor 4"));
            Assert.That(
                description.text,
                Is.EqualTo("Go to Room 427 on Floor 4 to attend Introduction to Computer Science."));
        }

        [Test]
        public void Hud_CompletedIcs_StaysVisibleAndGreen_SameDay()
        {
            SetClassroomConfig(classroom, "205", 2);
            dailyActivityState.SetAttendIcsStatusForTesting(
                ActivityStatus.Completed,
                "COMPLETED",
                0,
                12,
                90);

            statsHud.enabled = false;
            statsHud.enabled = true;

            Transform icsRoot = FindChild(statsHud.transform, "IcsObjectiveBlock");
            Assert.That(icsRoot.gameObject.activeSelf, Is.True);
            TextMeshProUGUI marker = FindLabel(statsHud.transform, "IcsMarker");
            TextMeshProUGUI title = FindLabel(statsHud.transform, "IcsTitle");
            Assert.That(marker.text, Is.EqualTo("[x]"));
            Assert.That(marker.color, Is.EqualTo(UiTheme.Success));
            Assert.That(title.color, Is.EqualTo(UiTheme.Success));
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
        public void Hud_HidesIcs_OnDay2()
        {
            SetClassroomConfig(classroom, "205", 2);
            saveState.SetDayProgressForTesting(1, 2);
            dailyActivityState.ResetForNewDay(2);

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
        public void CompletedClass_ShowsTerminalMessage_NotLectureUi()
        {
            SetClassroomConfig(classroom, "205", 2);
            dailyActivityState.SetAttendIcsStatusForTesting(
                ActivityStatus.Completed,
                "COMPLETED",
                0,
                12,
                90);

            string result = classroom.Interact();
            Assert.That(result, Does.Contain("already completed"));
            Assert.That(ClassroomChoiceUI.IsOpen, Is.False);
            Assert.That(ClassroomLectureUI.IsOpen, Is.False);
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

            UnityEngine.Object.DestroyImmediate(summaryObject);
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

            UnityEngine.Object.DestroyImmediate(summaryObject);
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
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.sizeDelta.x, Is.InRange(550f, 700f));
        }

        [Test]
        public void Lecture_FinalMilestoneSuccess_AutoClosesAndRestoresControls()
        {
            playerObject.AddComponent<CharacterController>();
            PlayerMovement movement = playerObject.AddComponent<PlayerMovement>();
            FirstPersonLook look = playerObject.AddComponent<FirstPersonLook>();
            InteractionController interaction = playerObject.AddComponent<InteractionController>();

            var sync = new FakeAttendIcsSync();
            sync.QueueMilestoneSuccess(90, ActivityStatus.Completed, "COMPLETED", 4, 12);

            ClassroomLectureUI lecture = ClassroomLectureUI.EnsureExists();
            lecture.SetCompletionCloseDelayForTesting(0f);

            var start = new AttendIcsSessionResult(
                new ActivityRecord(ActivityIds.AttendIcs, ActivityStatus.InProgress, "ATTENDING", 0, 8, 1, 60),
                false,
                true,
                60000,
                0,
                0,
                0,
                0,
                50f,
                58f);

            lecture.BeginLecture(classroom, sync, start, () => { });
            Assert.That(ClassroomLectureUI.IsOpen, Is.True);
            Assert.That(movement.enabled, Is.False);

            RunToCompletion(lecture.ClaimMilestoneForTesting(90));

            Assert.That(ClassroomLectureUI.IsOpen, Is.False);
            Assert.That(lecture.IsLecturePanelActiveForTesting(), Is.False);
            Assert.That(lecture.IsLeaveButtonInteractableForTesting(), Is.False);
            Assert.That(sync.MilestoneRequestCount, Is.EqualTo(1));
            Assert.That(movement.enabled, Is.True);
            Assert.That(look.enabled, Is.True);
            Assert.That(interaction.enabled, Is.True);
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked));
            Assert.That(Cursor.visible, Is.False);
        }

        [Test]
        public void Lecture_FinalMilestoneFailure_DoesNotComplete_AllowsRetry()
        {
            var sync = new FakeAttendIcsSync();
            sync.QueueMilestoneFailure();
            sync.QueueMilestoneSuccess(90, ActivityStatus.Completed, "COMPLETED", 4, 12);

            ClassroomLectureUI lecture = ClassroomLectureUI.EnsureExists();
            lecture.SetCompletionCloseDelayForTesting(0f);

            var start = new AttendIcsSessionResult(
                new ActivityRecord(ActivityIds.AttendIcs, ActivityStatus.InProgress, "ATTENDING", 0, 8, 1, 60),
                false,
                true,
                60000,
                0,
                0,
                0,
                0,
                50f,
                58f);

            lecture.BeginLecture(classroom, sync, start, () => { });

            RunToCompletion(lecture.ClaimMilestoneForTesting(90));
            Assert.That(ClassroomLectureUI.IsOpen, Is.True);
            Assert.That(lecture.StatusTextForTesting(), Does.Contain("Retrying"));
            Assert.That(lecture.IsLeaveButtonInteractableForTesting(), Is.True);
            Assert.That(lecture.ClaimedMilestoneForTesting(), Is.EqualTo(60));

            RunToCompletion(lecture.ClaimMilestoneForTesting(90));
            Assert.That(ClassroomLectureUI.IsOpen, Is.False);
            Assert.That(sync.MilestoneRequestCount, Is.EqualTo(2));
            Assert.That(lecture.FinalMilestoneAttemptsForTesting(), Is.EqualTo(2));
        }

        [Test]
        public void Lecture_DoesNotResubmitAlreadyClaimedMilestone()
        {
            var sync = new FakeAttendIcsSync();
            ClassroomLectureUI lecture = ClassroomLectureUI.EnsureExists();
            lecture.SetCompletionCloseDelayForTesting(0f);

            var start = new AttendIcsSessionResult(
                new ActivityRecord(ActivityIds.AttendIcs, ActivityStatus.InProgress, "ATTENDING", 0, 8, 1, 60),
                false,
                true,
                60000,
                0,
                0,
                0,
                0,
                50f,
                58f);

            lecture.BeginLecture(classroom, sync, start, () => { });
            RunToCompletion(lecture.ClaimMilestoneForTesting(30));
            Assert.That(sync.MilestoneRequestCount, Is.EqualTo(0));
        }

        [Test]
        public void Hud_SurvivesLectureClose()
        {
            SetClassroomConfig(classroom, "205", 2);
            dailyActivityState.SetAttendIcsStatusForTesting(ActivityStatus.Pending);

            var sync = new FakeAttendIcsSync();
            sync.QueueMilestoneSuccess(90, ActivityStatus.Completed, "COMPLETED", 4, 12);

            ClassroomLectureUI lecture = ClassroomLectureUI.EnsureExists();
            lecture.SetCompletionCloseDelayForTesting(0f);
            var start = new AttendIcsSessionResult(
                new ActivityRecord(ActivityIds.AttendIcs, ActivityStatus.InProgress, "ATTENDING", 0, 8, 1, 60),
                false,
                true,
                60000,
                0,
                0,
                0,
                0,
                50f,
                58f);

            lecture.BeginLecture(classroom, sync, start, () => { });
            RunToCompletion(lecture.ClaimMilestoneForTesting(90));

            Assert.That(statsHud, Is.Not.Null);
            Assert.That(statsHud.enabled, Is.True);
            Assert.That(FindChild(statsHud.transform, "StatsPanel"), Is.Not.Null);
            Assert.That(FindChild(statsHud.transform, "StatsPanel").gameObject.activeInHierarchy, Is.True);
        }

        [Test]
        public void Hud_ShowsEnglishAndDmFromConfigs_WhenFloorsAreUnloaded()
        {
            DestroyClassroomWithoutCache();
            locationConfigAsset = CreateLocationConfig("427", 4);
            IcsClassroomLocationConfig english = CreateCourseConfig(
                ActivityIds.AttendEnglish, "ENGLISH", "English", "702", 7);
            IcsClassroomLocationConfig dm = CreateCourseConfig(
                ActivityIds.AttendDm, "DM", "Discrete Mathematics", "423", 4);
            dailyActivityState.SetLocationConfigForTesting(locationConfigAsset);
            dailyActivityState.SetAdditionalLocationConfigsForTesting(new[] { english, dm });
            dailyActivityState.SetAttendIcsStatusForTesting(ActivityStatus.Pending);

            statsHud.enabled = false;
            statsHud.enabled = true;

            Assert.That(statsHud.CountBackgroundImagesForTesting(), Is.EqualTo(1));
            Transform panel = FindChild(statsHud.transform, "StatsPanel");
            Assert.That(FindChild(panel, "IcsObjectiveBlock").gameObject.activeSelf, Is.True);
            Assert.That(FindLabel(panel, "IcsDescription").text,
                Is.EqualTo("Go to Room 427 on Floor 4 to attend Introduction to Computer Science."));

            Transform englishRoot = FindChild(panel, "Classroom_ATTEND_ENGLISH");
            Transform dmRoot = FindChild(panel, "Classroom_ATTEND_DM");
            Assert.That(englishRoot.gameObject.activeSelf, Is.True);
            Assert.That(dmRoot.gameObject.activeSelf, Is.True);
            Assert.That(englishRoot.IsChildOf(panel), Is.True);
            Assert.That(FindLabel(panel, "ATTEND_ENGLISHTitle").text, Is.EqualTo("Attend English"));
            Assert.That(FindLabel(panel, "ATTEND_ENGLISHDescription").text,
                Is.EqualTo("Go to Room 702 on Floor 7 to attend English."));
            Assert.That(FindLabel(panel, "ATTEND_DMTitle").text, Is.EqualTo("Attend Discrete Mathematics"));
            Assert.That(FindLabel(panel, "ATTEND_DMDescription").text,
                Is.EqualTo("Go to Room 423 on Floor 4 to attend Discrete Mathematics."));

            UnityEngine.Object.DestroyImmediate(english);
            UnityEngine.Object.DestroyImmediate(dm);
        }

        [Test]
        public void EnglishDoor_OpensEnglishChoice_NotIcs()
        {
            GameObject door = new GameObject("EnglishDoor");
            door.AddComponent<BoxCollider>();
            IcsClassroomInteractable english = door.AddComponent<IcsClassroomInteractable>();
            SetCourseFields(english, ActivityIds.AttendEnglish, "ENGLISH", "English", "702", 7);

            string result = english.Interact();

            Assert.That(result, Is.Null);
            Assert.That(ClassroomChoiceUI.IsOpen, Is.True);
            Assert.That(FindLabel(ClassroomChoiceUI.Instance.transform, "Title").text, Is.EqualTo("ENGLISH"));
            Assert.That(FindLabel(ClassroomChoiceUI.Instance.transform, "Room").text, Is.EqualTo("Room: 702"));
            UnityEngine.Object.DestroyImmediate(door);
        }

        [Test]
        public void DmDoor_OpensDiscreteMathematics_WithoutResolvingIcs()
        {
            dailyActivityState.SetAttendIcsStatusForTesting(ActivityStatus.Pending);
            GameObject door = new GameObject("DmDoor");
            door.AddComponent<BoxCollider>();
            IcsClassroomInteractable dm = door.AddComponent<IcsClassroomInteractable>();
            SetCourseFields(dm, ActivityIds.AttendDm, "DM", "Discrete Mathematics", "423", 4);

            string result = dm.Interact();

            Assert.That(result, Is.Null);
            Assert.That(ClassroomChoiceUI.IsOpen, Is.True);
            Assert.That(FindLabel(ClassroomChoiceUI.Instance.transform, "Title").text, Is.EqualTo("DISCRETE MATHEMATICS"));
            Assert.That(FindLabel(ClassroomChoiceUI.Instance.transform, "Room").text, Is.EqualTo("Room: 423"));
            Assert.That(dailyActivityState.IsAttendIcsResolved, Is.False);
            UnityEngine.Object.DestroyImmediate(door);
        }

        [Test]
        public void ClassroomProgress_IsIndependent()
        {
            dailyActivityState.SetAttendIcsStatusForTesting(ActivityStatus.Pending);
            dailyActivityState.SetClassroomStatusForTesting(
                ActivityIds.AttendEnglish,
                ActivityStatus.Completed,
                "COMPLETED",
                0,
                12,
                90);
            dailyActivityState.SetClassroomStatusForTesting(
                ActivityIds.AttendDm,
                ActivityStatus.Missed,
                "LEFT_EARLY",
                0,
                -1,
                30);

            Assert.That(dailyActivityState.IsAttendIcsResolved, Is.False);
            Assert.That(dailyActivityState.IsClassroomResolved(ActivityIds.AttendEnglish), Is.True);
            Assert.That(dailyActivityState.IsClassroomResolved(ActivityIds.AttendDm), Is.True);
            Assert.That(dailyActivityState.GetClassroomOutcome(ActivityIds.AttendDm), Is.EqualTo("LEFT_EARLY"));
            Assert.That(dailyActivityState.AttendIcsMilestoneSeconds, Is.EqualTo(0));
        }

        [Test]
        public void Day2_HidesUnscheduledEnglishAndDm()
        {
            DestroyClassroomWithoutCache();
            locationConfigAsset = CreateLocationConfig("427", 4);
            IcsClassroomLocationConfig english = CreateCourseConfig(
                ActivityIds.AttendEnglish, "ENGLISH", "English", "702", 7);
            IcsClassroomLocationConfig dm = CreateCourseConfig(
                ActivityIds.AttendDm, "DM", "Discrete Mathematics", "423", 4);
            dailyActivityState.SetLocationConfigForTesting(locationConfigAsset);
            dailyActivityState.SetAdditionalLocationConfigsForTesting(new[] { english, dm });
            dailyActivityState.SetClassroomStatusForTesting(
                ActivityIds.AttendEnglish, ActivityStatus.Completed, "COMPLETED", 0, 12, 90);

            saveState.SetDayProgressForTesting(1, 2);
            dailyActivityState.ResetForNewDay(2);

            Transform englishRoot = FindChild(statsHud.transform, "Classroom_ATTEND_ENGLISH");
            Transform dmRoot = FindChild(statsHud.transform, "Classroom_ATTEND_DM");
            Transform icsRoot = FindChild(statsHud.transform, "IcsObjectiveBlock");
            if (englishRoot != null)
            {
                Assert.That(englishRoot.gameObject.activeSelf, Is.False);
            }

            if (dmRoot != null)
            {
                Assert.That(dmRoot.gameObject.activeSelf, Is.False);
            }

            Assert.That(icsRoot.gameObject.activeSelf, Is.False);
            Assert.That(dailyActivityState.IsClassroomResolved(ActivityIds.AttendEnglish), Is.False);
            UnityEngine.Object.DestroyImmediate(english);
            UnityEngine.Object.DestroyImmediate(dm);
        }

        [Test]
        public void Summary_ListsEachCourseIndependently()
        {
            GameObject summaryObject = new GameObject("SummaryCourses");
            DailySummaryUI summaryUi = summaryObject.AddComponent<DailySummaryUI>();
            var summary = new DayFinalizeResult(
                1,
                1,
                new[]
                {
                    new DaySummaryActivity(ActivityIds.AttendIcs, ActivityStatus.Completed, "COMPLETED", 0, 12),
                    new DaySummaryActivity(ActivityIds.AttendEnglish, ActivityStatus.Missed, "SKIPPED", 0, -5),
                    new DaySummaryActivity(ActivityIds.AttendDm, ActivityStatus.Missed, "LEFT_EARLY", 0, -1)
                },
                0,
                6,
                50,
                56);

            summaryUi.ShowForTesting(summary);
            TextMeshProUGUI body = FindLabel(summaryUi.transform, "Body");
            TextMeshProUGUI totals = FindLabel(summaryUi.transform, "Totals");
            Assert.That(body.text, Does.Contain("Introduction to Computer Science"));
            Assert.That(body.text, Does.Contain("Academic Reputation: +12"));
            Assert.That(body.text, Does.Contain("English (Missed)"));
            Assert.That(body.text, Does.Contain("Academic Reputation: -5"));
            Assert.That(body.text, Does.Contain("Discrete Mathematics (Left Early)"));
            Assert.That(body.text, Does.Contain("Academic Reputation: -1"));
            Assert.That(totals.text, Does.Contain("Academic Reputation: +6"));
            UnityEngine.Object.DestroyImmediate(summaryObject);
        }

        [Test]
        public void Lecture_EnglishHeader_AutoClosesAndRestoresControls()
        {
            playerObject.AddComponent<CharacterController>();
            PlayerMovement movement = playerObject.AddComponent<PlayerMovement>();
            FirstPersonLook look = playerObject.AddComponent<FirstPersonLook>();
            InteractionController interaction = playerObject.AddComponent<InteractionController>();
            SetCourseFields(classroom, ActivityIds.AttendEnglish, "ENGLISH", "English", "702", 7);

            var sync = new FakeAttendIcsSync();
            sync.QueueMilestoneSuccess(90, ActivityStatus.Completed, "COMPLETED", 4, 12);
            ClassroomLectureUI lecture = ClassroomLectureUI.EnsureExists();
            lecture.SetCompletionCloseDelayForTesting(0f);
            var start = new AttendIcsSessionResult(
                new ActivityRecord(ActivityIds.AttendEnglish, ActivityStatus.InProgress, "ATTENDING", 0, 8, 1, 60),
                false,
                true,
                60000,
                0,
                0,
                0,
                0,
                50f,
                58f);

            lecture.BeginLecture(classroom, sync, start, () => { });
            Assert.That(FindLabel(lecture.transform, "Header").text, Is.EqualTo("ENGLISH"));
            RunToCompletion(lecture.ClaimMilestoneForTesting(90));
            Assert.That(ClassroomLectureUI.IsOpen, Is.False);
            Assert.That(movement.enabled, Is.True);
            Assert.That(look.enabled, Is.True);
            Assert.That(interaction.enabled, Is.True);
            Assert.That(Cursor.visible, Is.False);
        }

        [Test]
        public void EarlyLeave_UpdatesOnlyThatCourseOnTheHud()
        {
            BindThreeCourses();
            PlayerStats stats = playerObject.GetComponent<PlayerStats>();
            ShowHud();

            stats.ApplyServerState(50f, 50f, StatUpdateSource.InitialHydration);
            Assert.That(ApplyLeave(ActivityIds.AttendIcs, 45f), Is.True);
            AssertLeftEarly("Ics", "Attend Introduction to Computer Science — Left Early");
            AssertPending("ATTEND_ENGLISH", "Attend English");
            AssertPending("ATTEND_DM", "Attend Discrete Mathematics");
            Assert.That(stats.AcademicReputation, Is.EqualTo(45f));

            dailyActivityState.SetAttendIcsStatusForTesting(ActivityStatus.Pending);
            stats.ApplyServerState(50f, 58f, StatUpdateSource.InitialHydration);
            Assert.That(ApplyLeave(ActivityIds.AttendEnglish, 53f), Is.True);
            AssertLeftEarly("ATTEND_ENGLISH", "Attend English — Left Early");
            AssertPendingMarker("Ics");
            AssertPending("ATTEND_DM", "Attend Discrete Mathematics");
            Assert.That(FindLabel(statsHud.transform, "AcademicText").text, Is.EqualTo("ACADEMIC: 53"));
            Assert.That(FindLabel(statsHud.transform, "IdCardMarker").text, Is.EqualTo("[x]"));
            Assert.That(FindLabel(statsHud.transform, "BreakfastMarker").text, Is.EqualTo("[ ]"));

            dailyActivityState.SetClassroomStatusForTesting(ActivityIds.AttendEnglish, ActivityStatus.Pending);
            stats.ApplyServerState(50f, 50f, StatUpdateSource.InitialHydration);
            Assert.That(ApplyLeave(ActivityIds.AttendDm, 45f), Is.True);
            AssertLeftEarly("ATTEND_DM", "Attend Discrete Mathematics — Left Early");
            AssertPending("ATTEND_ENGLISH", "Attend English");
            AssertPendingMarker("Ics");
            Assert.That(dailyActivityState.GetClassroomOutcome(ActivityIds.AttendEnglish), Is.Not.EqualTo("LEFT_EARLY"));
            Assert.That(dailyActivityState.AttendIcsOutcome, Is.Not.EqualTo("LEFT_EARLY"));
        }

        [Test]
        public void FullAttendance_TurnsOnlyThatObjectiveGreen()
        {
            BindThreeCourses();
            ShowHud();
            PlayerProgressSync sync = playerObject.AddComponent<PlayerProgressSync>();
            Assert.That(sync.ApplySessionResponseForTesting(SessionJson(
                ActivityIds.AttendEnglish, "COMPLETED", "COMPLETED", 90, 0, 12, 62)), Is.True);

            TextMeshProUGUI marker = FindLabel(statsHud.transform, "ATTEND_ENGLISHMarker");
            TextMeshProUGUI title = FindLabel(statsHud.transform, "ATTEND_ENGLISHTitle");
            Assert.That(marker.text, Is.EqualTo("[x]"));
            Assert.That(marker.color, Is.EqualTo(UiTheme.Success));
            Assert.That(title.text, Is.EqualTo("Attend English"));
            Assert.That(title.color, Is.EqualTo(UiTheme.Success));
            AssertPendingMarker("Ics");
            AssertPending("ATTEND_DM", "Attend Discrete Mathematics");
            Assert.That(FindLabel(statsHud.transform, "AcademicText").text, Is.EqualTo("ACADEMIC: 62"));
        }

        [Test]
        public void Proxy_KeepsDistinctLabel_AndDoesNotLookLikeFullAttendance()
        {
            BindThreeCourses();
            ShowHud();
            PlayerProgressSync sync = playerObject.AddComponent<PlayerProgressSync>();
            Assert.That(sync.ApplySessionResponseForTesting(SessionJson(
                ActivityIds.AttendEnglish, "COMPLETED", "PROXY", 0, 5, 0, 50)), Is.True);

            TextMeshProUGUI marker = FindLabel(statsHud.transform, "ATTEND_ENGLISHMarker");
            TextMeshProUGUI title = FindLabel(statsHud.transform, "ATTEND_ENGLISHTitle");
            TextMeshProUGUI description = FindLabel(statsHud.transform, "ATTEND_ENGLISHDescription");
            Assert.That(marker.text, Is.EqualTo("[~]"));
            Assert.That(marker.color, Is.EqualTo(UiTheme.BrightOrange));
            Assert.That(title.color, Is.EqualTo(UiTheme.BrightOrange));
            Assert.That(description.gameObject.activeSelf, Is.True);
            Assert.That(description.text, Is.EqualTo("Proxy — Punched ID and Left"));
            Assert.That(marker.text, Is.Not.EqualTo("[x]"));
            AssertPendingMarker("Ics");
            AssertPending("ATTEND_DM", "Attend Discrete Mathematics");
        }

        [Test]
        public void ResolvedClassroom_StaysOnHud_AfterFloorUnloads_AndContinueHydratesIt()
        {
            BindThreeCourses();
            ShowHud();
            Assert.That(ApplyLeave(ActivityIds.AttendEnglish, 53f), Is.True);
            AssertLeftEarly("ATTEND_ENGLISH", "Attend English — Left Early");

            UnityEngine.Object.DestroyImmediate(classroomObject);
            classroomObject = null;
            classroom = null;
            ShowHud();
            AssertLeftEarly("ATTEND_ENGLISH", "Attend English — Left Early");

            dailyActivityState.ResetForNewGame();
            saveState.SetDayProgressForTesting(1, 1);
            saveState.SetIdentityForTesting("STUDENT", "CSE", true);
            dailyActivityState.SetGetIdCardStatusForTesting(ActivityStatus.Completed, "COMPLETED");
            dailyActivityState.ApplyServerActivity(new ActivityRecord(
                ActivityIds.AttendEnglish,
                ActivityStatus.Missed,
                "LEFT_EARLY",
                0,
                3,
                1,
                60));
            playerObject.GetComponent<PlayerStats>().ApplyServerState(50f, 53f, StatUpdateSource.InitialHydration);
            AssertLeftEarly("ATTEND_ENGLISH", "Attend English — Left Early");
            Assert.That(FindLabel(statsHud.transform, "AcademicText").text, Is.EqualTo("ACADEMIC: 53"));
        }

        [Test]
        public void EndDayReplay_DoesNotPenalizeAResolvedClassAgain()
        {
            BindThreeCourses();
            ShowHud();
            PlayerStats stats = playerObject.GetComponent<PlayerStats>();
            Assert.That(ApplyLeave(ActivityIds.AttendEnglish, 53f), Is.True);
            Assert.That(ApplyLeave(ActivityIds.AttendEnglish, 53f), Is.True);
            Assert.That(stats.AcademicReputation, Is.EqualTo(53f));
            Assert.That(dailyActivityState.GetClassroomOutcome(ActivityIds.AttendEnglish), Is.EqualTo("LEFT_EARLY"));

            dailyActivityState.ApplyServerActivity(new ActivityRecord(
                ActivityIds.AttendDm, ActivityStatus.Missed, "SKIPPED", 0, -5, 1));
            Assert.That(stats.AcademicReputation, Is.EqualTo(53f));
            AssertLeftEarly("ATTEND_DM", "Attend Discrete Mathematics — Missed");
            AssertLeftEarly("ATTEND_ENGLISH", "Attend English — Left Early");
        }

        [Test]
        public void FailedSessionResponse_DoesNotChangeReputationOrObjective()
        {
            BindThreeCourses();
            ShowHud();
            PlayerStats stats = playerObject.GetComponent<PlayerStats>();
            stats.ApplyServerState(50f, 58f, StatUpdateSource.InitialHydration);
            PlayerProgressSync sync = playerObject.AddComponent<PlayerProgressSync>();

            Assert.That(sync.ApplySessionResponseForTesting("{"), Is.False);
            Assert.That(sync.ApplySessionResponseForTesting(""), Is.False);
            Assert.That(stats.AcademicReputation, Is.EqualTo(58f));
            Assert.That(dailyActivityState.IsClassroomResolved(ActivityIds.AttendEnglish), Is.False);
            AssertPending("ATTEND_ENGLISH", "Attend English");
            Assert.That(FindLabel(statsHud.transform, "IdCardMarker").text, Is.EqualTo("[x]"));
            Assert.That(FindLabel(statsHud.transform, "BreakfastMarker").text, Is.EqualTo("[ ]"));
        }

        [Test]
        public void Door_ReadsTheVisibleHudPlayer_NotAStrayActivityState()
        {
            GameObject doorObject = new GameObject("EnglishDoor");
            doorObject.AddComponent<BoxCollider>();
            IcsClassroomInteractable door = doorObject.AddComponent<IcsClassroomInteractable>();
            SetCourseFields(door, ActivityIds.AttendEnglish, "ENGLISH", "English", "702", 7);

            GameObject stray = new GameObject("StrayActivities");
            DailyActivityState strayState = stray.AddComponent<DailyActivityState>();
            strayState.ApplyServerActivity(new ActivityRecord(
                ActivityIds.AttendEnglish, ActivityStatus.Pending, string.Empty, 0, 0, 1));

            dailyActivityState.ApplyServerActivity(new ActivityRecord(
                ActivityIds.AttendEnglish, ActivityStatus.Missed, "LEFT_EARLY", 0, 3, 1, 60));

            Assert.That(door.Interact(), Is.EqualTo("You already left this class early today."));
            UnityEngine.Object.DestroyImmediate(doorObject);
            UnityEngine.Object.DestroyImmediate(stray);
        }

        [Test]
        public void DuplicateFloorPlayer_IsDisabled_SoItCannotOwnTheHud()
        {
            GameObject floorPlayer = new GameObject("Floor07Player");
            floorPlayer.AddComponent<PlayerStats>();
            floorPlayer.AddComponent<DailyActivityState>();
            floorPlayer.AddComponent<StatsHUD>();

            Assert.That(floorPlayer.activeInHierarchy, Is.False);
            Assert.That(StatsHUD.Instance, Is.SameAs(statsHud));
            UnityEngine.Object.DestroyImmediate(floorPlayer);
        }

        private bool ApplyLeave(string activityId, float academicReputation)
        {
            PlayerProgressSync sync = playerObject.GetComponent<PlayerProgressSync>();
            if (sync == null)
            {
                sync = playerObject.AddComponent<PlayerProgressSync>();
            }

            return sync.ApplySessionResponseForTesting(SessionJson(
                activityId, "MISSED", "LEFT_EARLY", 60, 0, 3, academicReputation));
        }

        private void BindThreeCourses()
        {
            DestroyClassroomWithoutCache();
            locationConfigAsset = CreateLocationConfig("427", 4);
            englishConfig = CreateCourseConfig(
                ActivityIds.AttendEnglish, "ENGLISH", "English", "702", 7);
            dmConfig = CreateCourseConfig(
                ActivityIds.AttendDm, "DM", "Discrete Mathematics", "423", 4);
            dailyActivityState.SetLocationConfigForTesting(locationConfigAsset);
            dailyActivityState.SetAdditionalLocationConfigsForTesting(new[] { englishConfig, dmConfig });
        }

        private void ShowHud()
        {
            statsHud.enabled = false;
            statsHud.enabled = true;
        }

        private void AssertLeftEarly(string namePrefix, string title)
        {
            TextMeshProUGUI marker = FindLabel(statsHud.transform, namePrefix + "Marker");
            TextMeshProUGUI titleLabel = FindLabel(statsHud.transform, namePrefix + "Title");
            Assert.That(marker.text, Is.EqualTo("[X]"));
            Assert.That(marker.color, Is.EqualTo(UiTheme.Danger));
            Assert.That(titleLabel.text, Is.EqualTo(title));
            Assert.That(titleLabel.color, Is.EqualTo(UiTheme.Danger));
        }

        private void AssertPending(string namePrefix, string title)
        {
            TextMeshProUGUI marker = FindLabel(statsHud.transform, namePrefix + "Marker");
            TextMeshProUGUI titleLabel = FindLabel(statsHud.transform, namePrefix + "Title");
            Assert.That(marker.text, Is.EqualTo("[ ]"));
            Assert.That(titleLabel.text, Is.EqualTo(title));
            Assert.That(titleLabel.color, Is.EqualTo(UiTheme.White));
        }

        private void AssertPendingMarker(string namePrefix)
        {
            Assert.That(FindLabel(statsHud.transform, namePrefix + "Marker").text, Is.EqualTo("[ ]"));
        }

        private static string SessionJson(
            string activityId,
            string status,
            string outcome,
            int milestoneSeconds,
            int auraDelta,
            int reputationDelta,
            float academicReputation)
        {
            return "{"
                + "\"activityId\":\"" + activityId + "\","
                + "\"status\":\"" + status + "\","
                + "\"outcome\":\"" + outcome + "\","
                + "\"milestoneSeconds\":" + milestoneSeconds + ","
                + "\"auraDelta\":" + auraDelta + ","
                + "\"reputationDelta\":" + reputationDelta + ","
                + "\"requestedAuraDelta\":0,"
                + "\"requestedReputationDelta\":0,"
                + "\"appliedAuraDelta\":" + auraDelta + ","
                + "\"appliedReputationDelta\":" + reputationDelta + ","
                + "\"alreadyApplied\":false,"
                + "\"sessionActive\":false,"
                + "\"activeElapsedMs\":60000,"
                + "\"dayNumber\":1,"
                + "\"aura\":50,"
                + "\"academicReputation\":" + academicReputation.ToString("0")
                + "}";
        }

        private static void RunToCompletion(IEnumerator routine)
        {
            int guard = 0;
            while (routine.MoveNext())
            {
                guard++;
                Assert.That(guard, Is.LessThan(10000), "Coroutine did not finish");
            }
        }

        private static void SetCourseFields(
            IcsClassroomInteractable target,
            string activityId,
            string courseId,
            string courseName,
            string room,
            int floor)
        {
            var type = typeof(IcsClassroomInteractable);
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            type.GetField("activityId", flags)?.SetValue(target, activityId);
            type.GetField("courseId", flags)?.SetValue(target, courseId);
            type.GetField("courseName", flags)?.SetValue(target, courseName);
            type.GetField("classroomNumber", flags)?.SetValue(target, room);
            type.GetField("floor", flags)?.SetValue(target, floor);
            type.GetField("departmentCode", flags)?.SetValue(target, "CSE");
            type.GetField("studentRole", flags)?.SetValue(target, "STUDENT");
            type.GetField("scheduledGameplayDay", flags)?.SetValue(target, 1);
        }

        private static IcsClassroomLocationConfig CreateCourseConfig(
            string activityId,
            string courseId,
            string courseName,
            string room,
            int floor)
        {
            IcsClassroomLocationConfig config = CreateLocationConfig(room, floor);
            var type = typeof(IcsClassroomLocationConfig);
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            type.GetField("activityId", flags)?.SetValue(config, activityId);
            type.GetField("courseId", flags)?.SetValue(config, courseId);
            type.GetField("courseName", flags)?.SetValue(config, courseName);
            return config;
        }

        private static void SetClassroomConfig(IcsClassroomInteractable target, string room, int floor)
        {
            var type = typeof(IcsClassroomInteractable);
            type.GetField("classroomNumber", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(target, room);
            type.GetField("floor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(target, floor);

            DailyActivityState activities = UnityEngine.Object.FindFirstObjectByType<DailyActivityState>();
            activities?.RegisterClassroom(target);
        }

        private void DestroyClassroomWithoutCache()
        {
            if (classroomObject != null)
            {
                UnityEngine.Object.DestroyImmediate(classroomObject);
                classroomObject = null;
                classroom = null;
            }

            dailyActivityState.IcsClassroom = null;
        }

        private static IcsClassroomLocationConfig CreateLocationConfig(string room, int floor)
        {
            IcsClassroomLocationConfig config = ScriptableObject.CreateInstance<IcsClassroomLocationConfig>();
            var type = typeof(IcsClassroomLocationConfig);
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            type.GetField("classroomNumber", flags)?.SetValue(config, room);
            type.GetField("floor", flags)?.SetValue(config, floor);
            type.GetField("courseName", flags)?.SetValue(config, "Introduction to Computer Science");
            type.GetField("departmentCode", flags)?.SetValue(config, "CSE");
            type.GetField("scheduledGameplayDay", flags)?.SetValue(config, 1);
            return config;
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

        private sealed class FakeAttendIcsSync : IAttendIcsProgressSync
        {
            private readonly Queue<Action<Action<AttendIcsSessionResult>, Action>> milestoneQueue =
                new Queue<Action<Action<AttendIcsSessionResult>, Action>>();

            public bool IsHydrated => true;
            public bool IsMutationInFlight => false;
            public int MilestoneRequestCount { get; private set; }

            public void QueueMilestoneSuccess(
                int milestoneSeconds,
                ActivityStatus status,
                string outcome,
                int appliedRep,
                int totalRepDelta)
            {
                milestoneQueue.Enqueue((onSuccess, _) =>
                {
                    var record = new ActivityRecord(
                        ActivityIds.AttendIcs,
                        status,
                        outcome,
                        0,
                        totalRepDelta,
                        1,
                        milestoneSeconds);
                    onSuccess(new AttendIcsSessionResult(
                        record,
                        false,
                        status != ActivityStatus.Completed,
                        milestoneSeconds * 1000L,
                        0,
                        4,
                        0,
                        appliedRep,
                        50f,
                        50f + totalRepDelta));
                });
            }

            public void QueueMilestoneFailure()
            {
                milestoneQueue.Enqueue((_, onFailure) => onFailure());
            }

            public void RequestAttendIcsStart(Action<AttendIcsSessionResult> onSuccess, Action onFailure)
            {
                onFailure?.Invoke();
            }

            public void RequestAttendIcsPause(Action<AttendIcsSessionResult> onSuccess, Action onFailure)
            {
                onSuccess?.Invoke(default);
            }

            public void RequestAttendIcsResume(Action<AttendIcsSessionResult> onSuccess, Action onFailure)
            {
                onSuccess?.Invoke(default);
            }

            public void RequestAttendIcsMilestone(
                int milestoneSeconds,
                Action<AttendIcsSessionResult> onSuccess,
                Action onFailure)
            {
                MilestoneRequestCount++;
                if (milestoneQueue.Count == 0)
                {
                    onFailure?.Invoke();
                    return;
                }

                milestoneQueue.Dequeue().Invoke(onSuccess, onFailure);
            }

            public void RequestAttendIcsLeaveEarly(Action<AttendIcsSessionResult> onSuccess, Action onFailure)
            {
                onFailure?.Invoke();
            }

            public void RequestAttendIcsProxy(Action<AttendIcsSessionResult> onSuccess, Action onFailure)
            {
                onFailure?.Invoke();
            }
        }
    }
}
