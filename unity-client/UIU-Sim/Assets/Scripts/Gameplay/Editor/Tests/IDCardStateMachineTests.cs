#if UNITY_EDITOR
using UnityEditor;
#endif
using NUnit.Framework;
using UnityEngine;

namespace UIU.Simulator.Gameplay.Tests
{
    /// <summary>
    /// Unit tests verifying the ID-card state machine rules across PlayerInventory and IDScanner:
    /// 1. Guaranteed first permanent-ID scan failure (tutorial beat).
    /// 2. Persistent failure while HasIDProblem is true.
    /// 3. Reception-issued temporary ID resolution and card consumption.
    /// 4. Post-tutorial recurring 10% failure triggering new HasIDProblem state.
    /// 5. Deterministic testability via configurable PermanentFailChance.
    /// </summary>
    [TestFixture]
    public sealed class IDCardStateMachineTests
    {
        private GameObject playerObject;
        private PlayerInventory playerInventory;
        private GameObject scannerObject;
        private IDScanner idScanner;

        [SetUp]
        public void SetUp()
        {
            // Set up player
            playerObject = new GameObject("TestPlayer");
            playerInventory = playerObject.AddComponent<PlayerInventory>();

            // Set up scanner with required components
            scannerObject = new GameObject("TestScanner");
            scannerObject.AddComponent<BoxCollider>();
            scannerObject.AddComponent<AudioSource>();
            scannerObject.AddComponent<InteractionFeedback>();
            idScanner = scannerObject.AddComponent<IDScanner>();
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
        }

        [Test]
        public void InitialState_HasPermanentID_NoProblem_InitialFailureNotTriggered()
        {
            Assert.That(playerInventory.HasPermanentID, Is.True);
            Assert.That(playerInventory.CurrentIDCard, Is.EqualTo(IDCardType.Permanent));
            Assert.That(playerInventory.HasIDProblem, Is.False);
            Assert.That(playerInventory.HasTriggeredInitialIDFailure, Is.False);
            Assert.That(playerInventory.TemporaryIDCount, Is.EqualTo(0));
            Assert.That(idScanner.PermanentFailChance, Is.EqualTo(0.10f).Within(0.0001f));
        }

        [Test]
        public void FirstPermanentScan_AlwaysFails_TriggersInitialFailureAndIDProblem()
        {
            string message = idScanner.Interact();

            Assert.That(message, Is.EqualTo("You don't have an id card, go see the receptionist"));
            Assert.That(playerInventory.HasTriggeredInitialIDFailure, Is.True);
            Assert.That(playerInventory.HasIDProblem, Is.True);
        }

        [Test]
        public void SubsequentPermanentScans_WhileIDProblemActive_ContinueToFail()
        {
            // First scan triggers failure
            idScanner.Interact();
            Assert.That(playerInventory.HasIDProblem, Is.True);

            // Second permanent scan while problem is active
            string message = idScanner.Interact();

            Assert.That(message, Is.EqualTo("You don't have an id card, go see the receptionist"));
            Assert.That(playerInventory.HasIDProblem, Is.True);
            Assert.That(playerInventory.HasTriggeredInitialIDFailure, Is.True);
        }

        [Test]
        public void TemporaryIDScan_AlwaysSucceeds_ConsumesCard_ClearsIDProblem()
        {
            // 1. Initial permanent scan fails
            idScanner.Interact();
            Assert.That(playerInventory.HasIDProblem, Is.True);

            // 2. Simulate Reception issuing a temporary ID directly to player inventory
            playerInventory.AddTemporaryID();
            Assert.That(playerInventory.TemporaryIDCount, Is.EqualTo(1));
            Assert.That(playerInventory.CurrentIDCard, Is.EqualTo(IDCardType.Temporary));
            Assert.That(playerInventory.HasIDProblem, Is.True); // Remains true until scanned

            // 3. Scan temporary ID
            string message = idScanner.Interact();

            Assert.That(message, Is.EqualTo("Temporary access granted."));
            Assert.That(playerInventory.TemporaryIDCount, Is.EqualTo(0));
            Assert.That(playerInventory.HasIDProblem, Is.False); // Problem cleared!
            Assert.That(playerInventory.HasTriggeredInitialIDFailure, Is.True);
            Assert.That(playerInventory.CurrentIDCard, Is.EqualTo(IDCardType.Permanent)); // Auto-reselects permanent
        }

        [Test]
        public void PostTutorial_PermanentScan_DeterministicSuccess_WhenFailChanceZero()
        {
            // Complete tutorial
            idScanner.Interact(); // Fails
            playerInventory.AddTemporaryID();
            idScanner.Interact(); // Temp scan clears problem
            Assert.That(playerInventory.HasIDProblem, Is.False);

            // Set fail chance to 0 for deterministic success testing
            idScanner.PermanentFailChance = 0f;

            string message = idScanner.Interact();

            Assert.That(message, Is.EqualTo("Access granted. Welcome!"));
            Assert.That(playerInventory.HasIDProblem, Is.False);
        }

        [Test]
        public void PostTutorial_PermanentScan_TriggersRecurringProblem_WhenFailChanceOne()
        {
            // Complete tutorial
            idScanner.Interact(); // Fails
            playerInventory.AddTemporaryID();
            idScanner.Interact(); // Temp scan clears problem
            Assert.That(playerInventory.HasIDProblem, Is.False);

            // Set fail chance to 1 (100%) for deterministic recurring failure testing
            idScanner.PermanentFailChance = 1f;

            string message = idScanner.Interact();

            Assert.That(message, Is.EqualTo("You don't have an id card, go see the receptionist"));
            Assert.That(playerInventory.HasIDProblem, Is.True);

            // Subsequent permanent scan continues failing
            string nextMessage = idScanner.Interact();
            Assert.That(nextMessage, Is.EqualTo("You don't have an id card, go see the receptionist"));
            Assert.That(playerInventory.HasIDProblem, Is.True);
        }

        [Test]
        public void PostTutorial_RecurringProblem_RecoversViaNewTemporaryID()
        {
            // Complete tutorial
            idScanner.Interact();
            playerInventory.AddTemporaryID();
            idScanner.Interact();

            // Trigger 100% recurring failure
            idScanner.PermanentFailChance = 1f;
            idScanner.Interact();
            Assert.That(playerInventory.HasIDProblem, Is.True);

            // Player visits Reception again -> Reception issues second temporary ID
            playerInventory.AddTemporaryID();
            Assert.That(playerInventory.TemporaryIDCount, Is.EqualTo(1));
            Assert.That(playerInventory.CurrentIDCard, Is.EqualTo(IDCardType.Temporary));

            // Scan second temporary ID
            string tempMessage = idScanner.Interact();
            Assert.That(tempMessage, Is.EqualTo("Temporary access granted."));
            Assert.That(playerInventory.TemporaryIDCount, Is.EqualTo(0));
            Assert.That(playerInventory.HasIDProblem, Is.False);

            // Reset fail chance to 0 -> permanent ID scan works again
            idScanner.PermanentFailChance = 0f;
            string permMessage = idScanner.Interact();
            Assert.That(permMessage, Is.EqualTo("Access granted. Welcome!"));
        }

#if UNITY_EDITOR
        [MenuItem("UIU Simulator/Tests/Run ID Card State Machine Tests")]
        public static void RunFromMenu()
        {
            var tests = new IDCardStateMachineTests();
            try
            {
                tests.SetUp();
                tests.InitialState_HasPermanentID_NoProblem_InitialFailureNotTriggered();
                tests.TearDown();

                tests.SetUp();
                tests.FirstPermanentScan_AlwaysFails_TriggersInitialFailureAndIDProblem();
                tests.TearDown();

                tests.SetUp();
                tests.SubsequentPermanentScans_WhileIDProblemActive_ContinueToFail();
                tests.TearDown();

                tests.SetUp();
                tests.TemporaryIDScan_AlwaysSucceeds_ConsumesCard_ClearsIDProblem();
                tests.TearDown();

                tests.SetUp();
                tests.PostTutorial_PermanentScan_DeterministicSuccess_WhenFailChanceZero();
                tests.TearDown();

                tests.SetUp();
                tests.PostTutorial_PermanentScan_TriggersRecurringProblem_WhenFailChanceOne();
                tests.TearDown();

                tests.SetUp();
                tests.PostTutorial_RecurringProblem_RecoversViaNewTemporaryID();
                tests.TearDown();

                Debug.Log("<color=green><b>[IDCardStateMachineTests] All 7 tests PASSED!</b></color>");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"<color=red><b>[IDCardStateMachineTests] Test FAILED:</b> {ex.Message}</color>\n{ex.StackTrace}");
            }
            finally
            {
                tests.TearDown();
            }
        }
#endif
    }
}
