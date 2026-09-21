using NUnit.Framework;
using TMPro;
using UIU.Simulator.Gameplay.Elevator;
using UIU.Simulator.Gameplay.IDCard;
using UIU.Simulator.Gameplay.Player;
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
            if (GameMenuManager.IsOpen && menu != null)
            {
                menu.Close(restoreGameplayControls: true);
            }

            Time.timeScale = 1f;

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
            menu.Open();

            Assert.That(FindButton("Button_Resume"), Is.Not.Null);
            Assert.That(FindButton("Button_SaveGame"), Is.Not.Null);
            Assert.That(FindButton("Button_NewGame"), Is.Not.Null);
            Assert.That(FindButton("Button_IdCard"), Is.Not.Null);
            Assert.That(FindButton("Button_ClassRoutine"), Is.Not.Null);
            Assert.That(FindButton("Button_Settings"), Is.Not.Null);
            Assert.That(FindButton("Button_Logout"), Is.Not.Null);
            Assert.That(FindButton("Button_QuitGame"), Is.Not.Null);

            Assert.That(FindButtonLabel("Button_Resume"), Is.EqualTo("Resume"));
            Assert.That(FindButtonLabel("Button_SaveGame"), Is.EqualTo("Save Game"));
            Assert.That(FindButtonLabel("Button_NewGame"), Is.EqualTo("New Game"));
            Assert.That(FindButtonLabel("Button_IdCard"), Is.EqualTo("ID Card"));
            Assert.That(FindButtonLabel("Button_ClassRoutine"), Is.EqualTo("Class Routine"));
            Assert.That(FindButtonLabel("Button_Settings"), Is.EqualTo("Settings"));
            Assert.That(FindButtonLabel("Button_Logout"), Is.EqualTo("Logout"));
            Assert.That(FindButtonLabel("Button_QuitGame"), Is.EqualTo("Quit Game"));
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

            FindButton("Button_ClassRoutine").onClick.Invoke();
            FindButton("Button_Settings").onClick.Invoke();

            Assert.That(GameMenuManager.IsOpen, Is.True);
            Assert.That(playerMovement.enabled, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [Test]
        public void Test08b_IdCard_WithoutAdmission_ShowsStatusAndKeepsMenuOpen()
        {
            if (PlayerSaveState.Instance != null)
            {
                Object.DestroyImmediate(PlayerSaveState.Instance.gameObject);
            }

            GameObject saveObject = new GameObject("TestPlayerSaveState");
            PlayerSaveState saveState = saveObject.AddComponent<PlayerSaveState>();
            saveState.SetStateForTesting(hasSaveValue: false, idCardIssuedValue: false);

            try
            {
                menu.Open();
                FindButton("Button_IdCard").onClick.Invoke();

                Assert.That(GameMenuManager.IsOpen, Is.True);
                Assert.That(IdCardUI.IsOpen, Is.False);

                TextMeshProUGUI status = FindNamedTmp("StatusLabel");
                Assert.That(status, Is.Not.Null);
                Assert.That(status.text, Does.Contain("receptionist").IgnoreCase);
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
    }
}
