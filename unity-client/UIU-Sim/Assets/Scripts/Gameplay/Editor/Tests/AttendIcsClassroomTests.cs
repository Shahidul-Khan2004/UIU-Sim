using NUnit.Framework;
using TMPro;
using UIU.Simulator.Gameplay.Activities;
using UIU.Simulator.Gameplay.Classroom;
using UIU.Simulator.Gameplay.Player;
using UIU.Simulator.Gameplay.UI;
using UIU.Simulator.UI;
using UnityEngine;

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
