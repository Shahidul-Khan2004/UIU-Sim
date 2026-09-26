using NUnit.Framework;
using TMPro;
using UIU.Simulator.Gameplay.IDCard;
using UIU.Simulator.Gameplay.Player;
using UIU.Simulator.Networking;
using UnityEngine;

namespace UIU.Simulator.Gameplay.Editor.Tests
{
    [TestFixture]
    public sealed class IdCardUITests
    {
        private GameObject saveObject;
        private PlayerSaveState saveState;

        [SetUp]
        public void SetUp()
        {
            DestroyExisting(PlayerSaveState.Instance != null ? PlayerSaveState.Instance.gameObject : null);
            DestroyExisting(IdCardUI.Instance != null ? IdCardUI.Instance.gameObject : null);

            saveObject = new GameObject("TestPlayerSaveState");
            saveState = saveObject.AddComponent<PlayerSaveState>();
        }

        [TearDown]
        public void TearDown()
        {
            if (IdCardUI.IsOpen && IdCardUI.Instance != null)
            {
                IdCardUI.Instance.Hide();
            }

            DestroyExisting(IdCardUI.Instance != null ? IdCardUI.Instance.gameObject : null);
            DestroyExisting(saveObject);
        }

        [Test]
        public void StudentCard_ShowsExistingFields_HidesFacultyRows()
        {
            ApplySave("Alex Student", "STUDENT", "CSE", "22112345", idCardIssued: true);

            IdCardUI card = IdCardUI.EnsureExists();
            card.Show();

            Assert.That(IdCardUI.IsOpen, Is.True);
            Assert.That(FindLabel("Header").text, Is.EqualTo("ID Card"));
            Assert.That(FindLabel("NameValue").text, Is.EqualTo("Alex Student"));
            Assert.That(FindLabel("RoleValue").text, Is.EqualTo("Student"));
            Assert.That(FindLabel("DeptValue").text, Is.EqualTo("CSE"));
            Assert.That(FindLabel("IdKey").text, Is.EqualTo("Student ID"));
            Assert.That(FindLabel("IdValue").text, Is.EqualTo("22112345"));

            TextMeshProUGUI designationKey = FindLabel("DesignationKey");
            TextMeshProUGUI officeKey = FindLabel("OfficeKey");
            Assert.That(designationKey, Is.Not.Null);
            Assert.That(officeKey, Is.Not.Null);
            Assert.That(designationKey.gameObject.activeSelf, Is.False);
            Assert.That(officeKey.gameObject.activeSelf, Is.False);
            Assert.That(FindLabel("DesignationValue").gameObject.activeSelf, Is.False);
            Assert.That(FindLabel("OfficeValue").gameObject.activeSelf, Is.False);

            RectTransform panel = FindPanel();
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.sizeDelta.y, Is.EqualTo(420f));
        }

        [Test]
        public void FacultyCard_ShowsAssignedIdentityFields()
        {
            ApplySave("Test Faculty", FacultyIdentity.Role, "CSE", "F-001", idCardIssued: true);

            IdCardUI card = IdCardUI.EnsureExists();
            card.Show();

            Assert.That(IdCardUI.IsOpen, Is.True);
            Assert.That(FindLabel("Header").text, Is.EqualTo("Faculty ID Card"));
            Assert.That(FindLabel("NameValue").text, Is.EqualTo("Test Faculty"));
            Assert.That(FindLabel("RoleValue").text, Is.EqualTo("Faculty"));
            Assert.That(FindLabel("DeptValue").text, Is.EqualTo("CSE"));
            Assert.That(FindLabel("IdKey").text, Is.EqualTo("Faculty ID"));
            Assert.That(FindLabel("IdValue").text, Is.EqualTo("F-001"));
            Assert.That(FindLabel("DesignationKey").text, Is.EqualTo("Designation"));
            Assert.That(FindLabel("DesignationValue").text, Is.EqualTo(FacultyIdentity.Designation));
            Assert.That(FindLabel("OfficeKey").text, Is.EqualTo("Office"));
            Assert.That(FindLabel("OfficeValue").text, Is.EqualTo(FacultyIdentity.FormatOffice()));
            Assert.That(FindLabel("DesignationValue").gameObject.activeSelf, Is.True);
            Assert.That(FindLabel("OfficeValue").gameObject.activeSelf, Is.True);

            RectTransform panel = FindPanel();
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.sizeDelta.y, Is.EqualTo(548f));
        }

        [Test]
        public void NotIssued_ShowsReceptionistStatus_HidesFacultyRows()
        {
            saveState.SetStateForTesting(hasSaveValue: false, idCardIssuedValue: false);

            IdCardUI card = IdCardUI.EnsureExists();
            card.Show();

            Assert.That(FindLabel("Header").text, Does.Contain("receptionist").IgnoreCase);
            Assert.That(FindLabel("NameValue").text, Is.EqualTo("—"));
            Assert.That(FindLabel("RoleValue").text, Is.EqualTo("—"));
            Assert.That(FindLabel("DeptValue").text, Is.EqualTo("—"));
            Assert.That(FindLabel("IdValue").text, Is.EqualTo("—"));
            Assert.That(FindLabel("IdKey").text, Is.EqualTo("Student ID"));
            Assert.That(FindLabel("DesignationKey").gameObject.activeSelf, Is.False);
            Assert.That(FindLabel("OfficeKey").gameObject.activeSelf, Is.False);
        }

        [Test]
        public void FacultyIdentity_UsesAssignedConstants()
        {
            Assert.That(FacultyIdentity.Matches("FACULTY"), Is.True);
            Assert.That(FacultyIdentity.Matches("faculty"), Is.True);
            Assert.That(FacultyIdentity.Matches("STUDENT"), Is.False);
            Assert.That(FacultyIdentity.Matches(null), Is.False);
            Assert.That(FacultyIdentity.Designation, Is.EqualTo("Lecturer"));
            Assert.That(FacultyIdentity.OfficeRoom, Is.EqualTo("335"));
            Assert.That(FacultyIdentity.FormatOffice(), Is.EqualTo("Room 335"));
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

        private static TextMeshProUGUI FindLabel(string objectName)
        {
            if (IdCardUI.Instance == null)
            {
                return null;
            }

            TextMeshProUGUI[] labels = IdCardUI.Instance.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null && labels[i].gameObject.name == objectName)
                {
                    return labels[i];
                }
            }

            return null;
        }

        private static RectTransform FindPanel()
        {
            if (IdCardUI.Instance == null)
            {
                return null;
            }

            Transform panel = IdCardUI.Instance.transform.Find("IdCardCanvas/IdCardOverlay/IdCardPanel");
            return panel != null ? panel.GetComponent<RectTransform>() : null;
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
