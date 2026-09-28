using NUnit.Framework;
using TMPro;
using UIU.Simulator.Gameplay.Assessment;
using UIU.Simulator.Gameplay.Elevator;
using UIU.Simulator.Gameplay.Faculty;
using UIU.Simulator.Gameplay.IDCard;
using UIU.Simulator.Gameplay.Player;
using UIU.Simulator.Gameplay.UI;
using UIU.Simulator.Networking;
using UIU.Simulator.UI;
using UnityEngine;
using UnityEngine.UI;

namespace UIU.Simulator.Gameplay.Editor.Tests
{
    [TestFixture]
    public sealed class GameMenuTests
    {
        private GameObject playerObject;
        private PlayerMovement playerMovement;
        private FirstPersonLook firstPersonLook;
        private InteractionController interactionController;
        private GameObject cameraObject;

        private GameObject menuObject;
        private GameMenuManager menu;

        [SetUp]
        public void SetUp()
        {
            Time.timeScale = 1f;

            if (GameMenuManager.Instance != null)
            {
                Object.DestroyImmediate(GameMenuManager.Instance.gameObject);
            }

            if (WelcomeCardUI.Instance != null)
            {
                Object.DestroyImmediate(WelcomeCardUI.Instance.gameObject);
            }

            if (ClassRoutineUI.Instance != null)
            {
                Object.DestroyImmediate(ClassRoutineUI.Instance.gameObject);
            }

            if (PlayerSaveState.Instance != null)
            {
                Object.DestroyImmediate(PlayerSaveState.Instance.gameObject);
            }

            if (FacultyProgress.Instance != null)
            {
                Object.DestroyImmediate(FacultyProgress.Instance.gameObject);
            }

            playerObject = new GameObject("TestPlayer");
            playerObject.AddComponent<CharacterController>();
            playerMovement = playerObject.AddComponent<PlayerMovement>();
            firstPersonLook = playerObject.AddComponent<FirstPersonLook>();
            interactionController = playerObject.AddComponent<InteractionController>();

            cameraObject = new GameObject("FirstPersonCamera");
            cameraObject.transform.SetParent(playerObject.transform, false);
            cameraObject.AddComponent<Camera>();

            menuObject = new GameObject("TestGameMenuManager");
            menu = menuObject.AddComponent<GameMenuManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (ClassRoutineUI.IsOpen && ClassRoutineUI.Instance != null)
            {
                ClassRoutineUI.Instance.Hide();
            }

            if (IdCardUI.IsOpen && IdCardUI.Instance != null)
            {
                IdCardUI.Instance.Hide();
            }

            if (GameMenuManager.IsOpen && menu != null)
            {
                menu.Close(restoreGameplayControls: true);
            }

            Time.timeScale = 1f;

            if (ClassRoutineUI.Instance != null)
            {
                Object.DestroyImmediate(ClassRoutineUI.Instance.gameObject);
            }

            if (FacultyProgress.Instance != null)
            {
                Object.DestroyImmediate(FacultyProgress.Instance.gameObject);
            }

            if (IdCardUI.Instance != null)
            {
                Object.DestroyImmediate(IdCardUI.Instance.gameObject);
            }

            if (WelcomeCardUI.Instance != null)
            {
                if (WelcomeCardUI.IsOpen)
                {
                    WelcomeCardUI.Instance.Hide();
                }

                Object.DestroyImmediate(WelcomeCardUI.Instance.gameObject);
            }

            if (PlayerSaveState.Instance != null)
            {
                Object.DestroyImmediate(PlayerSaveState.Instance.gameObject);
            }

            if (menuObject != null)
            {
                Object.DestroyImmediate(menuObject);
            }

            if (cameraObject != null)
            {
                Object.DestroyImmediate(cameraObject);
            }

            if (playerObject != null)
            {
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void Test01_EscapeOpensMenu_AndSoftPausesPlayerOnly()
        {
            Assert.That(GameMenuManager.IsOpen, Is.False);
            Assert.That(playerMovement.enabled, Is.True);
            Assert.That(firstPersonLook.enabled, Is.True);
            Assert.That(interactionController.enabled, Is.True);

            menu.HandleEscape();

            Assert.That(GameMenuManager.IsOpen, Is.True, "ESC must open the game menu.");
            Assert.That(playerMovement.enabled, Is.False, "Player movement must be disabled while the menu is open.");
            Assert.That(firstPersonLook.enabled, Is.False, "Camera look must be disabled while the menu is open.");
            Assert.That(interactionController.enabled, Is.False, "Gameplay interaction must be disabled while the menu is open.");
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None), "Cursor must be unlocked while the menu is open.");
            Assert.That(Cursor.visible, Is.True, "Cursor must be visible while the menu is open.");
            Assert.That(Time.timeScale, Is.EqualTo(1f), "Soft pause must not use Time.timeScale = 0.");
        }

        [Test]
        public void Test02_EscapeWhileOpen_ResumesControls()
        {
            menu.Open();
            Assert.That(GameMenuManager.IsOpen, Is.True);

            menu.HandleEscape();

            Assert.That(GameMenuManager.IsOpen, Is.False);
            Assert.That(playerMovement.enabled, Is.True);
            Assert.That(firstPersonLook.enabled, Is.True);
            Assert.That(interactionController.enabled, Is.True);
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked));
            Assert.That(Cursor.visible, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [Test]
        public void Test03_ResumeButton_RestoresControls()
        {
            menu.Open();

            Button resume = FindButton("Button_Resume");
            Assert.That(resume, Is.Not.Null, "Resume button must exist.");
            resume.onClick.Invoke();

            Assert.That(GameMenuManager.IsOpen, Is.False);
            Assert.That(playerMovement.enabled, Is.True);
            Assert.That(firstPersonLook.enabled, Is.True);
            Assert.That(interactionController.enabled, Is.True);
        }

        [Test]
        public void Test04_MenuOptions_ExistWithExpectedLabels()
        {
            GameObject saveObject = CreateStudentSave();
            try
            {
                menu.Open();

                Assert.That(FindButton("Button_Resume"), Is.Not.Null);
                Assert.That(FindButton("Button_SaveGame"), Is.Not.Null);
                Assert.That(FindButton("Button_NextDay"), Is.Not.Null);
                Assert.That(FindButton("Button_NewGame"), Is.Not.Null);
                Assert.That(FindButton("Button_ReportCard"), Is.Not.Null);
                Assert.That(FindButton("Button_IdCard"), Is.Not.Null);
                Assert.That(FindButton("Button_ClassRoutine"), Is.Not.Null);
                Assert.That(FindButton("Button_Settings"), Is.Not.Null);
                Assert.That(FindButton("Button_Logout"), Is.Not.Null);
                Assert.That(FindButton("Button_QuitGame"), Is.Not.Null);

                Assert.That(FindNamedTmp("Title").text, Is.EqualTo("GAME MENU"));
                Assert.That(FindButton("Button_NextDay").gameObject.activeSelf, Is.True);
                Assert.That(FindButton("Button_ReportCard").gameObject.activeSelf, Is.True);
                Assert.That(FindButton("Button_IdCard").gameObject.activeSelf, Is.True);
                Assert.That(FindButton("Button_ClassRoutine").gameObject.activeSelf, Is.True);
                Assert.That(FindButton("Button_FacultyAttendance"), Is.Null);
                Assert.That(FindButton("Button_CourseManagement"), Is.Null);
                Assert.That(FindButton("Button_MaterialsManagement"), Is.Null);

                Assert.That(FindButtonLabel("Button_Resume"), Is.EqualTo("Resume"));
                Assert.That(FindButtonLabel("Button_SaveGame"), Is.EqualTo("Save Game"));
                Assert.That(FindButtonLabel("Button_NextDay"), Is.EqualTo("Next Day"));
                Assert.That(FindButtonLabel("Button_NewGame"), Is.EqualTo("New Game"));
                Assert.That(FindButtonLabel("Button_ReportCard"), Is.EqualTo("Report Card"));
                Assert.That(FindButtonLabel("Button_IdCard"), Is.EqualTo("ID Card"));
                Assert.That(FindButtonLabel("Button_ClassRoutine"), Is.EqualTo("Class Routine"));
                Assert.That(FindButtonLabel("Button_Settings"), Is.EqualTo("Settings"));
                Assert.That(FindButtonLabel("Button_Logout"), Is.EqualTo("Logout"));
                Assert.That(FindButtonLabel("Button_QuitGame"), Is.EqualTo("Quit Game"));
            }
            finally
            {
                Object.DestroyImmediate(saveObject);
            }
        }

        [Test]
        public void Test04b_VisitorMenu_HidesRoleSpecificOptions()
        {
            GameObject saveObject = CreateVisitorSave();
            try
            {
                menu.Open();

                Assert.That(FindNamedTmp("Title").text, Is.EqualTo("GAME MENU"));
                Assert.That(FindButton("Button_Resume").gameObject.activeSelf, Is.True);
                Assert.That(FindButton("Button_SaveGame").gameObject.activeSelf, Is.True);
                Assert.That(FindButton("Button_NewGame").gameObject.activeSelf, Is.True);
                Assert.That(FindButton("Button_Settings").gameObject.activeSelf, Is.True);
                Assert.That(FindButton("Button_Logout").gameObject.activeSelf, Is.True);
                Assert.That(FindButton("Button_QuitGame").gameObject.activeSelf, Is.True);

                Assert.That(FindButton("Button_NextDay").gameObject.activeSelf, Is.False);
                Assert.That(FindButton("Button_ReportCard").gameObject.activeSelf, Is.False);
                Assert.That(FindButton("Button_IdCard").gameObject.activeSelf, Is.False);
                Assert.That(FindButton("Button_ClassRoutine").gameObject.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(saveObject);
            }
        }

        [Test]
        public void Test05_NewGame_ShowsConfirmation_CancelClosesConfirmOnly()
        {
            menu.Open();
            Assert.That(menu.IsConfirmOpen, Is.False);

            FindButton("Button_NewGame").onClick.Invoke();

            Assert.That(menu.IsConfirmOpen, Is.True);
            Assert.That(GameMenuManager.IsOpen, Is.True);
            Assert.That(FindTmpText("Delete current university progress?"), Is.Not.Null);
            Assert.That(FindButton("Button_CancelNewGame"), Is.Not.Null);
            Assert.That(FindButton("Button_ConfirmNewGame"), Is.Not.Null);

            FindButton("Button_CancelNewGame").onClick.Invoke();

            Assert.That(menu.IsConfirmOpen, Is.False);
            Assert.That(GameMenuManager.IsOpen, Is.True, "Cancel must not close the game menu.");
            Assert.That(playerMovement.enabled, Is.False);
        }

        [Test]
        public void Test06_EscapeDuringConfirm_ClosesConfirmNotMenu()
        {
            menu.Open();
            FindButton("Button_NewGame").onClick.Invoke();
            Assert.That(menu.IsConfirmOpen, Is.True);

            menu.HandleEscape();

            Assert.That(menu.IsConfirmOpen, Is.False);
            Assert.That(GameMenuManager.IsOpen, Is.True);
        }

        [Test]
        public void Test07_SaveConfirmation_ShowsExactMessage()
        {
            menu.Open();
            menu.ShowSaveConfirmation();

            TextMeshProUGUI status = FindNamedTmp("StatusLabel");
            Assert.That(status, Is.Not.Null);
            Assert.That(status.text, Is.EqualTo("Game Saved ✓"));
        }

        [Test]
        public void Test08_PlaceholderButtons_DoNotCloseMenu()
        {
            menu.Open();

            FindButton("Button_Settings").onClick.Invoke();

            Assert.That(GameMenuManager.IsOpen, Is.True);
            Assert.That(playerMovement.enabled, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(FindNamedTmp("StatusLabel").text, Does.Contain("coming soon").IgnoreCase);
        }

        [Test]
        public void Test08d_StudentClassRoutine_OpensExistingRoutineUi()
        {
            GameObject saveObject = CreateStudentSave();
            try
            {
                menu.Open();
                FindButton("Button_ClassRoutine").onClick.Invoke();

                Assert.That(GameMenuManager.IsOpen, Is.True);
                Assert.That(ClassRoutineUI.IsOpen, Is.True);
                Assert.That(FindRoutineLabel("Title").text, Is.EqualTo(StudentClassRoutineUI.Title));
                Assert.That(FindRoutineCourse("ICS", "CourseValue").text, Is.EqualTo("Introduction to Computer Science"));
                Assert.That(FindRoutineCourse("ICS", "ClassroomValue").text, Is.EqualTo("Room 427"));
                Assert.That(FindRoutineCourse("ICS", "FloorValue").text, Is.EqualTo("4"));
                Assert.That(FindRoutineCourse("ENGLISH", "CourseValue").text, Is.EqualTo("English"));
                Assert.That(FindRoutineCourse("ENGLISH", "ClassroomValue").text, Is.EqualTo("Room 702"));
                Assert.That(FindRoutineCourse("ENGLISH", "FloorValue").text, Is.EqualTo("7"));
                Assert.That(FindRoutineCourse("DM", "CourseValue").text, Is.EqualTo("Discrete Mathematics"));
                Assert.That(FindRoutineCourse("DM", "ClassroomValue").text, Is.EqualTo("Room 423"));
                Assert.That(FindRoutineCourse("DM", "FloorValue").text, Is.EqualTo("4"));
                Assert.That(FindNamedChild(ClassRoutineUI.Instance.transform, "Routine_PHY"), Is.Null);
                Assert.That(FindNamedChild(ClassRoutineUI.Instance.transform, "Routine_IB"), Is.Null);
                Assert.That(FindNamedChild(ClassRoutineUI.Instance.transform, "Routine_POA"), Is.Null);
                Assert.That(FindNamedChild(ClassRoutineUI.Instance.transform, "Routine_BBA-ENGLISH"), Is.Null);

                menu.HandleEscape();
                Assert.That(ClassRoutineUI.IsOpen, Is.False);
                Assert.That(GameMenuManager.IsOpen, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(saveObject);
            }
        }

        [Test]
        public void Test08d2_BbaStudentClassRoutine_ShowsOnlyBbaCourses()
        {
            if (PlayerSaveState.Instance != null)
            {
                Object.DestroyImmediate(PlayerSaveState.Instance.gameObject);
            }

            GameObject saveObject = new GameObject("TestBbaStudentSaveState");
            PlayerSaveState saveState = saveObject.AddComponent<PlayerSaveState>();
            saveState.ApplyCreatedSave(new ApiClient.PlayerSaveStatusDto
            {
                hasSave = true,
                save = new ApiClient.PlayerSaveDto
                {
                    playerName = "Nadia Student",
                    role = "STUDENT",
                    department = "BBA",
                    universityId = "22110001",
                    admissionCompleted = true,
                    idCardIssued = true,
                    semester = 1,
                    currentDay = 1
                }
            });

            try
            {
                menu.Open();
                FindButton("Button_ClassRoutine").onClick.Invoke();

                Assert.That(GameMenuManager.IsOpen, Is.True);
                Assert.That(ClassRoutineUI.IsOpen, Is.True);
                Assert.That(FindRoutineLabel("Title").text, Is.EqualTo(StudentClassRoutineUI.Title));
                Assert.That(FindRoutineCourse("IB", "CourseValue").text, Is.EqualTo("Introduction to Business"));
                Assert.That(FindRoutineCourse("IB", "ClassroomValue").text, Is.EqualTo("Room 523"));
                Assert.That(FindRoutineCourse("IB", "FloorValue").text, Is.EqualTo("5"));
                Assert.That(FindRoutineCourse("POA", "CourseValue").text, Is.EqualTo("Principles of Accounting"));
                Assert.That(FindRoutineCourse("POA", "ClassroomValue").text, Is.EqualTo("Room 527"));
                Assert.That(FindRoutineCourse("BBA-ENGLISH", "CourseValue").text, Is.EqualTo("English"));
                Assert.That(FindRoutineCourse("BBA-ENGLISH", "ClassroomValue").text, Is.EqualTo("Room 701"));
                Assert.That(FindNamedChild(ClassRoutineUI.Instance.transform, "Routine_ICS"), Is.Null);
                Assert.That(FindNamedChild(ClassRoutineUI.Instance.transform, "Routine_ENGLISH"), Is.Null);
                Assert.That(FindNamedChild(ClassRoutineUI.Instance.transform, "Routine_DM"), Is.Null);
            }
            finally
            {
                if (ClassRoutineUI.IsOpen && ClassRoutineUI.Instance != null)
                {
                    ClassRoutineUI.Instance.Hide();
                }

                Object.DestroyImmediate(saveObject);
            }
        }

        [Test]
        public void Test08b_IdCard_WithoutAdmission_IsHiddenFromVisitorMenu()
        {
            GameObject saveObject = CreateVisitorSave();
            try
            {
                menu.Open();

                Assert.That(FindButton("Button_IdCard").gameObject.activeSelf, Is.False);
                Assert.That(FindButton("Button_ReportCard").gameObject.activeSelf, Is.False);
                Assert.That(FindButton("Button_ClassRoutine").gameObject.activeSelf, Is.False);
                Assert.That(IdCardUI.IsOpen, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(saveObject);
            }
        }

        [Test]
        public void Test08c_IdCard_AfterAdmission_OpensIdCardPanel()
        {
            if (PlayerSaveState.Instance != null)
            {
                Object.DestroyImmediate(PlayerSaveState.Instance.gameObject);
            }

            GameObject saveObject = new GameObject("TestPlayerSaveState");
            PlayerSaveState saveState = saveObject.AddComponent<PlayerSaveState>();
            saveState.SetStateForTesting(hasSaveValue: true, idCardIssuedValue: true);

            var dto = new ApiClient.PlayerSaveStatusDto
            {
                hasSave = true,
                save = new ApiClient.PlayerSaveDto
                {
                    playerName = "Alex Student",
                    role = "STUDENT",
                    department = "CSE",
                    universityId = "22112345",
                    admissionCompleted = true,
                    idCardIssued = true
                }
            };
            saveState.ApplyCreatedSave(dto);

            try
            {
                menu.Open();
                FindButton("Button_IdCard").onClick.Invoke();

                Assert.That(GameMenuManager.IsOpen, Is.True);
                Assert.That(IdCardUI.IsOpen, Is.True);
                Assert.That(IdCardUI.Instance, Is.Not.Null);

                menu.HandleEscape();
                Assert.That(IdCardUI.IsOpen, Is.False);
                Assert.That(GameMenuManager.IsOpen, Is.True);
            }
            finally
            {
                if (IdCardUI.IsOpen)
                {
                    IdCardUI.Instance.Hide();
                }

                if (IdCardUI.Instance != null)
                {
                    Object.DestroyImmediate(IdCardUI.Instance.gameObject);
                }

                Object.DestroyImmediate(saveObject);
            }
        }

        [Test]
        public void Test08d_ReportCard_AvailableToAnyAdmittedStudent()
        {
            if (PlayerSaveState.Instance != null)
            {
                Object.DestroyImmediate(PlayerSaveState.Instance.gameObject);
            }

            GameObject saveObject = new GameObject("TestPlayerSaveState");
            PlayerSaveState saveState = saveObject.AddComponent<PlayerSaveState>();
            saveState.SetDayProgressForTesting(1, 1);
            saveState.SetIdentityForTesting("STUDENT", "BBA", true);

            try
            {
                menu.Open();
                FindButton("Button_ReportCard").onClick.Invoke();

                Assert.That(GameMenuManager.IsOpen, Is.False, "Report Card must close the menu for admitted students.");
                Assert.That(ReportCardUI.Instance, Is.Not.Null);
            }
            finally
            {
                if (ReportCardUI.Instance != null)
                {
                    ReportCardUI.Instance.CloseModal();
                    Object.DestroyImmediate(ReportCardUI.Instance.gameObject);
                }

                Object.DestroyImmediate(saveObject);
            }
        }

        [Test]
        public void Test08e_ReportCard_DeniedForNonStudent()
        {
            if (PlayerSaveState.Instance != null)
            {
                Object.DestroyImmediate(PlayerSaveState.Instance.gameObject);
            }

            GameObject saveObject = new GameObject("TestPlayerSaveState");
            PlayerSaveState saveState = saveObject.AddComponent<PlayerSaveState>();
            saveState.SetDayProgressForTesting(1, 1);
            saveState.SetIdentityForTesting("FACULTY", "CSE", true);

            try
            {
                menu.Open();
                FindButton("Button_ReportCard").onClick.Invoke();

                Assert.That(GameMenuManager.IsOpen, Is.True);
                TextMeshProUGUI status = FindNamedTmp("StatusLabel");
                Assert.That(status, Is.Not.Null);
                Assert.That(status.text, Does.Contain("students").IgnoreCase);
                Assert.That(status.text, Does.Not.Contain("CSE"));
            }
            finally
            {
                Object.DestroyImmediate(saveObject);
            }
        }

        [Test]
        public void Test09_DoesNotOpenWhenElevatorModalIsOpen()
        {
            GameObject elevatorObject = new GameObject("TestElevatorUI");
            ElevatorUI elevatorUI = elevatorObject.AddComponent<ElevatorUI>();
            try
            {
                elevatorUI.Show(1, 0);
                Assert.That(ElevatorUI.IsOpen, Is.True);

                menu.HandleEscape();

                Assert.That(GameMenuManager.IsOpen, Is.False, "Game menu must not steal Escape from an open elevator modal.");
            }
            finally
            {
                if (ElevatorUI.IsOpen)
                {
                    elevatorUI.Hide();
                }

                Object.DestroyImmediate(elevatorObject);
            }
        }

        [Test]
        public void Test10_SoftPause_DoesNotChangeTimeScaleOnOpenOrClose()
        {
            Time.timeScale = 1f;
            menu.Open();
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            menu.Resume();
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [Test]
        public void Test11_FacultyMenu_IsIndependentAndHidesReportCard()
        {
            GameObject saveObject = CreateFacultySave();
            try
            {
                menu.Open();

                Assert.That(FindNamedTmp("Title").text, Is.EqualTo("FACULTY MENU"));
                Assert.That(FindButton("Button_ReportCard").gameObject.activeSelf, Is.False);
                Assert.That(FindButton("Button_NextDay").gameObject.activeSelf, Is.True);
                Assert.That(FindButton("Button_IdCard").gameObject.activeSelf, Is.True);
                Assert.That(FindButton("Button_ClassRoutine").gameObject.activeSelf, Is.True);
                Assert.That(FindButton("Button_Resume").gameObject.activeSelf, Is.True);
                Assert.That(FindButton("Button_Settings").gameObject.activeSelf, Is.True);
                Assert.That(FindButton("Button_Logout").gameObject.activeSelf, Is.True);
                Assert.That(FindButton("Button_FacultyAttendance"), Is.Null);
                Assert.That(FindButton("Button_CourseManagement"), Is.Null);
                Assert.That(FindButton("Button_MaterialsManagement"), Is.Null);

                FindButton("Button_NextDay").onClick.Invoke();
                Assert.That(FindNamedTmp("StatusLabel").text, Is.EqualTo("Coming Soon"));
                Assert.That(GameMenuManager.IsOpen, Is.True);

                FindButton("Button_Settings").onClick.Invoke();
                Assert.That(FindNamedTmp("StatusLabel").text, Does.Contain("coming soon").IgnoreCase);
                Assert.That(GameMenuManager.IsOpen, Is.True);

                FindButton("Button_Resume").onClick.Invoke();
                Assert.That(GameMenuManager.IsOpen, Is.False);
                Assert.That(playerMovement.enabled, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(saveObject);
            }
        }

        [Test]
        public void Test12_FacultyClassRoutine_OpensExistingFacultyRoutine()
        {
            GameObject saveObject = CreateFacultySave();
            try
            {
                menu.Open();
                FindButton("Button_ClassRoutine").onClick.Invoke();

                Assert.That(GameMenuManager.IsOpen, Is.True);
                Assert.That(ClassRoutineUI.IsOpen, Is.True);
                Assert.That(FindRoutineLabel("Title").text, Is.EqualTo(ClassRoutineUI.DefaultTitle));
                Assert.That(FindRoutineCourse("ICS", "CourseValue").text, Is.EqualTo("Introduction to Computer Science"));
                Assert.That(FindRoutineCourse("ICS", "ClassroomValue").text, Is.EqualTo("Room 427"));
                Assert.That(FindRoutineCourse("ICS", "FloorValue").text, Is.EqualTo("4"));
                Assert.That(FindRoutineCourse("DM", "CourseValue").text, Is.EqualTo("Discrete Mathematics"));
                Assert.That(FindRoutineCourse("DM", "ClassroomValue").text, Is.EqualTo("Room 423"));
                Assert.That(FindRoutineCourse("DM", "FloorValue").text, Is.EqualTo("4"));
                Assert.That(FindNamedChild(ClassRoutineUI.Instance.transform, "Routine_ENGLISH"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(saveObject);
            }
        }

        [Test]
        public void Test13_FacultyIdCard_OpensExistingFacultyIdentityCard()
        {
            GameObject saveObject = CreateFacultySave();
            try
            {
                menu.Open();
                FindButton("Button_IdCard").onClick.Invoke();

                Assert.That(GameMenuManager.IsOpen, Is.True);
                Assert.That(IdCardUI.IsOpen, Is.True);
                Assert.That(FindLabel(IdCardUI.Instance.transform, "NameValue").text, Is.EqualTo("Lail"));
                Assert.That(FindLabel(IdCardUI.Instance.transform, "IdKey").text, Is.EqualTo("Faculty ID"));
                Assert.That(FindLabel(IdCardUI.Instance.transform, "IdValue").text, Is.EqualTo("F-001"));
                Assert.That(FindLabel(IdCardUI.Instance.transform, "DeptValue").text, Is.EqualTo("CSE"));
                Assert.That(FindLabel(IdCardUI.Instance.transform, "DesignationValue").text, Is.EqualTo(FacultyIdentity.Designation));
                Assert.That(FindLabel(IdCardUI.Instance.transform, "OfficeValue").text, Is.EqualTo(FacultyIdentity.FormatOffice()));
            }
            finally
            {
                if (IdCardUI.IsOpen && IdCardUI.Instance != null)
                {
                    IdCardUI.Instance.Hide();
                }

                Object.DestroyImmediate(saveObject);
            }
        }

        private GameObject CreateVisitorSave()
        {
            if (PlayerSaveState.Instance != null)
            {
                Object.DestroyImmediate(PlayerSaveState.Instance.gameObject);
            }

            GameObject saveObject = new GameObject("TestVisitorSaveState");
            PlayerSaveState saveState = saveObject.AddComponent<PlayerSaveState>();
            saveState.SetStateForTesting(hasSaveValue: false, idCardIssuedValue: false);
            return saveObject;
        }

        private GameObject CreateStudentSave()
        {
            if (PlayerSaveState.Instance != null)
            {
                Object.DestroyImmediate(PlayerSaveState.Instance.gameObject);
            }

            GameObject saveObject = new GameObject("TestStudentSaveState");
            PlayerSaveState saveState = saveObject.AddComponent<PlayerSaveState>();
            saveState.ApplyCreatedSave(new ApiClient.PlayerSaveStatusDto
            {
                hasSave = true,
                save = new ApiClient.PlayerSaveDto
                {
                    playerName = "Alex Student",
                    role = "STUDENT",
                    department = "CSE",
                    universityId = "22112345",
                    admissionCompleted = true,
                    idCardIssued = true,
                    semester = 1,
                    currentDay = 1
                }
            });
            return saveObject;
        }

        private GameObject CreateFacultySave()
        {
            if (PlayerSaveState.Instance != null)
            {
                Object.DestroyImmediate(PlayerSaveState.Instance.gameObject);
            }

            GameObject saveObject = new GameObject("TestFacultySaveState");
            PlayerSaveState saveState = saveObject.AddComponent<PlayerSaveState>();
            saveState.ApplyCreatedSave(new ApiClient.PlayerSaveStatusDto
            {
                hasSave = true,
                save = new ApiClient.PlayerSaveDto
                {
                    playerName = "Lail",
                    role = FacultyIdentity.Role,
                    department = "CSE",
                    universityId = "F-001",
                    admissionCompleted = true,
                    idCardIssued = true,
                    semester = 1,
                    currentDay = 1
                }
            });
            return saveObject;
        }

        private Button FindButton(string objectName)
        {
            Button[] buttons = menu.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].gameObject.name == objectName)
                {
                    return buttons[i];
                }
            }

            return null;
        }

        private string FindButtonLabel(string objectName)
        {
            Button button = FindButton(objectName);
            if (button == null)
            {
                return null;
            }

            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            return label != null ? label.text : null;
        }

        private TextMeshProUGUI FindNamedTmp(string objectName)
        {
            TextMeshProUGUI[] labels = menu.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null && labels[i].gameObject.name == objectName)
                {
                    return labels[i];
                }
            }

            return null;
        }

        private TextMeshProUGUI FindTmpText(string text)
        {
            TextMeshProUGUI[] labels = menu.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null && labels[i].text == text)
                {
                    return labels[i];
                }
            }

            return null;
        }

        private static TextMeshProUGUI FindRoutineLabel(string objectName)
        {
            if (ClassRoutineUI.Instance == null)
            {
                return null;
            }

            return FindLabel(ClassRoutineUI.Instance.transform, objectName);
        }

        private static TextMeshProUGUI FindRoutineCourse(string courseId, string objectName)
        {
            if (ClassRoutineUI.Instance == null)
            {
                return null;
            }

            Transform card = FindNamedChild(ClassRoutineUI.Instance.transform, "Routine_" + courseId);
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
    }
}
