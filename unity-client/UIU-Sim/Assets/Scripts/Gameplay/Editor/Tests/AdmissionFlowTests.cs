#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;
using UIU.Simulator.Gameplay.Admission;
using UIU.Simulator.Gameplay.Player;
using UnityEngine;

namespace UIU.Simulator.Gameplay.Tests
{
    [TestFixture]
    public sealed class AdmissionFlowTests
    {
        private GameObject saveStateObject;
        private PlayerSaveState saveState;
        private GameObject scannerObject;
        private IDScanner scanner;
        private GameObject inventoryObject;
        private PlayerInventory inventory;
        private GameObject receptionistObject;
        private Receptionist receptionist;

        [SetUp]
        public void SetUp()
        {
            saveStateObject = new GameObject("TestPlayerSaveState");
            saveState = saveStateObject.AddComponent<PlayerSaveState>();
            InvokeMethod(saveState, "Awake");

            inventoryObject = new GameObject("TestInventory");
            inventory = inventoryObject.AddComponent<PlayerInventory>();
            InvokeMethod(inventory, "Awake");

            scannerObject = new GameObject("TestScanner");
            scannerObject.AddComponent<BoxCollider>();
            scannerObject.AddComponent<AudioSource>();
            scannerObject.AddComponent<InteractionFeedback>();
            scanner = scannerObject.AddComponent<IDScanner>();
            InvokeMethod(scanner, "Awake");

            receptionistObject = new GameObject("TestReceptionist");
            receptionistObject.AddComponent<BoxCollider>();
            receptionist = receptionistObject.AddComponent<Receptionist>();
        }

        [TearDown]
        public void TearDown()
        {
            if (AdmissionUI.Instance != null)
            {
                AdmissionUI.Instance.Hide();
                Object.DestroyImmediate(AdmissionUI.Instance.gameObject);
            }

            if (scannerObject != null) Object.DestroyImmediate(scannerObject);
            if (receptionistObject != null) Object.DestroyImmediate(receptionistObject);
            if (inventoryObject != null) Object.DestroyImmediate(inventoryObject);
            if (saveStateObject != null) Object.DestroyImmediate(saveStateObject);
        }

        [Test]
        public void Scanner_NeedsAdmission_DeniesEntry()
        {
            saveState.SetStateForTesting(hasSaveValue: false, idCardIssuedValue: false);

            string response = scanner.Interact();

            Assert.That(response, Is.EqualTo("You do not have an ID card yet. Please visit the receptionist."));
        }

        [Test]
        public void Scanner_IdCardIssued_AllowsExistingInventoryPath()
        {
            saveState.SetStateForTesting(hasSaveValue: true, idCardIssuedValue: true);
            inventory.SetTriggeredInitialIDFailure(true);
            inventory.ResolveIDProblem();
            scanner.PermanentFailChance = 0f;

            string response = scanner.Interact();

            Assert.That(response, Is.EqualTo("Access granted. Welcome!"));
        }

        [Test]
        public void Receptionist_NeedsAdmission_OpensAdmissionPanel()
        {
            saveState.SetStateForTesting(hasSaveValue: false, idCardIssuedValue: false);

            string response = receptionist.Interact();

            Assert.That(response, Is.Null);
            Assert.That(AdmissionUI.IsOpen, Is.True);
        }

        private static void InvokeMethod(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            method?.Invoke(target, null);
        }
    }
}
#endif
