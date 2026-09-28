using NUnit.Framework;
using TMPro;
using UIU.Simulator.Gameplay.Admission;
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
    public sealed class WelcomeCardUITests
    {
        private GameObject playerObject;
        private StatsHUD statsHud;
        private FacultyHUD facultyHud;
        private PlayerSaveState saveState;
        private WelcomeCardUI welcome;

        [SetUp]
        public void SetUp()
        {
            DestroyExisting(WelcomeCardUI.Instance != null ? WelcomeCardUI.Instance.gameObject : null);
            DestroyExisting(AdmissionUI.Instance != null ? AdmissionUI.Instance.gameObject : null);
            DestroyExisting(PlayerSaveState.Instance != null ? PlayerSaveState.Instance.gameObject : null);
            DestroyExisting(FacultyProgress.Instance != null ? FacultyProgress.Instance.gameObject : null);
            DestroyExisting(FacultyHUD.Instance != null ? FacultyHUD.Instance.gameObject : null);
            DestroyExisting(StatsHUD.Instance != null ? StatsHUD.Instance.gameObject : null);

            playerObject = new GameObject("WelcomeCardTestPlayer");
            playerObject.AddComponent<PlayerStats>();
            statsHud = playerObject.AddComponent<StatsHUD>();
            saveState = playerObject.AddComponent<PlayerSaveState>();
            playerObject.AddComponent<FacultyProgress>();
            facultyHud = playerObject.AddComponent<FacultyHUD>();

            welcome = WelcomeCardUI.EnsureExists();
        }

        [TearDown]
        public void TearDown()
        {
            if (WelcomeCardUI.IsOpen && WelcomeCardUI.Instance != null)
            {
                WelcomeCardUI.Instance.Hide();
            }

            if (AdmissionUI.IsOpen && AdmissionUI.Instance != null)
            {
                AdmissionUI.Instance.Hide();
            }

            DestroyExisting(welcome != null ? welcome.gameObject : null);
            DestroyExisting(AdmissionUI.Instance != null ? AdmissionUI.Instance.gameObject : null);
            DestroyExisting(playerObject);
        }

        [Test]
        public void FreshVisitor_ShowsWelcomeCard_AndHidesBothHuds()
        {
            saveState.SetStateForTesting(hasSaveValue: false, idCardIssuedValue: false);
            facultyHud.ApplyRoleVisibility();
            welcome.EvaluateForCurrentSave();

            Assert.That(WelcomeCardUI.IsOpen, Is.True);
            Assert.That(FindLabel(welcome.transform, "Title").text, Is.EqualTo("WELCOME TO UNITED INTERNATIONAL UNIVERSITY"));
            Assert.That(FindLabel(welcome.transform, "Message").text, Does.Contain("receptionist"));
            Assert.That(FindButton(welcome.transform, "Button_GetIdCard"), Is.Not.Null);
            Assert.That(FindButton(welcome.transform, "Button_CloseWelcome"), Is.Not.Null);
            Assert.That(FindButtonLabel(welcome.transform, "Button_GetIdCard"), Is.EqualTo("GET YOUR ID CARD"));
            Assert.That(FindButtonLabel(welcome.transform, "Button_CloseWelcome"), Is.EqualTo("CLOSE"));
            Assert.That(FindButton(welcome.transform, "Button_GoToReceptionist").gameObject.activeSelf, Is.False);
            Assert.That(statsHud.IsVisible, Is.False);
            Assert.That(facultyHud.IsVisible, Is.False);
        }

        [Test]
        public void CloseWelcome_KeepsHudsHidden_UntilIdCard()
        {
            saveState.SetStateForTesting(hasSaveValue: false, idCardIssuedValue: false);
            welcome.EvaluateForCurrentSave();
            Assert.That(WelcomeCardUI.IsOpen, Is.True);

            FindButton(welcome.transform, "Button_CloseWelcome").onClick.Invoke();

            Assert.That(WelcomeCardUI.IsOpen, Is.False);
            Assert.That(statsHud.IsVisible, Is.False);
            Assert.That(facultyHud.IsVisible, Is.False);

            welcome.EvaluateForCurrentSave();
            Assert.That(WelcomeCardUI.IsOpen, Is.False, "Close must not reopen the welcome card for this visit.");
        }

        [Test]
        public void GetYourIdCard_ShowsReceptionistGuide_DoesNotOpenAdmission()
        {
            saveState.SetStateForTesting(hasSaveValue: false, idCardIssuedValue: false);
            welcome.EvaluateForCurrentSave();

            GameObject receptionistObject = new GameObject("TestReceptionist");
            receptionistObject.AddComponent<BoxCollider>();
            receptionistObject.AddComponent<Receptionist>();
            try
            {
                FindButton(welcome.transform, "Button_GetIdCard").onClick.Invoke();

                Assert.That(WelcomeCardUI.IsOpen, Is.True, "Guide card must stay open.");
                Assert.That(AdmissionUI.IsOpen, Is.False, "Registration must remain receptionist-only.");
                Assert.That(FindLabel(welcome.transform, "Message").text, Is.EqualTo("Please visit the receptionist to get your ID card."));
                Assert.That(FindButton(welcome.transform, "Button_GetIdCard").gameObject.activeSelf, Is.False);
                Assert.That(FindButton(welcome.transform, "Button_GoToReceptionist").gameObject.activeSelf, Is.True);
                Assert.That(FindButtonLabel(welcome.transform, "Button_GoToReceptionist"), Is.EqualTo("GO TO RECEPTIONIST"));
                Assert.That(FindButton(welcome.transform, "Button_CloseWelcome").gameObject.activeSelf, Is.True);
                Assert.That(statsHud.IsVisible, Is.False);
                Assert.That(facultyHud.IsVisible, Is.False);

                FindButton(welcome.transform, "Button_GoToReceptionist").onClick.Invoke();

                Assert.That(WelcomeCardUI.IsOpen, Is.False);
                Assert.That(AdmissionUI.IsOpen, Is.False);
            }
            finally
            {
                DestroyExisting(receptionistObject);
            }
        }

        [Test]
        public void GetYourIdCard_CloseFromGuide_DoesNotOpenAdmission()
        {
            saveState.SetStateForTesting(hasSaveValue: false, idCardIssuedValue: false);
            welcome.EvaluateForCurrentSave();
            FindButton(welcome.transform, "Button_GetIdCard").onClick.Invoke();

            FindButton(welcome.transform, "Button_CloseWelcome").onClick.Invoke();

            Assert.That(WelcomeCardUI.IsOpen, Is.False);
            Assert.That(AdmissionUI.IsOpen, Is.False);
        }

        [Test]
        public void StudentIdIssued_ShowsOnlyStudentHud()
        {
            saveState.SetStateForTesting(hasSaveValue: false, idCardIssuedValue: false);
            welcome.EvaluateForCurrentSave();
            Assert.That(WelcomeCardUI.IsOpen, Is.True);

            ApplySave("STUDENT", "Alex", "22112345");
            facultyHud.ApplyRoleVisibility();
            welcome.EvaluateForCurrentSave();

            Assert.That(WelcomeCardUI.IsOpen, Is.False);
            Assert.That(statsHud.IsVisible, Is.True);
            Assert.That(facultyHud.IsVisible, Is.False);
        }

        [Test]
        public void FacultyIdIssued_ShowsOnlyFacultyHud()
        {
            saveState.SetStateForTesting(hasSaveValue: false, idCardIssuedValue: false);
            welcome.EvaluateForCurrentSave();

            ApplySave(FacultyIdentity.Role, "Lail", "F-001");
            facultyHud.ApplyRoleVisibility();
            welcome.EvaluateForCurrentSave();

            Assert.That(WelcomeCardUI.IsOpen, Is.False);
            Assert.That(facultyHud.IsVisible, Is.True);
            Assert.That(statsHud.IsVisible, Is.False);
        }

        private void ApplySave(string role, string playerName, string universityId)
        {
            saveState.ApplyCreatedSave(new ApiClient.PlayerSaveStatusDto
            {
                hasSave = true,
                save = new ApiClient.PlayerSaveDto
                {
                    role = role,
                    playerName = playerName,
                    department = "CSE",
                    universityId = universityId,
                    admissionCompleted = true,
                    idCardIssued = true,
                    semester = 1,
                    currentDay = 1
                }
            });
        }

        private static Button FindButton(Transform root, string objectName)
        {
            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].gameObject.name == objectName)
                {
                    return buttons[i];
                }
            }

            return null;
        }

        private static string FindButtonLabel(Transform root, string objectName)
        {
            Button button = FindButton(root, objectName);
            if (button == null)
            {
                return null;
            }

            TextMeshProUGUI tmp = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null)
            {
                return tmp.text;
            }

            Text legacy = button.GetComponentInChildren<Text>(true);
            return legacy != null ? legacy.text : null;
        }

        private static TextMeshProUGUI FindLabel(Transform root, string objectName)
        {
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

        private static void DestroyExisting(GameObject target)
        {
            if (target != null)
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
