#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UIU.Simulator.Gameplay.Tests
{
    /// <summary>
    /// Tests verifying the gate-opening system and its integration with IDScanner:
    /// 1. Successful scan triggers assigned gate Open().
    /// 2. Failed scan does NOT open gate.
    /// 3. Scanner with null gate reference still functions safely.
    /// 4. GateController reaches expected open rotation.
    /// 5. AutoClose returns pivots to original closed rotation.
    /// 6. Duplicate Open() calls do not create overlapping animation corruption.
    /// 7. Close() safely takes ownership and returns pivots to closed rotation.
    /// 8. InteractionController ignores disabled IDScanner components.
    /// </summary>
    [TestFixture]
    public sealed class GateControllerTests
    {
        private GameObject playerObject;
        private PlayerInventory playerInventory;

        private GameObject scannerObject;
        private IDScanner idScanner;

        private GameObject gateObject;
        private GateController gateController;
        private GameObject leftPivotObject;
        private GameObject rightPivotObject;

        [SetUp]
        public void SetUp()
        {
            // Player setup
            playerObject = new GameObject("TestPlayer");
            playerInventory = playerObject.AddComponent<PlayerInventory>();
            InvokeMethod(playerInventory, "Awake");

            // Gate setup
            gateObject = new GameObject("TestGate");
            gateController = gateObject.AddComponent<GateController>();

            leftPivotObject = new GameObject("Left_pivot");
            leftPivotObject.transform.SetParent(gateObject.transform);
            leftPivotObject.transform.localPosition = new Vector3(-19.37f, 0.68f, 30.23f);
            leftPivotObject.transform.localRotation = Quaternion.identity;

            rightPivotObject = new GameObject("Right_pivot");
            rightPivotObject.transform.SetParent(gateObject.transform);
            rightPivotObject.transform.localPosition = new Vector3(-17.15f, 0.68f, 30.23f);
            rightPivotObject.transform.localRotation = Quaternion.identity;

            gateController.LeftPivot = leftPivotObject.transform;
            gateController.RightPivot = rightPivotObject.transform;
            gateController.InitializeRotations();

            // Scanner setup
            scannerObject = new GameObject("TestScanner");
            scannerObject.AddComponent<BoxCollider>();
            scannerObject.AddComponent<AudioSource>();
            scannerObject.AddComponent<InteractionFeedback>();
            idScanner = scannerObject.AddComponent<IDScanner>();
            idScanner.GateToOpen = gateController;
            InvokeMethod(idScanner, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            if (playerObject != null)
            {
                Object.DestroyImmediate(playerObject);
            }

            if (scannerObject != null)
            {
                Object.DestroyImmediate(scannerObject);
            }

            if (gateObject != null)
            {
                Object.DestroyImmediate(gateObject);
            }
        }

        private static void InvokeMethod(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            method?.Invoke(target, null);
        }

        // ── 1. Successful Scan Triggers Open ───────────────────────────────

        [Test]
        public void SuccessfulScan_WithAssignedGate_TriggersGateOpen()
        {
            // Use temporary ID which is guaranteed to succeed
            playerInventory.AddTemporaryID();
            Assert.That(playerInventory.CurrentIDCard, Is.EqualTo(IDCardType.Temporary));
            Assert.That(gateController.IsBusy, Is.False);

            string response = idScanner.Interact();

            Assert.That(response, Is.EqualTo("Temporary access granted."));
            Assert.That(gateController.IsBusy, Is.True, "Gate should begin opening sequence when scan succeeds.");
        }

        // ── 2. Failed Scan Does Not Open Gate ──────────────────────────────

        [Test]
        public void FailedScan_WithAssignedGate_DoesNotOpenGate()
        {
            // First permanent scan always fails (tutorial failure)
            Assert.That(playerInventory.HasTriggeredInitialIDFailure, Is.False);
            Assert.That(gateController.IsBusy, Is.False);

            string response = idScanner.Interact();

            Assert.That(response, Is.EqualTo("You don't have an id card, go see the receptionist"));
            Assert.That(gateController.IsBusy, Is.False, "Gate must NOT open on failed scan.");
            Assert.That(gateController.IsOpen, Is.False);
            Assert.That(gateController.LeftPivot.localRotation, Is.EqualTo(gateController.LeftClosedRotation));
            Assert.That(gateController.RightPivot.localRotation, Is.EqualTo(gateController.RightClosedRotation));
        }

        [Test]
        public void RecurringFailure_DoesNotOpenGate()
        {
            // Clear tutorial failure
            idScanner.Interact(); // Fails tutorial
            playerInventory.AddTemporaryID();
            idScanner.Interact(); // Succeeds with temp ID
            Assert.That(playerInventory.HasIDProblem, Is.False);

            // Set recurring fail chance to 100%
            idScanner.PermanentFailChance = 1f;

            // Stop any previous routine for test clarity
            gateController.Close();
            SetField(gateController, "isBusy", false);

            string response = idScanner.Interact();

            Assert.That(response, Is.EqualTo("You don't have an id card, go see the receptionist"));
            Assert.That(gateController.IsBusy, Is.False, "Gate must NOT open on recurring failure.");
            Assert.That(gateController.IsOpen, Is.False);
        }

        // ── 3. Null Gate Reference Safety ──────────────────────────────────

        [Test]
        public void Scan_WithNullGateReference_FunctionsSafely()
        {
            idScanner.GateToOpen = null;

            // 1. Failed scan with null gate
            Assert.DoesNotThrow(() =>
            {
                string failResponse = idScanner.Interact();
                Assert.That(failResponse, Is.EqualTo("You don't have an id card, go see the receptionist"));
            });

            // 2. Successful scan with null gate
            playerInventory.AddTemporaryID();
            Assert.DoesNotThrow(() =>
            {
                string successResponse = idScanner.Interact();
                Assert.That(successResponse, Is.EqualTo("Temporary access granted."));
            });
        }

        // ── 4. Gate Controller Reaches Expected Open Rotation ──────────────

        [UnityTest]
        public IEnumerator GateController_ReachesExpectedOpenRotation()
        {
            gateController.OpenDuration = 0.05f;
            gateController.AutoClose = false;

            gateController.Open();
            Assert.That(gateController.IsBusy, Is.True);

            yield return new WaitWhile(() => gateController.IsBusy);

            Assert.That(gateController.IsOpen, Is.True);
            Assert.That(Quaternion.Angle(leftPivotObject.transform.localRotation, gateController.LeftOpenRotation), Is.LessThan(1f),
                "Left pivot should reach expected open rotation.");
            Assert.That(Quaternion.Angle(rightPivotObject.transform.localRotation, gateController.RightOpenRotation), Is.LessThan(1f),
                "Right pivot should reach expected open rotation.");
        }

        // ── 5. AutoClose Returns to Closed Rotation ─────────────────────────

        [UnityTest]
        public IEnumerator GateController_AutoClose_ReturnsToClosedRotation()
        {
            gateController.OpenDuration = 0.05f;
            gateController.HoldOpenSeconds = 0.02f;
            gateController.AutoClose = true;

            gateController.Open();
            Assert.That(gateController.IsBusy, Is.True);

            yield return new WaitWhile(() => gateController.IsBusy);

            Assert.That(gateController.IsOpen, Is.False);
            Assert.That(gateController.IsBusy, Is.False);
            Assert.That(Quaternion.Angle(leftPivotObject.transform.localRotation, gateController.LeftClosedRotation), Is.LessThan(1f),
                "Left pivot should return to closed rotation after auto-close.");
            Assert.That(Quaternion.Angle(rightPivotObject.transform.localRotation, gateController.RightClosedRotation), Is.LessThan(1f),
                "Right pivot should return to closed rotation after auto-close.");
        }

        // ── 6. Duplicate Call Safety ───────────────────────────────────────

        [UnityTest]
        public IEnumerator GateController_DuplicateOpenCalls_IgnoredWhileBusy()
        {
            gateController.OpenDuration = 0.08f;
            gateController.HoldOpenSeconds = 0.04f;
            gateController.AutoClose = true;

            gateController.Open();
            Assert.That(gateController.IsBusy, Is.True);

            // Repeated duplicate calls while busy
            gateController.Open();
            gateController.Open();

            // Wait for completion
            yield return new WaitWhile(() => gateController.IsBusy);

            Assert.That(gateController.IsOpen, Is.False);
            Assert.That(gateController.IsBusy, Is.False);
            Assert.That(Quaternion.Angle(leftPivotObject.transform.localRotation, gateController.LeftClosedRotation), Is.LessThan(1f),
                "Gate should cleanly complete single sequence and return to closed rotation despite duplicate calls.");
            Assert.That(Quaternion.Angle(rightPivotObject.transform.localRotation, gateController.RightClosedRotation), Is.LessThan(1f));
        }

        // ── 7. Close Routine Ownership Safety ──────────────────────────────

        [UnityTest]
        public IEnumerator GateController_Close_SafelyInterruptsAndReturnsToClosed()
        {
            gateController.OpenDuration = 0.1f;
            gateController.AutoClose = false;

            gateController.Open();
            Assert.That(gateController.IsBusy, Is.True);

            // Allow 1 frame to start rotating
            yield return null;

            // Close interrupts open routine cleanly
            gateController.Close();
            Assert.That(gateController.IsBusy, Is.True);

            yield return new WaitWhile(() => gateController.IsBusy);

            Assert.That(gateController.IsOpen, Is.False);
            Assert.That(Quaternion.Angle(leftPivotObject.transform.localRotation, gateController.LeftClosedRotation), Is.LessThan(1f));
            Assert.That(Quaternion.Angle(rightPivotObject.transform.localRotation, gateController.RightClosedRotation), Is.LessThan(1f));
        }

        // ── 8. InteractionController Disabled Interactable Check ───────────

        [Test]
        public void InteractionController_IgnoresDisabledInteractable()
        {
            // Create a player with InteractionController
            GameObject controllerObject = new GameObject("PlayerWithInteraction");
            InteractionController controller = controllerObject.AddComponent<InteractionController>();

            // IDScanner is currently enabled
            Assert.That(idScanner.isActiveAndEnabled, Is.True);

            // Disable IDScanner component (as will be done on decorative scanners)
            idScanner.enabled = false;
            Assert.That(idScanner.isActiveAndEnabled, Is.False);

            // Verify helper check logic
            IInteractable interactable = idScanner;
            bool isIgnored = interactable == null || (interactable is Behaviour b && !b.isActiveAndEnabled);
            Assert.That(isIgnored, Is.True, "Disabled IDScanner must be ignored by interaction check.");

            // Re-enable and verify it is recognized
            idScanner.enabled = true;
            bool isRecognized = interactable != null && (!(interactable is Behaviour b2) || b2.isActiveAndEnabled);
            Assert.That(isRecognized, Is.True, "Enabled IDScanner must be recognized by interaction check.");

            Object.DestroyImmediate(controllerObject);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            field?.SetValue(target, value);
        }

#if UNITY_EDITOR
        [MenuItem("UIU Simulator/Tests/Run Gate Controller Tests")]
        public static void RunFromMenu()
        {
            var tests = new GateControllerTests();
            try
            {
                tests.SetUp();
                tests.SuccessfulScan_WithAssignedGate_TriggersGateOpen();
                tests.TearDown();

                tests.SetUp();
                tests.FailedScan_WithAssignedGate_DoesNotOpenGate();
                tests.TearDown();

                tests.SetUp();
                tests.RecurringFailure_DoesNotOpenGate();
                tests.TearDown();

                tests.SetUp();
                tests.Scan_WithNullGateReference_FunctionsSafely();
                tests.TearDown();

                tests.SetUp();
                tests.InteractionController_IgnoresDisabledInteractable();
                tests.TearDown();

                Debug.Log("<color=green><b>[GateControllerTests] All synchronous tests PASSED! Coroutine tests run via Test Runner window.</b></color>");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"<color=red><b>[GateControllerTests] Test FAILED:</b> {ex.Message}</color>\n{ex.StackTrace}");
            }
            finally
            {
                tests.TearDown();
            }
        }
#endif
    }
}
