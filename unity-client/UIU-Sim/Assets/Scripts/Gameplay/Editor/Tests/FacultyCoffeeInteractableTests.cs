using NUnit.Framework;
using UIU.Simulator.Gameplay.Faculty;
using UIU.Simulator.Gameplay.IDCard;
using UIU.Simulator.Gameplay.Player;
using UIU.Simulator.Networking;
using UnityEngine;

namespace UIU.Simulator.Gameplay.Editor.Tests
{
    [TestFixture]
    public sealed class FacultyCoffeeInteractableTests
    {
        private GameObject host;
        private PlayerSaveState saveState;
        private FacultyProgress facultyProgress;
        private FacultyCoffeeInteractable coffee;
        private GameObject dialogueHost;

        [SetUp]
        public void SetUp()
        {
            DestroyExisting(PlayerSaveState.Instance != null ? PlayerSaveState.Instance.gameObject : null);
            DestroyExisting(FacultyProgress.Instance != null ? FacultyProgress.Instance.gameObject : null);
            DestroyExisting(DialogueUI.Instance != null ? DialogueUI.Instance.gameObject : null);

            host = new GameObject("FacultyCoffeeTestHost");
            host.AddComponent<BoxCollider>();
            saveState = host.AddComponent<PlayerSaveState>();
            facultyProgress = host.AddComponent<FacultyProgress>();
            coffee = host.AddComponent<FacultyCoffeeInteractable>();

            dialogueHost = new GameObject("DialogueUIHost");
            dialogueHost.AddComponent<DialogueUI>();
        }

        [TearDown]
        public void TearDown()
        {
            if (DialogueUI.IsOpen && DialogueUI.Instance != null)
            {
                DialogueUI.Instance.Hide();
            }

            DestroyExisting(host);
            DestroyExisting(dialogueHost);
            DestroyExisting(DialogueUI.Instance != null ? DialogueUI.Instance.gameObject : null);
        }

        [Test]
        public void StudentIsBlockedFromCoffeeBooth()
        {
            ApplyStudentSave();
            CompleteBothClassesLocally();

            string response = coffee.Interact();

            Assert.That(response, Does.Contain("Faculty only"));
            Assert.That(DialogueUI.IsOpen, Is.False);
        }

        [Test]
        public void FacultyWithoutIdIsBlocked()
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
                    idCardIssued = false,
                    semester = 1,
                    currentDay = 1
                }
            });
            CompleteBothClassesLocally();

            string response = coffee.Interact();

            Assert.That(response, Does.Contain("Faculty ID"));
            Assert.That(DialogueUI.IsOpen, Is.False);
        }

        [Test]
        public void FacultyBeforeBothClassesIsBlocked()
        {
            ApplyFacultySave();
            facultyProgress.SetStateForTesting(
                65,
                icsCompletedValue: true,
                dmCompletedValue: false,
                facultyIdIssuedValue: true);

            string response = coffee.Interact();

            Assert.That(response, Does.Contain("after your classes"));
            Assert.That(DialogueUI.IsOpen, Is.False);
        }

        [Test]
        public void FacultyAfterBothClassesOpensDialogueChoices()
        {
            ApplyFacultySave();
            CompleteBothClassesLocally();

            string response = coffee.Interact();

            Assert.That(response, Is.Null);
            Assert.That(DialogueUI.IsOpen, Is.True);
        }

        [Test]
        public void AlreadyClaimedShowsMessageWithoutDialogue()
        {
            ApplyFacultySave();
            CompleteBothClassesLocally();
            facultyProgress.SetStateForTesting(
                71,
                icsCompletedValue: true,
                dmCompletedValue: true,
                facultyIdIssuedValue: true,
                coffeeClaimedForCurrentDayValue: true,
                coffeeOptionValue: "CAPPUCCINO");

            string response = coffee.Interact();

            Assert.That(response, Does.Contain("already"));
            Assert.That(DialogueUI.IsOpen, Is.False);
        }

        [Test]
        public void ImplementsIInteractableWithCoffeePrompt()
        {
            Assert.That(coffee, Is.InstanceOf<IInteractable>());
            Assert.That(coffee.InteractionPrompt, Is.EqualTo("Get Coffee"));
        }

        private void CompleteBothClassesLocally()
        {
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

        private static void DestroyExisting(GameObject go)
        {
            if (go != null)
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
