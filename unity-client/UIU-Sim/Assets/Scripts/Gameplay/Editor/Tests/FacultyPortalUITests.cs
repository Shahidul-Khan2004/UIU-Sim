using NUnit.Framework;
using TMPro;
using UIU.Simulator.Gameplay.Faculty;
using UIU.Simulator.Gameplay.IDCard;
using UIU.Simulator.Gameplay.Player;
using UIU.Simulator.Networking;
using UIU.Simulator.UI;
using UnityEngine;
using UnityEngine.UI;

namespace UIU.Simulator.Gameplay.Editor.Tests
{
    [TestFixture]
    public sealed class FacultyPortalUITests
    {
        private GameObject saveObject;
        private PlayerSaveState saveState;
        private GameObject deskObject;
        private FacultyOfficeInteractable desk;

        [SetUp]
        public void SetUp()
        {
            DestroyExisting(PlayerSaveState.Instance != null ? PlayerSaveState.Instance.gameObject : null);
            DestroyExisting(FacultyMaterialsUI.Instance != null ? FacultyMaterialsUI.Instance.gameObject : null);
            DestroyExisting(FacultyStudentListUI.Instance != null ? FacultyStudentListUI.Instance.gameObject : null);
            DestroyExisting(FacultyCoursesUI.Instance != null ? FacultyCoursesUI.Instance.gameObject : null);
            DestroyExisting(ClassRoutineUI.Instance != null ? ClassRoutineUI.Instance.gameObject : null);
            DestroyExisting(FacultyPortalUI.Instance != null ? FacultyPortalUI.Instance.gameObject : null);

            saveObject = new GameObject("TestPlayerSaveState");
            saveState = saveObject.AddComponent<PlayerSaveState>();

            deskObject = new GameObject("TestFacultyDesk");
            deskObject.AddComponent<BoxCollider>();
            desk = deskObject.AddComponent<FacultyOfficeInteractable>();
        }

        [TearDown]
        public void TearDown()
        {
            if (FacultyMaterialsUI.IsOpen && FacultyMaterialsUI.Instance != null)
            {
                FacultyMaterialsUI.Instance.Hide();
            }

            if (FacultyStudentListUI.IsOpen && FacultyStudentListUI.Instance != null)
            {
                FacultyStudentListUI.Instance.Hide();
            }

            if (FacultyCoursesUI.IsOpen && FacultyCoursesUI.Instance != null)
            {
                FacultyCoursesUI.Instance.Hide();
            }

            if (ClassRoutineUI.IsOpen && ClassRoutineUI.Instance != null)
            {
                ClassRoutineUI.Instance.Hide();
            }

            if (FacultyPortalUI.IsOpen && FacultyPortalUI.Instance != null)
            {
                FacultyPortalUI.Instance.Hide();
            }

            DestroyExisting(FacultyMaterialsUI.Instance != null ? FacultyMaterialsUI.Instance.gameObject : null);
            DestroyExisting(FacultyStudentListUI.Instance != null ? FacultyStudentListUI.Instance.gameObject : null);
            DestroyExisting(FacultyCoursesUI.Instance != null ? FacultyCoursesUI.Instance.gameObject : null);
            DestroyExisting(ClassRoutineUI.Instance != null ? ClassRoutineUI.Instance.gameObject : null);
            DestroyExisting(FacultyPortalUI.Instance != null ? FacultyPortalUI.Instance.gameObject : null);
            DestroyExisting(deskObject);
            DestroyExisting(saveObject);
        }

        [Test]
        public void FacultyComputer_OpensPortal_WithSaveProfile()
        {
            ApplySave("Lail", FacultyIdentity.Role, "CSE", "F-001", idCardIssued: true);

            string response = desk.Interact();

            Assert.That(response, Is.Null);
            Assert.That(FacultyPortalUI.IsOpen, Is.True);
            Assert.That(FindPortalLabel("Title").text, Is.EqualTo("FACULTY PORTAL"));
            Assert.That(FindPortalLabel("WelcomeLabel").text, Is.EqualTo("Welcome, Lail"));
            Assert.That(FindPortalLabel("NameValue").text, Is.EqualTo("Lail"));
            Assert.That(FindPortalLabel("FacultyIdValue").text, Is.EqualTo("F-001"));
            Assert.That(FindPortalLabel("DepartmentValue").text, Is.EqualTo("CSE"));
            Assert.That(FindPortalLabel("DesignationValue").text, Is.EqualTo(FacultyIdentity.Designation));
            Assert.That(FindPortalLabel("OfficeValue").text, Is.EqualTo(FacultyIdentity.FormatOffice()));
        }

        [Test]
        public void FacultyPortal_ShowsViewCoursesButton()
        {
            ApplySave("Lail", FacultyIdentity.Role, "CSE", "F-001", idCardIssued: true);
            FacultyPortalUI portal = FacultyPortalUI.EnsureExists();
            portal.Show();

            Transform viewButton = FindNamedChild(portal.transform, "Button_ViewCourses");
            Assert.That(viewButton, Is.Not.Null);
            Assert.That(viewButton.GetComponent<Button>(), Is.Not.Null);
            Assert.That(viewButton.GetComponent<Button>().interactable, Is.True);
            Assert.That(FindPortalLabel("Button_ViewCoursesLabel").text, Is.EqualTo("View Courses"));
            Assert.That(FindNamedChild(portal.transform, "Button_ViewClassRoutine"), Is.Null);
            Assert.That(ClassRoutineUI.IsOpen, Is.False);
        }

        [Test]
        public void ViewCourses_LoadsAssignedCoursesFromBackendPayload()
        {
            ApplySave("Lail", FacultyIdentity.Role, "CSE", "F-001", idCardIssued: true);
            FacultyPortalUI portal = FacultyPortalUI.EnsureExists();
            portal.Show();
            portal.OpenCourses();

            Assert.That(FacultyPortalUI.IsOpen, Is.True);
            Assert.That(FacultyCoursesUI.IsOpen, Is.True);
            FacultyCoursesUI.Instance.ApplyCoursesResponse(SampleCoursesResponse());

            Assert.That(FindCoursesLabel("Title").text, Is.EqualTo("MY COURSES"));
            Assert.That(FindCourseLabel("ICS", "CourseValue").text, Is.EqualTo("Introduction to Computer Science"));
            Assert.That(FindCourseLabel("ICS", "ClassroomValue").text, Is.EqualTo("Room 427"));
            Assert.That(FindCourseLabel("ICS", "FloorValue").text, Is.EqualTo("4"));
            Assert.That(FindCourseLabel("DM", "CourseValue").text, Is.EqualTo("Discrete Mathematics"));
            Assert.That(FindCourseLabel("DM", "ClassroomValue").text, Is.EqualTo("Room 423"));
            Assert.That(FindCourseLabel("DM", "FloorValue").text, Is.EqualTo("4"));
            Assert.That(FindNamedChild(FacultyCoursesUI.Instance.transform, "Button_ViewStudents_ICS"), Is.Not.Null);
            Assert.That(FindNamedChild(FacultyCoursesUI.Instance.transform, "Button_ViewMaterials_ICS"), Is.Not.Null);
            Assert.That(FindNamedChild(FacultyCoursesUI.Instance.transform, "Button_ViewStudents_DM"), Is.Not.Null);
            Assert.That(FindNamedChild(FacultyCoursesUI.Instance.transform, "Button_ViewMaterials_DM"), Is.Not.Null);

            RectTransform panel = FindNamedChild(FacultyCoursesUI.Instance.transform, "FacultyCoursesPanel")
                as RectTransform;
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.sizeDelta.y, Is.GreaterThanOrEqualTo(800f));
        }

        [Test]
        public void ViewStudents_OpensEnrolledStudentList()
        {
            ApplySave("Lail", FacultyIdentity.Role, "CSE", "F-001", idCardIssued: true);
            FacultyPortalUI portal = FacultyPortalUI.EnsureExists();
            portal.Show();
            portal.OpenCourses();
            FacultyCoursesUI.Instance.ApplyCoursesResponse(SampleCoursesResponse());
            FacultyCoursesUI.Instance.OpenStudents(SampleCoursesResponse().courses[0]);
            FacultyStudentListUI.Instance.ApplyStudents(SampleStudents());

            Assert.That(FacultyStudentListUI.IsOpen, Is.True);
            Assert.That(FindStudentLabel("Title").text, Is.EqualTo("ICS STUDENTS"));
            Assert.That(FindNamedChild(FacultyStudentListUI.Instance.transform, "Student_0"), Is.Not.Null);
            Assert.That(FindLabel(FacultyStudentListUI.Instance.transform, "StudentName").text, Is.EqualTo("Rahim"));
            Assert.That(FindLabel(FacultyStudentListUI.Instance.transform, "StudentId").text, Is.EqualTo("2025001"));
            Assert.That(FindStudentLabel("HeaderName").text, Is.EqualTo("Student Name"));
            Assert.That(FindStudentLabel("HeaderId").text, Is.EqualTo("Student ID"));
        }

        [Test]
        public void ViewStudents_EmptyState_DisplaysNoStudentsEnrolled()
        {
            ApplySave("Lail", FacultyIdentity.Role, "CSE", "F-001", idCardIssued: true);
            FacultyPortalUI portal = FacultyPortalUI.EnsureExists();
            portal.Show();
            portal.OpenCourses();
            FacultyCoursesUI.Instance.ApplyCoursesResponse(SampleCoursesResponse());
            FacultyCoursesUI.Instance.OpenStudents(SampleCoursesResponse().courses[1]);
            FacultyStudentListUI.Instance.ApplyStudents(System.Array.Empty<ApiClient.FacultyStudentDto>());

            Assert.That(FacultyStudentListUI.IsOpen, Is.True);
            Assert.That(FindStudentLabel("Title").text, Is.EqualTo("DM STUDENTS"));
            Assert.That(FindStudentLabel("StatusLabel").text, Is.EqualTo(FacultyStudentListUI.EmptyStudentsMessage));
            Assert.That(FindStudentLabel("StatusLabel").color, Is.EqualTo(UiTheme.Grey));
            Assert.That(FindNamedChild(FacultyStudentListUI.Instance.transform, "Student_0"), Is.Null);
        }

        [Test]
        public void ViewMaterials_OpensCourseMaterials()
        {
            ApplySave("Lail", FacultyIdentity.Role, "CSE", "F-001", idCardIssued: true);
            FacultyPortalUI portal = FacultyPortalUI.EnsureExists();
            portal.Show();
            portal.OpenCourses();
            FacultyCoursesUI.Instance.ApplyCoursesResponse(SampleCoursesResponse());
            FacultyCoursesUI.Instance.OpenMaterials(SampleCoursesResponse().courses[0]);
            FacultyMaterialsUI.Instance.ApplyMaterials(SampleMaterials());

            Assert.That(FacultyMaterialsUI.IsOpen, Is.True);
            Assert.That(FindMaterialsLabel("Title").text, Is.EqualTo("ICS MATERIALS"));
            Assert.That(FindLabel(FacultyMaterialsUI.Instance.transform, "MaterialTitle").text, Is.EqualTo("1. Lecture Notes"));
            Assert.That(FindLabel(FacultyMaterialsUI.Instance.transform, "MaterialUrl").text, Is.EqualTo("https://drive.example/lecture-notes"));
            Assert.That(FindNamedChild(FacultyMaterialsUI.Instance.transform, "MaterialType"), Is.Null);
            Assert.That(FindNamedChild(FacultyMaterialsUI.Instance.transform, "Button_OpenMaterial_0"), Is.Not.Null);
            Assert.That(FindNamedChild(FacultyMaterialsUI.Instance.transform, "Button_DeleteMaterial_0"), Is.Not.Null);
            Assert.That(FindNamedChild(FacultyMaterialsUI.Instance.transform, "Button_AddMaterial"), Is.Not.Null);
        }

        [Test]
        public void ViewMaterials_OpenButtons_OpenBrowserUrls()
        {
            ApplySave("Lail", FacultyIdentity.Role, "CSE", "F-001", idCardIssued: true);
            FacultyPortalUI portal = FacultyPortalUI.EnsureExists();
            portal.Show();
            portal.OpenCourses();
            FacultyCoursesUI.Instance.ApplyCoursesResponse(SampleCoursesResponse());
            FacultyCoursesUI.Instance.OpenMaterials(SampleCoursesResponse().courses[0]);
            FacultyMaterialsUI.Instance.ApplyMaterials(SampleMaterials());

            FindNamedChild(FacultyMaterialsUI.Instance.transform, "Button_OpenMaterial_0").GetComponent<Button>().onClick.Invoke();
            Assert.That(FacultyMaterialsUI.LastOpenedUrl, Is.EqualTo("https://drive.example/lecture-notes"));

            FindNamedChild(FacultyMaterialsUI.Instance.transform, "Button_OpenMaterial_1").GetComponent<Button>().onClick.Invoke();
            Assert.That(FacultyMaterialsUI.LastOpenedUrl, Is.EqualTo("https://drive.example/chapter-notes"));
        }

        [Test]
        public void ViewMaterials_DeleteConfirmation_RemovesMaterialAndShowsEmptyState()
        {
            ApplySave("Lail", FacultyIdentity.Role, "CSE", "F-001", idCardIssued: true);
            FacultyPortalUI portal = FacultyPortalUI.EnsureExists();
            portal.Show();
            portal.OpenCourses();
            FacultyCoursesUI.Instance.ApplyCoursesResponse(SampleCoursesResponse());
            FacultyCoursesUI.Instance.OpenMaterials(SampleCoursesResponse().courses[0]);
            FacultyMaterialsUI.Instance.ApplyMaterials(SampleMaterials());

            FindNamedChild(FacultyMaterialsUI.Instance.transform, "Button_DeleteMaterial_0").GetComponent<Button>().onClick.Invoke();
            Assert.That(FacultyMaterialsUI.IsDeleteConfirmOpen, Is.True);
            Assert.That(FindMaterialsLabel("DeleteConfirmLabel").text, Is.EqualTo(FacultyMaterialsUI.DeleteConfirmMessage));
            Assert.That(FindNamedChild(FacultyMaterialsUI.Instance.transform, "FacultyDeleteConfirmOverlay").gameObject.activeSelf, Is.True);

            FindNamedChild(FacultyMaterialsUI.Instance.transform, "Button_CancelDelete").GetComponent<Button>().onClick.Invoke();
            Assert.That(FacultyMaterialsUI.IsDeleteConfirmOpen, Is.False);
            Assert.That(FindNamedChild(FacultyMaterialsUI.Instance.transform, "Material_0"), Is.Not.Null);
            Assert.That(FindNamedChild(FacultyMaterialsUI.Instance.transform, "Material_1"), Is.Not.Null);

            FindNamedChild(FacultyMaterialsUI.Instance.transform, "Button_DeleteMaterial_0").GetComponent<Button>().onClick.Invoke();
            FindNamedChild(FacultyMaterialsUI.Instance.transform, "Button_ConfirmDelete").GetComponent<Button>().onClick.Invoke();
            Assert.That(FacultyMaterialsUI.IsDeleteConfirmOpen, Is.False);
            Assert.That(FindLabel(FacultyMaterialsUI.Instance.transform, "MaterialTitle").text, Is.EqualTo("1. Google Drive Chapter Notes"));
            Assert.That(FindNamedChild(FacultyMaterialsUI.Instance.transform, "Material_1"), Is.Null);

            FindNamedChild(FacultyMaterialsUI.Instance.transform, "Button_DeleteMaterial_0").GetComponent<Button>().onClick.Invoke();
            FindNamedChild(FacultyMaterialsUI.Instance.transform, "Button_ConfirmDelete").GetComponent<Button>().onClick.Invoke();
            Assert.That(FindNamedChild(FacultyMaterialsUI.Instance.transform, "Material_0"), Is.Null);
            Assert.That(FindMaterialsLabel("StatusLabel").text, Is.EqualTo(FacultyMaterialsUI.EmptyMaterialsMessage));
            Assert.That(FindMaterialsLabel("StatusLabel").color, Is.EqualTo(UiTheme.Grey));
        }

        [Test]
        public void ViewMaterials_EmptyState_DisplaysNoMaterialsAddedYet()
        {
            ApplySave("Lail", FacultyIdentity.Role, "CSE", "F-001", idCardIssued: true);
            FacultyPortalUI portal = FacultyPortalUI.EnsureExists();
            portal.Show();
            portal.OpenCourses();
            FacultyCoursesUI.Instance.ApplyCoursesResponse(SampleCoursesResponse());
            FacultyCoursesUI.Instance.OpenMaterials(SampleCoursesResponse().courses[0]);
            FacultyMaterialsUI.Instance.ApplyMaterials(System.Array.Empty<ApiClient.FacultyMaterialDto>());

            Assert.That(FacultyMaterialsUI.IsOpen, Is.True);
            Assert.That(FindMaterialsLabel("ExistingHeader").text, Is.EqualTo("Existing Materials:"));
            Assert.That(FindMaterialsLabel("StatusLabel").text, Is.EqualTo(FacultyMaterialsUI.EmptyMaterialsMessage));
            Assert.That(FindMaterialsLabel("StatusLabel").color, Is.EqualTo(UiTheme.Grey));
        }

        [Test]
        public void AddMaterial_FormContainsOnlyTitleAndUrl_NoPdfUi()
        {
            ApplySave("Lail", FacultyIdentity.Role, "CSE", "F-001", idCardIssued: true);
            FacultyPortalUI portal = FacultyPortalUI.EnsureExists();
            portal.Show();
            portal.OpenCourses();
            FacultyCoursesUI.Instance.ApplyCoursesResponse(SampleCoursesResponse());
            FacultyCoursesUI.Instance.OpenMaterials(SampleCoursesResponse().courses[0]);
            FacultyMaterialsUI.Instance.ShowAddForm();

            Assert.That(FindNamedChild(FacultyMaterialsUI.Instance.transform, "TitleField"), Is.Not.Null);
            Assert.That(FindNamedChild(FacultyMaterialsUI.Instance.transform, "UrlField"), Is.Not.Null);
            Assert.That(FindMaterialsLabel("UrlKey").text, Is.EqualTo("Drive / External Link:"));
            Assert.That(FindNamedChild(FacultyMaterialsUI.Instance.transform, "PdfRow"), Is.Null);
            Assert.That(FindNamedChild(FacultyMaterialsUI.Instance.transform, "Button_ChooseFile"), Is.Null);
            Assert.That(FindNamedChild(FacultyMaterialsUI.Instance.transform, "Button_TypePdf"), Is.Null);
            Assert.That(FindNamedChild(FacultyMaterialsUI.Instance.transform, "Button_TypeLink"), Is.Null);
            Assert.That(FindNamedChild(FacultyMaterialsUI.Instance.transform, "TypeButtons"), Is.Null);
        }

        [Test]
        public void StudentComputer_DoesNotOpenPortal()
        {
            ApplySave("Alex Student", "STUDENT", "CSE", "22112345", idCardIssued: true);

            string response = desk.Interact();

            Assert.That(response, Is.EqualTo("You need faculty access to use this workstation."));
            Assert.That(FacultyPortalUI.IsOpen, Is.False);
            Assert.That(FacultyPortalUI.Instance, Is.Null);
        }

        [Test]
        public void FacultyWithoutIssuedCard_DoesNotOpenPortal()
        {
            ApplySave("Lail", FacultyIdentity.Role, "CSE", "F-001", idCardIssued: false);

            string response = desk.Interact();

            Assert.That(response, Is.EqualTo("You need faculty access to use this workstation."));
            Assert.That(FacultyPortalUI.IsOpen, Is.False);
        }

        [Test]
        public void Hide_ClosesPortal()
        {
            ApplySave("Lail", FacultyIdentity.Role, "CSE", "F-001", idCardIssued: true);
            desk.Interact();

            Assert.That(FacultyPortalUI.IsOpen, Is.True);
            FacultyPortalUI.Instance.Hide();
            Assert.That(FacultyPortalUI.IsOpen, Is.False);
        }

        private static ApiClient.FacultyCoursesResponseDto SampleCoursesResponse()
        {
            return new ApiClient.FacultyCoursesResponseDto
            {
                facultyId = "F-001",
                courses = new[]
                {
                    new ApiClient.FacultyCourseDto
                    {
                        courseCode = "ICS",
                        courseName = "Introduction to Computer Science",
                        classroom = "427",
                        floor = 4
                    },
                    new ApiClient.FacultyCourseDto
                    {
                        courseCode = "DM",
                        courseName = "Discrete Mathematics",
                        classroom = "423",
                        floor = 4
                    }
                }
            };
        }

        private static ApiClient.FacultyStudentDto[] SampleStudents()
        {
            return new[]
            {
                new ApiClient.FacultyStudentDto
                {
                    studentName = "Rahim",
                    studentId = "2025001"
                },
                new ApiClient.FacultyStudentDto
                {
                    studentName = "Karim",
                    studentId = "2025002"
                }
            };
        }

        private static ApiClient.FacultyMaterialDto[] SampleMaterials()
        {
            return new[]
            {
                new ApiClient.FacultyMaterialDto
                {
                    id = "1",
                    courseId = "ICS",
                    title = "Lecture Notes",
                    url = "https://drive.example/lecture-notes"
                },
                new ApiClient.FacultyMaterialDto
                {
                    id = "2",
                    courseId = "ICS",
                    title = "Google Drive Chapter Notes",
                    url = "https://drive.example/chapter-notes"
                }
            };
        }

        private void ApplySave(string playerName, string role, string department, string universityId, bool idCardIssued)
        {
            var dto = new ApiClient.PlayerSaveStatusDto
            {
                hasSave = true,
                save = new ApiClient.PlayerSaveDto
                {
                    playerName = playerName,
                    role = role,
                    department = department,
                    universityId = universityId,
                    admissionCompleted = true,
                    idCardIssued = idCardIssued,
                    semester = 1,
                    currentDay = 1
                }
            };
            saveState.ApplyCreatedSave(dto);
        }

        private static TextMeshProUGUI FindPortalLabel(string objectName)
        {
            return FindLabel(FacultyPortalUI.Instance != null ? FacultyPortalUI.Instance.transform : null, objectName);
        }

        private static TextMeshProUGUI FindCoursesLabel(string objectName)
        {
            return FindLabel(FacultyCoursesUI.Instance != null ? FacultyCoursesUI.Instance.transform : null, objectName);
        }

        private static TextMeshProUGUI FindStudentLabel(string objectName)
        {
            return FindLabel(FacultyStudentListUI.Instance != null ? FacultyStudentListUI.Instance.transform : null, objectName);
        }

        private static TextMeshProUGUI FindMaterialsLabel(string objectName)
        {
            return FindLabel(FacultyMaterialsUI.Instance != null ? FacultyMaterialsUI.Instance.transform : null, objectName);
        }

        private static TextMeshProUGUI FindCourseLabel(string courseCode, string objectName)
        {
            if (FacultyCoursesUI.Instance == null)
            {
                return null;
            }

            Transform card = FindNamedChild(FacultyCoursesUI.Instance.transform, "Course_" + courseCode);
            return card != null ? FindLabel(card, objectName) : null;
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
