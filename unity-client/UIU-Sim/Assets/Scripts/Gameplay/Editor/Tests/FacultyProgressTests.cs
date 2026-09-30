using NUnit.Framework;
using TMPro;
using UIU.Simulator.Gameplay.Classroom;
using UIU.Simulator.Gameplay.Faculty;
using UIU.Simulator.Gameplay.IDCard;
using UIU.Simulator.Gameplay.Player;
using UIU.Simulator.Gameplay.UI;
using UIU.Simulator.Networking;
using UIU.Simulator.UI;
using UnityEngine;

namespace UIU.Simulator.Gameplay.Editor.Tests
{
    [TestFixture]
    public sealed class FacultyProgressTests
    {
        private GameObject playerObject;
        private PlayerSaveState saveState;
        private StatsHUD statsHud;
        private FacultyProgress facultyProgress;
        private FacultyHUD facultyHud;
        private GameObject classroomObject;
        private IcsClassroomInteractable classroom;

        [SetUp]
        public void SetUp()
        {
            DestroyExisting(PlayerSaveState.Instance != null ? PlayerSaveState.Instance.gameObject : null);
            DestroyExisting(FacultyProgress.Instance != null ? FacultyProgress.Instance.gameObject : null);
            DestroyExisting(FacultyHUD.Instance != null ? FacultyHUD.Instance.gameObject : null);
            DestroyExisting(FacultyAssignedSchedule.Instance != null ? FacultyAssignedSchedule.Instance.gameObject : null);
            DestroyExisting(FacultyProgressSync.Instance != null ? FacultyProgressSync.Instance.gameObject : null);
            if (WelcomeCardUI.Instance != null)
            {
                Object.DestroyImmediate(WelcomeCardUI.Instance.gameObject);
            }

            DestroyExisting(StatsHUD.Instance != null ? StatsHUD.Instance.gameObject : null);
            DestroyExisting(FacultyClassroomChoiceUI.Instance != null ? FacultyClassroomChoiceUI.Instance.gameObject : null);
            DestroyExisting(FacultyLectureUI.Instance != null ? FacultyLectureUI.Instance.gameObject : null);

            playerObject = new GameObject("FacultyProgressTestPlayer");
            playerObject.AddComponent<PlayerStats>();
            statsHud = playerObject.AddComponent<StatsHUD>();
            saveState = playerObject.AddComponent<PlayerSaveState>();
            facultyProgress = playerObject.AddComponent<FacultyProgress>();
            facultyHud = playerObject.AddComponent<FacultyHUD>();

            classroomObject = new GameObject("FacultyClassroom");
            classroomObject.AddComponent<BoxCollider>();
            classroom = classroomObject.AddComponent<IcsClassroomInteractable>();
        }

        [TearDown]
        public void TearDown()
        {
            if (FacultyClassroomChoiceUI.IsOpen && FacultyClassroomChoiceUI.Instance != null)
            {
                FacultyClassroomChoiceUI.Instance.Hide();
            }

            if (FacultyLectureUI.Instance != null)
            {
                Object.DestroyImmediate(FacultyLectureUI.Instance.gameObject);
            }

            DestroyExisting(classroomObject);
            DestroyExisting(playerObject);
            DestroyExisting(WelcomeCardUI.Instance != null ? WelcomeCardUI.Instance.gameObject : null);
            DestroyExisting(FacultyClassroomChoiceUI.Instance != null ? FacultyClassroomChoiceUI.Instance.gameObject : null);
            DestroyExisting(FacultyLectureUI.Instance != null ? FacultyLectureUI.Instance.gameObject : null);
        }

        [Test]
        public void FacultyHud_FreshFaculty_Shows50AndAllObjectivesIncomplete()
        {
            ApplyFacultySave();
            facultyProgress.ResetToDefaults();
            facultyHud.ApplyRoleVisibility();

            Assert.That(FindLabel(facultyHud.transform, "ReputationValue").text, Is.EqualTo("50"));
            AssertObjective(facultyHud, "IdCard", complete: false);
            AssertObjective(facultyHud, "Office", complete: false);
            AssertObjective(facultyHud, "Materials", complete: false);
            AssertObjective(facultyHud, "Course0", complete: false);
            AssertObjective(facultyHud, "Course1", complete: false);
        }

        [Test]
        public void FacultyHud_OnlyIdCardTurnsGreen_AfterIdIssued()
        {
            ApplyFacultySave();
            facultyProgress.SetStateForTesting(50, facultyIdIssuedValue: true);
            facultyHud.ApplyRoleVisibility();

            AssertObjective(facultyHud, "IdCard", complete: true);
            AssertObjective(facultyHud, "Office", complete: false);
            AssertObjective(facultyHud, "Materials", complete: false);
            AssertObjective(facultyHud, "Course0", complete: false);
            AssertObjective(facultyHud, "Course1", complete: false);
        }

        [Test]
        public void FacultyHud_OnlyOfficeTurnsGreen_AfterOfficeSetup()
        {
            ApplyFacultySave();
            facultyProgress.SetStateForTesting(
                55,
                computerUsedValue: true,
                officeEnteredValue: true,
                facultyIdIssuedValue: true);
            facultyHud.ApplyRoleVisibility();

            AssertObjective(facultyHud, "IdCard", complete: true);
            AssertObjective(facultyHud, "Office", complete: true);
            AssertObjective(facultyHud, "Materials", complete: false);
            AssertObjective(facultyHud, "Course0", complete: false);
            AssertObjective(facultyHud, "Course1", complete: false);
        }

        [Test]
        public void FacultyHud_OnlyMaterialsTurnGreen_AfterMaterialsPrepared()
        {
            ApplyFacultySave();
            facultyProgress.SetStateForTesting(
                60,
                computerUsedValue: true,
                officeEnteredValue: true,
                icsPreparedValue: true,
                dmPreparedValue: true,
                facultyIdIssuedValue: true);
            facultyHud.ApplyRoleVisibility();

            AssertObjective(facultyHud, "IdCard", complete: true);
            AssertObjective(facultyHud, "Office", complete: true);
            AssertObjective(facultyHud, "Materials", complete: true);
            AssertObjective(facultyHud, "Course0", complete: false);
            AssertObjective(facultyHud, "Course1", complete: false);
        }

        [Test]
        public void FacultyHud_IcsGreenDmOrange_AfterIcsCompleted()
        {
            ApplyFacultySave();
            facultyProgress.SetStateForTesting(
                65,
                computerUsedValue: true,
                officeEnteredValue: true,
                icsPreparedValue: true,
                dmPreparedValue: true,
                icsCompletedValue: true,
                facultyIdIssuedValue: true);
            facultyHud.ApplyRoleVisibility();

            AssertObjective(facultyHud, "Course0", complete: true);
            AssertObjective(facultyHud, "Course1", complete: false);
        }

        [Test]
        public void VisitorWithoutIdCard_HidesStudentAndFacultyHuds()
        {
            saveState.SetStateForTesting(hasSaveValue: false, idCardIssuedValue: false);
            facultyHud.ApplyRoleVisibility();

            Assert.That(facultyHud.IsVisible, Is.False);
            Assert.That(statsHud.IsVisible, Is.False);
        }

        [Test]
        public void FacultyHud_AppearsForFaculty_AndHidesStudentHud()
        {
            ApplyFacultySave();
            facultyHud.ApplyRoleVisibility();

            Assert.That(facultyHud.IsVisible, Is.True);
            Assert.That(statsHud.IsVisible, Is.False);
            Assert.That(FindLabel(facultyHud.transform, "ReputationHeader").text, Is.EqualTo("FACULTY REPUTATION"));
            Assert.That(FindLabel(facultyHud.transform, "ReputationValue").text, Is.EqualTo("50"));
            Assert.That(FindLabel(facultyHud.transform, "IdCardTitle").text, Is.EqualTo("Faculty ID Card"));
            Assert.That(FindLabel(facultyHud.transform, "OfficeTitle").text, Is.EqualTo("Setup Faculty Office"));
            Assert.That(FindLabel(facultyHud.transform, "MaterialsTitle").text, Is.EqualTo("Prepare Course Materials"));
            Assert.That(FindLabel(facultyHud.transform, "Course0Title").text, Is.EqualTo("Introduction to Computer Science - Room 427"));
            Assert.That(FindLabel(facultyHud.transform, "Course1Title").text, Is.EqualTo("Discrete Mathematics - Room 423"));
            Assert.That(FindLabel(facultyHud.transform, "TeachTitle"), Is.Null);
            Assert.That(HudContains(facultyHud, "Teach First Class"), Is.False);
            Assert.That(FindLabel(facultyHud.transform, "AuraText"), Is.Null);
            Assert.That(FindLabel(facultyHud.transform, "AcademicText"), Is.Null);
        }

        [Test]
        public void StudentHud_RemainsUnchanged_WhenRoleIsStudent()
        {
            ApplyStudentSave();
            facultyHud.ApplyRoleVisibility();

            Assert.That(facultyHud.IsVisible, Is.False);
            Assert.That(statsHud.IsVisible, Is.True);
            Assert.That(FindLabel(statsHud.transform, "AuraText"), Is.Not.Null);
            Assert.That(FindLabel(statsHud.transform, "AcademicText"), Is.Not.Null);
        }

        [Test]
        public void FacultyComputerReward_AppliesOnceOnServerState()
        {
            ApplyFacultySave();
            facultyHud.ApplyRoleVisibility();

            facultyProgress.SetStateForTesting(55, computerUsedValue: true, officeEnteredValue: true, source: FacultyUpdateSource.GameplayMutation);

            Assert.That(facultyProgress.Reputation, Is.EqualTo(55));
            Assert.That(facultyProgress.ComputerUsed, Is.True);
            Assert.That(FindLabel(facultyHud.transform, "ReputationValue").text, Is.EqualTo("55"));
            Assert.That(FindLabel(facultyHud.transform, "ReputationFeedback").text, Is.EqualTo("+5 Reputation"));
            Assert.That(FindLabel(facultyHud.transform, "OfficeMarker").text, Is.EqualTo("✓"));
        }

        [Test]
        public void FacultyHud_ShowsNextAssignedClassroom_BeforeFirstLecture()
        {
            ApplyFacultySave();
            facultyProgress.SetStateForTesting(
                50,
                computerUsedValue: true,
                officeEnteredValue: true,
                icsPreparedValue: true,
                dmPreparedValue: true,
                facultyIdIssuedValue: true);
            facultyHud.ApplyRoleVisibility();

            TextMeshProUGUI current = FindLabel(facultyHud.transform, "CurrentObjective");
            TextMeshProUGUI next = FindLabel(facultyHud.transform, "NextActivity");
            Assert.That(FindLabel(facultyHud.transform, "ReputationValue").text, Is.EqualTo("50"));
            Assert.That(current.text, Does.Contain("Go to Introduction to Computer Science"));
            Assert.That(current.text, Does.Contain("Room: 427"));
            Assert.That(current.text, Does.Contain("Floor: 4"));
            Assert.That(next.text, Does.Contain("Teach Introduction to Computer Science"));
            Assert.That(current.text, Does.Not.Contain("Teach First Class"));
            Assert.That(next.text, Does.Not.Contain("Teach First Class"));
            Assert.That(FindLabel(facultyHud.transform, "Course0Marker").text, Is.EqualTo("○"));
            Assert.That(FindLabel(facultyHud.transform, "Course1Marker").text, Is.EqualTo("○"));
        }

        [Test]
        public void FacultyHud_AfterIcsCompletion_ShowsDiscreteMathematicsClassroom()
        {
            ApplyFacultySave();
            facultyProgress.SetStateForTesting(
                65,
                computerUsedValue: true,
                officeEnteredValue: true,
                classroomScannedValue: true,
                lectureCompletedValue: false,
                icsPreparedValue: true,
                dmPreparedValue: true,
                icsCompletedValue: true,
                facultyIdIssuedValue: true);
            facultyHud.ApplyRoleVisibility();

            TextMeshProUGUI current = FindLabel(facultyHud.transform, "CurrentObjective");
            TextMeshProUGUI next = FindLabel(facultyHud.transform, "NextActivity");
            Assert.That(FindLabel(facultyHud.transform, "ReputationValue").text, Is.EqualTo("65"));
            Assert.That(current.text, Does.Contain("Go to Discrete Mathematics"));
            Assert.That(current.text, Does.Contain("Room: 423"));
            Assert.That(current.text, Does.Contain("Floor: 4"));
            Assert.That(next.text, Does.Contain("Teach Discrete Mathematics"));
            Assert.That(HudContains(facultyHud, "Teach First Class"), Is.False);
            Assert.That(FindLabel(facultyHud.transform, "Course0Marker").text, Is.EqualTo("✓"));
            Assert.That(FindLabel(facultyHud.transform, "Course1Marker").text, Is.EqualTo("○"));
        }

        [Test]
        public void FacultyHud_AfterAllAssignedClasses_ShowsAllClassesCompleted()
        {
            ApplyFacultySave();
            facultyProgress.SetStateForTesting(
                70,
                computerUsedValue: true,
                officeEnteredValue: true,
                classroomScannedValue: true,
                lectureCompletedValue: true,
                icsPreparedValue: true,
                dmPreparedValue: true,
                icsCompletedValue: true,
                dmCompletedValue: true,
                facultyIdIssuedValue: true);
            facultyHud.ApplyRoleVisibility();

            Assert.That(FindLabel(facultyHud.transform, "CurrentObjective").text, Does.Contain("All classes completed."));
            Assert.That(FindLabel(facultyHud.transform, "ReputationValue").text, Is.EqualTo("70"));
            Assert.That(FindLabel(facultyHud.transform, "NextActivity").text, Does.Contain("—"));
            Assert.That(FindLabel(facultyHud.transform, "Course0Marker").text, Is.EqualTo("✓"));
            Assert.That(FindLabel(facultyHud.transform, "Course1Marker").text, Is.EqualTo("✓"));
            Assert.That(FindLabel(facultyHud.transform, "OptionalHeader").text, Is.EqualTo("OPTIONAL"));
            Assert.That(FindLabel(facultyHud.transform, "OptionalHeader").gameObject.activeInHierarchy, Is.True);
            Assert.That(FindLabel(facultyHud.transform, "CoffeeTitle").text, Is.EqualTo("Get Coffee"));
            Assert.That(FindLabel(facultyHud.transform, "CoffeeMarker").text, Is.EqualTo("○"));
            Assert.That(FindLabel(facultyHud.transform, "CoffeeMarker").color, Is.EqualTo(UiTheme.Grey));
            Assert.That(FindLabel(facultyHud.transform, "CurrentObjective").text, Does.Not.Contain("Coffee"));
            Assert.That(FindLabel(facultyHud.transform, "NextActivity").text, Does.Not.Contain("Coffee"));
        }

        [Test]
        public void FacultyHud_OptionalCoffeeHidden_BeforeBothClassesComplete()
        {
            ApplyFacultySave();
            facultyProgress.SetStateForTesting(
                65,
                computerUsedValue: true,
                officeEnteredValue: true,
                icsPreparedValue: true,
                dmPreparedValue: true,
                icsCompletedValue: true,
                dmCompletedValue: false,
                facultyIdIssuedValue: true);
            facultyHud.ApplyRoleVisibility();

            TextMeshProUGUI optionalHeader = FindLabel(facultyHud.transform, "OptionalHeader");
            Assert.That(optionalHeader, Is.Not.Null);
            Assert.That(optionalHeader.gameObject.activeInHierarchy, Is.False);
        }

        [Test]
        public void FacultyHud_OptionalCoffeeCompletesWithoutChangingCurrentObjective()
        {
            ApplyFacultySave();
            facultyProgress.SetStateForTesting(
                71,
                computerUsedValue: true,
                officeEnteredValue: true,
                classroomScannedValue: true,
                lectureCompletedValue: true,
                icsPreparedValue: true,
                dmPreparedValue: true,
                icsCompletedValue: true,
                dmCompletedValue: true,
                facultyIdIssuedValue: true,
                coffeeClaimedForCurrentDayValue: true,
                coffeeOptionValue: "CAPPUCCINO",
                source: FacultyUpdateSource.GameplayMutation);
            facultyHud.ApplyRoleVisibility();

            Assert.That(FindLabel(facultyHud.transform, "CurrentObjective").text, Does.Contain("All classes completed."));
            Assert.That(FindLabel(facultyHud.transform, "NextActivity").text, Does.Contain("—"));
            Assert.That(FindLabel(facultyHud.transform, "CoffeeMarker").text, Is.EqualTo("✓"));
            Assert.That(FindLabel(facultyHud.transform, "CoffeeMarker").color, Is.EqualTo(UiTheme.Success));
            Assert.That(FindLabel(facultyHud.transform, "CoffeeTitle").color, Is.EqualTo(UiTheme.Success));
            Assert.That(FindLabel(facultyHud.transform, "ReputationValue").text, Is.EqualTo("71"));
        }

        [Test]
        public void FacultyClassroom_AfterIcs_StillOpensDiscreteMathematicsChoiceUi()
        {
            ApplyFacultySave();
            facultyProgress.SetStateForTesting(
                65,
                computerUsedValue: true,
                officeEnteredValue: true,
                classroomScannedValue: true,
                icsPreparedValue: true,
                dmPreparedValue: true,
                icsCompletedValue: true);
            SetClassroomConfig(classroom, "423", 4);
            typeof(IcsClassroomInteractable)
                .GetField("courseId", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(classroom, "DM");
            typeof(IcsClassroomInteractable)
                .GetField("courseName", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(classroom, "Discrete Mathematics");

            string response = classroom.Interact();

            Assert.That(response, Is.Null);
            Assert.That(FacultyClassroomChoiceUI.IsOpen, Is.True);
            Assert.That(FindLabel(FacultyClassroomChoiceUI.Instance.transform, "Title").text, Is.EqualTo("DISCRETE MATHEMATICS"));
            Assert.That(FindLabel(FacultyClassroomChoiceUI.Instance.transform, "Room").text, Does.Contain("423"));
        }

        [Test]
        public void FacultyClassroom_OpensFacultyChoiceUi()
        {
            ApplyFacultySave();
            SetClassroomConfig(classroom, "427", 4);

            string response = classroom.Interact();

            Assert.That(response, Is.Null);
            Assert.That(FacultyClassroomChoiceUI.IsOpen, Is.True);
            Assert.That(FindLabel(FacultyClassroomChoiceUI.Instance.transform, "Title").text, Is.EqualTo("INTRODUCTION TO COMPUTER SCIENCE"));
            Assert.That(FindLabel(FacultyClassroomChoiceUI.Instance.transform, "Room").text, Does.Contain("427"));
            Assert.That(FindNamedChild(FacultyClassroomChoiceUI.Instance.transform, "ScanButton"), Is.Not.Null);
            Assert.That(FindNamedChild(FacultyClassroomChoiceUI.Instance.transform, "AttendButton"), Is.Null);
        }

        [Test]
        public void FacultyLecture_TimerUsesNinetySeconds()
        {
            FacultyLectureUI lecture = FacultyLectureUI.EnsureExists();
            lecture.BeginTeachingForTesting("Introduction to Computer Science", 90f);

            Assert.That(FacultyLectureUI.IsOpen, Is.True);
            Assert.That(lecture.IsLecturePanelActiveForTesting, Is.True);
            Assert.That(lecture.DurationSecondsForTesting, Is.EqualTo(90f));
            Assert.That(lecture.TimerTextForTesting, Is.EqualTo("0 / 90 min"));
            Assert.That(FindLabel(lecture.transform, "Header").text, Is.EqualTo("INTRODUCTION TO COMPUTER SCIENCE"));
            Assert.That(FindNamedChild(lecture.transform, "LeaveButton"), Is.Not.Null);
        }

        private void ApplyFacultySave()
        {
            saveState.ApplyCreatedSave(new ApiClient.PlayerSaveStatusDto
            {
                hasSave = true,
                save = new ApiClient.PlayerSaveDto
                {
                    role = FacultyIdentity.Role,
                    playerName = "Lail",
                    department = "CSE",
                    universityId = "F-001",
                    admissionCompleted = true,
                    idCardIssued = true,
                    semester = 1,
                    currentDay = 1
                }
            });
        }

        private void ApplyStudentSave()
        {
            saveState.ApplyCreatedSave(new ApiClient.PlayerSaveStatusDto
            {
                hasSave = true,
                save = new ApiClient.PlayerSaveDto
                {
                    role = "STUDENT",
                    playerName = "Alex",
                    department = "CSE",
                    universityId = "22112345",
                    admissionCompleted = true,
                    idCardIssued = true,
                    semester = 1,
                    currentDay = 1
                }
            });
        }

        private static void SetClassroomConfig(IcsClassroomInteractable target, string room, int floor)
        {
            var type = typeof(IcsClassroomInteractable);
            type.GetField("classroomNumber", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(target, room);
            type.GetField("floor", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(target, floor);
        }

        private static void AssertObjective(FacultyHUD hud, string prefix, bool complete)
        {
            TextMeshProUGUI marker = FindLabel(hud.transform, prefix + "Marker");
            TextMeshProUGUI title = FindLabel(hud.transform, prefix + "Title");
            Assert.That(marker, Is.Not.Null, prefix + "Marker");
            Assert.That(title, Is.Not.Null, prefix + "Title");
            Assert.That(marker.text, Is.EqualTo(complete ? "✓" : "○"));
            Assert.That(marker.color, Is.EqualTo(complete ? UiTheme.Success : UiTheme.BrightOrange));
            Assert.That(title.color, Is.EqualTo(complete ? UiTheme.Success : UiTheme.White));
        }

        private static bool HudContains(FacultyHUD hud, string text)
        {
            if (hud == null || string.IsNullOrEmpty(text))
            {
                return false;
            }

            TextMeshProUGUI[] labels = hud.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null && !string.IsNullOrEmpty(labels[i].text)
                    && labels[i].text.IndexOf(text, System.StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static TextMeshProUGUI FindLabel(Transform root, string objectName)
        {
            if (root == null)
            {
                return null;
            }

            TextMeshProUGUI[] labels = root.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null && labels[i].gameObject.name == objectName)
                {
                    return labels[i];
                }
            }

            return null;
        }

        private static Transform FindNamedChild(Transform root, string objectName)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == objectName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform match = FindNamedChild(root.GetChild(i), objectName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static void DestroyExisting(GameObject target)
        {
            if (target != null)
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
