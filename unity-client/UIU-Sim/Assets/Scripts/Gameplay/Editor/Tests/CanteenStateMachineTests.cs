#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace UIU.Simulator.Gameplay.Tests
{
    /// <summary>
    /// Unit tests verifying the Canteen state machine rules, daily campus state,
    /// and defensive queue teardown across CampusDayState and CanteenBreakfastCounter:
    /// 1. Initial daily state: HasCompletedBreakfastEvent is false.
    /// 2. Rice route: instant completion, +5 Aura bonus, HasCompletedBreakfastEvent = true.
    /// 3. Farming prevention: subsequent counter interaction blocked with message.
    /// 4. Porotta wait to completion: 100% timer, 0 Aura delta, movement/look restored.
    /// 5. Porotta skip breakfast: -5 Aura penalty, movement/look restored, event completed.
    /// 6. Porotta skip the line: -10 Aura penalty, movement/look restored, event completed.
    /// 7. Defensive cleanup: OnDisable/OnDestroy restores movement/look, does NOT complete event, 0 Aura change.
    /// 8. BeginCampusDay reset: resets daily state and allows breakfast again.
    /// </summary>
    [TestFixture]
    public sealed class CanteenStateMachineTests
    {
        private GameObject playerObject;
        private CampusDayState campusDayState;
        private PlayerStats playerStats;
        private PlayerMovement playerMovement;
        private FirstPersonLook firstPersonLook;
        private DialogueUI dialogueUI;
        private CanteenQueueUI canteenQueueUI;

        private GameObject counterObject;
        private CanteenBreakfastCounter breakfastCounter;

        [SetUp]
        public void SetUp()
        {
            // Create player object with required components
            playerObject = new GameObject("TestPlayer");
            playerObject.AddComponent<CharacterController>();
            playerMovement = playerObject.AddComponent<PlayerMovement>();
            firstPersonLook = playerObject.AddComponent<FirstPersonLook>();
            playerStats = playerObject.AddComponent<PlayerStats>();
            campusDayState = playerObject.AddComponent<CampusDayState>();
            dialogueUI = playerObject.AddComponent<DialogueUI>();
            canteenQueueUI = playerObject.AddComponent<CanteenQueueUI>();

            // Create counter object
            counterObject = new GameObject("TestBreakfastCounter");
            counterObject.AddComponent<BoxCollider>();
            counterObject.AddComponent<AudioSource>();
            counterObject.AddComponent<InteractionFeedback>();
            breakfastCounter = counterObject.AddComponent<CanteenBreakfastCounter>();
            breakfastCounter.QueueDuration = 5f; // Fast test duration
        }

        [TearDown]
        public void TearDown()
        {
            if (breakfastCounter != null && breakfastCounter.IsQueueActive)
            {
                breakfastCounter.TeardownQueue(isDefensive: true);
            }

            if (counterObject != null)
            {
                Object.DestroyImmediate(counterObject);
            }

            if (playerObject != null)
            {
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void InitialState_BreakfastNotCompleted_CounterInNotStartedState()
        {
            Assert.That(campusDayState.HasCompletedBreakfastEvent, Is.False);
            Assert.That(breakfastCounter.CurrentState, Is.EqualTo(CanteenBreakfastState.NotStarted));
            Assert.That(breakfastCounter.IsQueueActive, Is.False);
        }

        [Test]
        public void RiceRoute_CompletesBreakfastImmediately_Awards5Aura()
        {
            float initialAura = playerStats.Aura;

            // Trigger Rice choice via private handler
            InvokeMethod(breakfastCounter, "OnSelectRice");

            Assert.That(campusDayState.HasCompletedBreakfastEvent, Is.True);
            Assert.That(breakfastCounter.CurrentState, Is.EqualTo(CanteenBreakfastState.Completed));
            Assert.That(playerStats.Aura, Is.EqualTo(initialAura + 5f).Within(0.001f));
            Assert.That(breakfastCounter.IsQueueActive, Is.False);
        }

        [Test]
        public void RiceRoute_SubsequentInteractionBlocked_ReturnsAlreadySortedMessage()
        {
            // Complete breakfast via Rice
            InvokeMethod(breakfastCounter, "OnSelectRice");
            Assert.That(campusDayState.HasCompletedBreakfastEvent, Is.True);

            // Attempt subsequent interaction
            string message = breakfastCounter.Interact();

            Assert.That(message, Is.EqualTo("You've already sorted out breakfast today."));
        }

        [Test]
        public void PorottaRoute_WaitUntilCompletion_CompletesBreakfast_0AuraDelta()
        {
            float initialAura = playerStats.Aura;

            // Select Porotta to enter queue
            InvokeMethod(breakfastCounter, "OnSelectPorotta");

            Assert.That(breakfastCounter.IsQueueActive, Is.True);
            Assert.That(breakfastCounter.CurrentState, Is.EqualTo(CanteenBreakfastState.WaitingInQueue));
            Assert.That(playerMovement.enabled, Is.False, "Player movement should be locked in queue.");
            Assert.That(firstPersonLook.enabled, Is.False, "Camera look should be locked in queue.");
            Assert.That(CanteenQueueUI.IsOpen, Is.True, "Queue UI should be open.");

            // Simulate timer reaching 100% completion
            InvokeMethod(breakfastCounter, "OnQueueCompleted");

            Assert.That(breakfastCounter.IsQueueActive, Is.False);
            Assert.That(breakfastCounter.CurrentState, Is.EqualTo(CanteenBreakfastState.Completed));
            Assert.That(campusDayState.HasCompletedBreakfastEvent, Is.True);
            Assert.That(playerStats.Aura, Is.EqualTo(initialAura).Within(0.001f), "Aura should not change on full wait.");
            Assert.That(playerMovement.enabled, Is.True, "Player movement should be restored.");
            Assert.That(firstPersonLook.enabled, Is.True, "Camera look should be restored.");
            Assert.That(CanteenQueueUI.IsOpen, Is.False, "Queue UI should be hidden.");
        }

        [Test]
        public void PorottaRoute_SkipBreakfast_CancelsQueue_Deducts5Aura_CompletesEvent()
        {
            float initialAura = playerStats.Aura;

            // Select Porotta to enter queue
            InvokeMethod(breakfastCounter, "OnSelectPorotta");
            Assert.That(breakfastCounter.IsQueueActive, Is.True);

            // Choose Skip Breakfast
            InvokeMethod(breakfastCounter, "OnSkipBreakfast");

            Assert.That(breakfastCounter.IsQueueActive, Is.False);
            Assert.That(breakfastCounter.CurrentState, Is.EqualTo(CanteenBreakfastState.Completed));
            Assert.That(campusDayState.HasCompletedBreakfastEvent, Is.True);
            Assert.That(playerStats.Aura, Is.EqualTo(initialAura - 5f).Within(0.001f), "Aura should decrease by 5 on Skip Breakfast.");
            Assert.That(playerMovement.enabled, Is.True, "Player movement should be restored.");
            Assert.That(firstPersonLook.enabled, Is.True, "Camera look should be restored.");
            Assert.That(CanteenQueueUI.IsOpen, Is.False, "Queue UI should be hidden.");
        }

        [Test]
        public void PorottaRoute_SkipLine_CancelsQueue_Deducts10Aura_CompletesEvent()
        {
            float initialAura = playerStats.Aura;

            // Select Porotta to enter queue
            InvokeMethod(breakfastCounter, "OnSelectPorotta");
            Assert.That(breakfastCounter.IsQueueActive, Is.True);

            // Choose Skip the Line
            InvokeMethod(breakfastCounter, "OnSkipLine");

            Assert.That(breakfastCounter.IsQueueActive, Is.False);
            Assert.That(breakfastCounter.CurrentState, Is.EqualTo(CanteenBreakfastState.Completed));
            Assert.That(campusDayState.HasCompletedBreakfastEvent, Is.True);
            Assert.That(playerStats.Aura, Is.EqualTo(initialAura - 10f).Within(0.001f), "Aura should decrease by 10 on Skip the Line.");
            Assert.That(playerMovement.enabled, Is.True, "Player movement should be restored.");
            Assert.That(firstPersonLook.enabled, Is.True, "Camera look should be restored.");
            Assert.That(CanteenQueueUI.IsOpen, Is.False, "Queue UI should be hidden.");
        }

        [Test]
        public void DefensiveTeardown_RestoresControls_DoesNotCompleteEvent_NoAuraDelta()
        {
            float initialAura = playerStats.Aura;

            // Select Porotta to enter queue
            InvokeMethod(breakfastCounter, "OnSelectPorotta");
            Assert.That(breakfastCounter.IsQueueActive, Is.True);

            // Execute defensive teardown (simulating disable/destroy/scene change mid-queue)
            breakfastCounter.TeardownQueue(isDefensive: true);

            Assert.That(breakfastCounter.IsQueueActive, Is.False);
            Assert.That(campusDayState.HasCompletedBreakfastEvent, Is.False, "Defensive teardown must NOT complete breakfast event.");
            Assert.That(playerStats.Aura, Is.EqualTo(initialAura).Within(0.001f), "Defensive teardown must NOT change Aura.");
            Assert.That(playerMovement.enabled, Is.True, "Movement must be restored.");
            Assert.That(firstPersonLook.enabled, Is.True, "Look must be restored.");
            Assert.That(CanteenQueueUI.IsOpen, Is.False, "Queue UI must be hidden.");
        }

        [Test]
        public void BeginCampusDay_ResetsBreakfastEvent_AllowsInteractionAgain()
        {
            // Complete breakfast via Rice
            InvokeMethod(breakfastCounter, "OnSelectRice");
            Assert.That(campusDayState.HasCompletedBreakfastEvent, Is.True);

            // Reset day
            campusDayState.BeginCampusDay();
            Assert.That(campusDayState.HasCompletedBreakfastEvent, Is.False);

            // Interacting now proceeds to Ordering instead of blocked response
            string response = breakfastCounter.Interact();
            Assert.That(response, Is.Null, "Interact should show DialogueUI and return null response.");
            Assert.That(breakfastCounter.CurrentState, Is.EqualTo(CanteenBreakfastState.Ordering));
        }

        [Test]
        public void QueueUI_InputBleedProtection_DisarmedOnOpeningFrame_ArmedAfterwards()
        {
            bool skipBreakfastCalled = false;
            bool skipLineCalled = false;

            canteenQueueUI.Show(() => skipBreakfastCalled = true, () => skipLineCalled = true);

            Assert.That(CanteenQueueUI.IsOpen, Is.True);

            Button skipBreakfastBtn = GetField<Button>(canteenQueueUI, "skipBreakfastButton");
            Button skipLineBtn = GetField<Button>(canteenQueueUI, "skipLineButton");
            bool isArmed = GetField<bool>(canteenQueueUI, "isArmed");

            Assert.That(isArmed, Is.False, "CanteenQueueUI must be disarmed on the frame it opens.");
            Assert.That(skipBreakfastBtn.interactable, Is.False, "Skip Breakfast button must be non-interactable on opening frame.");
            Assert.That(skipLineBtn.interactable, Is.False, "Skip Line button must be non-interactable on opening frame.");

            // Attempt to trigger on opening frame (e.g. from key press or mouse click bleed)
            InvokeMethod(canteenQueueUI, "TriggerSkipBreakfast");
            InvokeMethod(canteenQueueUI, "TriggerSkipLine");

            Assert.That(skipBreakfastCalled, Is.False, "Skip Breakfast must NOT execute on opening frame.");
            Assert.That(skipLineCalled, Is.False, "Skip Line must NOT execute on opening frame.");

            // Simulate next frame input arming
            InvokeMethod(canteenQueueUI, "ArmInput");

            Assert.That(GetField<bool>(canteenQueueUI, "isArmed"), Is.True, "CanteenQueueUI should be armed after ArmInput.");
            Assert.That(skipBreakfastBtn.interactable, Is.True, "Skip Breakfast button must be interactable after arming.");
            Assert.That(skipLineBtn.interactable, Is.True, "Skip Line button must be interactable after arming.");

            // Trigger after arming
            InvokeMethod(canteenQueueUI, "TriggerSkipBreakfast");
            Assert.That(skipBreakfastCalled, Is.True, "Skip Breakfast must execute after arming.");

            canteenQueueUI.Hide();
            Assert.That(CanteenQueueUI.IsOpen, Is.False);
            Assert.That(GetField<bool>(canteenQueueUI, "isArmed"), Is.False);
        }

        [Test]
        public void QueueUI_ConfigurableText_AppliedOnShow()
        {
            SetField(canteenQueueUI, "queueTitle", "Custom Queue Header");
            SetField(canteenQueueUI, "waitingHelpText", "Custom Waiting Notice");
            SetField(canteenQueueUI, "skipBreakfastButtonLabel", "[1] Custom Skip");
            SetField(canteenQueueUI, "skipLineButtonLabel", "[2] Custom Rush");

            canteenQueueUI.Show(null, null);

            var headerLabel = GetField<TMPro.TextMeshProUGUI>(canteenQueueUI, "headerLabel");
            var statusLabel = GetField<TMPro.TextMeshProUGUI>(canteenQueueUI, "statusLabel");
            var skipBreakfastText = GetField<TMPro.TextMeshProUGUI>(canteenQueueUI, "skipBreakfastLabelText");
            var skipLineText = GetField<TMPro.TextMeshProUGUI>(canteenQueueUI, "skipLineLabelText");

            Assert.That(headerLabel.text, Is.EqualTo("Custom Queue Header"));
            Assert.That(statusLabel.text, Is.EqualTo("Custom Waiting Notice"));
            Assert.That(skipBreakfastText.text, Is.EqualTo("[1] Custom Skip"));
            Assert.That(skipLineText.text, Is.EqualTo("[2] Custom Rush"));

            canteenQueueUI.Hide();
        }

        [Test]
        public void BreakfastCounter_ConfigurableDialogueAndMessages()
        {
            SetField(breakfastCounter, "prompt", "Custom Breakfast Prompt");
            SetField(breakfastCounter, "workerName", "Custom Chef");
            SetField(breakfastCounter, "breakfastPrompt", "What would you like, student?");
            SetField(breakfastCounter, "porottaChoiceLabel", "Special Porotta");
            SetField(breakfastCounter, "riceChoiceLabel", "Special Khichuri");
            SetField(breakfastCounter, "alreadyCompletedMessage", "You already ate today!");

            Assert.That(breakfastCounter.InteractionPrompt, Is.EqualTo("Custom Breakfast Prompt"));

            // Complete breakfast once
            InvokeMethod(breakfastCounter, "OnSelectRice");
            Assert.That(campusDayState.HasCompletedBreakfastEvent, Is.True);

            // Attempt interaction — should return custom already completed message
            string blockedMessage = breakfastCounter.Interact();
            Assert.That(blockedMessage, Is.EqualTo("You already ate today!"));
        }

        [Test]
        public void SideStore_ConfigurableDialogueAndMenuItems()
        {
            GameObject storeGo = new GameObject("TestSideStore");
            storeGo.AddComponent<BoxCollider>();
            CanteenSideStore store = storeGo.AddComponent<CanteenSideStore>();

            SetField(store, "storeName", "East Snack Shack");
            SetField(store, "prompt", "Grab Snacks");
            SetField(store, "greeting", "Hungry? Grab something quick!");
            SetField(store, "shawarmaLabel", "Chicken Shawarma");
            SetField(store, "orderCompleteResponse", "Order up! Enjoy.");

            Assert.That(store.InteractionPrompt, Is.EqualTo("Grab Snacks"));

            string result = store.Interact();
            Assert.That(result, Is.Null, "Store interact opens dialogue and returns null.");
            Assert.That(DialogueUI.IsOpen, Is.True, "Dialogue should be open.");

            DialogueUI.Instance.Hide();
            Object.DestroyImmediate(storeGo);
        }

        private static void InvokeMethod(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, $"Method '{methodName}' not found on {target.GetType().Name}.");
            method.Invoke(target, null);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, $"Field '{fieldName}' not found on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static T GetField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, $"Field '{fieldName}' not found on {target.GetType().Name}.");
            return (T)field.GetValue(target);
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("UIU Simulator/Tests/Run Canteen State Machine Tests")]
        public static void RunFromMenu()
        {
            var tests = new CanteenStateMachineTests();
            try
            {
                tests.SetUp();
                tests.InitialState_BreakfastNotCompleted_CounterInNotStartedState();
                tests.TearDown();

                tests.SetUp();
                tests.RiceRoute_CompletesBreakfastImmediately_Awards5Aura();
                tests.TearDown();

                tests.SetUp();
                tests.RiceRoute_SubsequentInteractionBlocked_ReturnsAlreadySortedMessage();
                tests.TearDown();

                tests.SetUp();
                tests.PorottaRoute_WaitUntilCompletion_CompletesBreakfast_0AuraDelta();
                tests.TearDown();

                tests.SetUp();
                tests.PorottaRoute_SkipBreakfast_CancelsQueue_Deducts5Aura_CompletesEvent();
                tests.TearDown();

                tests.SetUp();
                tests.PorottaRoute_SkipLine_CancelsQueue_Deducts10Aura_CompletesEvent();
                tests.TearDown();

                tests.SetUp();
                tests.DefensiveTeardown_RestoresControls_DoesNotCompleteEvent_NoAuraDelta();
                tests.TearDown();

                tests.SetUp();
                tests.BeginCampusDay_ResetsBreakfastEvent_AllowsInteractionAgain();
                tests.TearDown();

                tests.SetUp();
                tests.QueueUI_InputBleedProtection_DisarmedOnOpeningFrame_ArmedAfterwards();
                tests.TearDown();

                tests.SetUp();
                tests.QueueUI_ConfigurableText_AppliedOnShow();
                tests.TearDown();

                tests.SetUp();
                tests.BreakfastCounter_ConfigurableDialogueAndMessages();
                tests.TearDown();

                tests.SetUp();
                tests.SideStore_ConfigurableDialogueAndMenuItems();
                tests.TearDown();

                Debug.Log("<color=green><b>[CanteenStateMachineTests] All 12 tests PASSED!</b></color>");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"<color=red><b>[CanteenStateMachineTests] Test FAILED:</b> {ex.Message}</color>\n{ex.StackTrace}");
            }
            finally
            {
                tests.TearDown();
            }
        }
#endif
    }
}
