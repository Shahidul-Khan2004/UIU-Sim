#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Reflection;
using NUnit.Framework;
using UIU.Simulator.Gameplay.Activities;
using UIU.Simulator.Gameplay.Player;
using UIU.Simulator.Gameplay.UI;
using UnityEngine;
using UnityEngine.UI;

namespace UIU.Simulator.Gameplay.Tests
{
    /// <summary>
    /// Unit tests for Neptune breakfast resolution via the activity foundation:
    /// Rice / Porotta wait / Skip line / Skip breakfast outcomes, side-store isolation,
    /// defensive queue teardown, duplicate-resolve safety, and network-failure safety.
    /// </summary>
    [TestFixture]
    public sealed class CanteenStateMachineTests
    {
        private GameObject playerObject;
        private CampusDayState campusDayState;
        private DailyActivityState dailyActivityState;
        private PlayerStats playerStats;
        private PlayerMovement playerMovement;
        private FirstPersonLook firstPersonLook;
        private DialogueUI dialogueUI;
        private CanteenQueueUI canteenQueueUI;

        private GameObject counterObject;
        private CanteenBreakfastCounter breakfastCounter;
        private TestActivityProgressSync fakeProgressSync;

        private sealed class TestActivityProgressSync : IActivityProgressSync, IPlayerProgressSync
        {
            private readonly PlayerStats stats;
            private readonly CampusDayState dayState;
            private readonly DailyActivityState activityState;

            public bool IsHydrated => true;
            public bool IsMutationInFlight => false;
            public bool FailNextResolve { get; set; }
            public int ResolveCount { get; private set; }
            public string LastOutcome { get; private set; }

            public TestActivityProgressSync(PlayerStats stats, CampusDayState dayState, DailyActivityState activityState)
            {
                this.stats = stats;
                this.dayState = dayState;
                this.activityState = activityState;
            }

            public void RequestStatDelta(int auraDelta, int academicReputationDelta)
            {
                // Breakfast must not use this path.
                if (stats != null)
                {
                    stats.ApplyServerState(
                        stats.Aura + auraDelta,
                        stats.AcademicReputation + academicReputationDelta,
                        StatUpdateSource.GameplayMutation);
                }
            }

            public void RequestActivityResolve(
                string activityId,
                string outcome,
                Action<ActivityResolveResult> onSuccess,
                Action onFailure)
            {
                ResolveCount++;
                LastOutcome = outcome;

                if (FailNextResolve)
                {
                    onFailure?.Invoke();
                    return;
                }

                int auraDelta = 0;
                ActivityStatus status = ActivityStatus.Completed;
                switch ((outcome ?? string.Empty).ToUpperInvariant())
                {
                    case "RICE":
                        auraDelta = 5;
                        break;
                    case "POROTTA_WAIT":
                        auraDelta = 0;
                        break;
                    case "SKIP_LINE":
                        auraDelta = -10;
                        break;
                    case "SKIP_BREAKFAST":
                        auraDelta = -5;
                        status = ActivityStatus.Missed;
                        break;
                }

                float newAura = (stats != null ? stats.Aura : 50f) + auraDelta;
                float reputation = stats != null ? stats.AcademicReputation : 50f;
                ActivityRecord record = new ActivityRecord(
                    activityId,
                    status,
                    outcome,
                    auraDelta,
                    0,
                    1);

                activityState?.ApplyServerActivity(record);
                dayState?.CompleteBreakfastEvent();
                stats?.ApplyServerState(newAura, reputation, StatUpdateSource.GameplayMutation);

                onSuccess?.Invoke(new ActivityResolveResult(record, false, newAura, reputation));
            }
        }

        [SetUp]
        public void SetUp()
        {
            playerObject = new GameObject("TestPlayer");
            playerObject.AddComponent<CharacterController>();
            playerMovement = playerObject.AddComponent<PlayerMovement>();
            firstPersonLook = playerObject.AddComponent<FirstPersonLook>();
            playerStats = playerObject.AddComponent<PlayerStats>();
            InvokeMethod(playerStats, "Awake");
            dailyActivityState = playerObject.AddComponent<DailyActivityState>();
            campusDayState = playerObject.AddComponent<CampusDayState>();
            InvokeMethod(campusDayState, "Awake");
            dialogueUI = playerObject.AddComponent<DialogueUI>();
            canteenQueueUI = playerObject.AddComponent<CanteenQueueUI>();
            InvokeMethod(dialogueUI, "Awake");
            InvokeMethod(canteenQueueUI, "Awake");

            counterObject = new GameObject("TestBreakfastCounter");
            counterObject.AddComponent<BoxCollider>();
            counterObject.AddComponent<AudioSource>();
            counterObject.AddComponent<InteractionFeedback>();
            breakfastCounter = counterObject.AddComponent<CanteenBreakfastCounter>();
            InvokeMethod(breakfastCounter, "Awake");
            breakfastCounter.QueueDuration = 5f;

            fakeProgressSync = new TestActivityProgressSync(playerStats, campusDayState, dailyActivityState);
            breakfastCounter.SetActivitySyncForTesting(fakeProgressSync);
        }

        [TearDown]
        public void TearDown()
        {
            if (breakfastCounter != null && breakfastCounter.IsQueueActive)
            {
                breakfastCounter.TeardownQueue(isDefensive: true);
            }

            if (DialogueUI.Instance != null && DialogueUI.IsOpen)
            {
                DialogueUI.Instance.Hide();
            }

            if (CanteenQueueUI.Instance != null && CanteenQueueUI.IsOpen)
            {
                CanteenQueueUI.Instance.Hide();
            }

            var notif = UnityEngine.Object.FindFirstObjectByType<SystemNotificationUI>();
            if (notif != null)
            {
                UnityEngine.Object.DestroyImmediate(notif.gameObject);
            }

            if (counterObject != null)
            {
                UnityEngine.Object.DestroyImmediate(counterObject);
            }

            if (playerObject != null)
            {
                UnityEngine.Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void InitialState_BreakfastNotCompleted_CounterInNotStartedState()
        {
            Assert.That(campusDayState.HasCompletedBreakfastEvent, Is.False);
            Assert.That(dailyActivityState.BreakfastStatus, Is.EqualTo(ActivityStatus.Pending));
            Assert.That(breakfastCounter.CurrentState, Is.EqualTo(CanteenBreakfastState.NotStarted));
            Assert.That(breakfastCounter.IsQueueActive, Is.False);
            Assert.That(breakfastCounter.StallDisplayName, Is.EqualTo("Neptune"));
        }

        [Test]
        public void RiceRoute_CompletesBreakfastImmediately_Awards5Aura()
        {
            float initialAura = playerStats.Aura;

            InvokeMethod(breakfastCounter, "OnSelectRice");

            Assert.That(campusDayState.HasCompletedBreakfastEvent, Is.True);
            Assert.That(dailyActivityState.BreakfastStatus, Is.EqualTo(ActivityStatus.Completed));
            Assert.That(breakfastCounter.CurrentState, Is.EqualTo(CanteenBreakfastState.Completed));
            Assert.That(playerStats.Aura, Is.EqualTo(initialAura + 5f).Within(0.001f));
            Assert.That(fakeProgressSync.LastOutcome, Is.EqualTo("RICE"));
            Assert.That(breakfastCounter.IsQueueActive, Is.False);
        }

        [Test]
        public void RiceRoute_SubsequentInteractionBlocked_ReturnsAlreadySortedMessage()
        {
            if (DialogueUI.IsOpen) DialogueUI.Instance.Hide();
            if (CanteenQueueUI.IsOpen) CanteenQueueUI.Instance.Hide();

            InvokeMethod(breakfastCounter, "OnSelectRice");
            Assert.That(campusDayState.HasCompletedBreakfastEvent, Is.True);

            string message = breakfastCounter.Interact();
            Assert.That(message, Is.EqualTo("You've already sorted out breakfast today."));
            Assert.That(fakeProgressSync.ResolveCount, Is.EqualTo(1));
        }

        [Test]
        public void PorottaRoute_WaitUntilCompletion_CompletesBreakfast_0AuraDelta()
        {
            float initialAura = playerStats.Aura;

            InvokeMethod(breakfastCounter, "OnSelectPorotta");

            Assert.That(breakfastCounter.IsQueueActive, Is.True);
            Assert.That(breakfastCounter.CurrentState, Is.EqualTo(CanteenBreakfastState.WaitingInQueue));
            Assert.That(campusDayState.HasCompletedBreakfastEvent, Is.False, "Queue start must not resolve breakfast.");
            Assert.That(playerMovement.enabled, Is.False);
            Assert.That(firstPersonLook.enabled, Is.False);
            Assert.That(CanteenQueueUI.IsOpen, Is.True);

            InvokeMethod(breakfastCounter, "OnQueueCompleted");

            Assert.That(breakfastCounter.IsQueueActive, Is.False);
            Assert.That(breakfastCounter.CurrentState, Is.EqualTo(CanteenBreakfastState.Completed));
            Assert.That(campusDayState.HasCompletedBreakfastEvent, Is.True);
            Assert.That(dailyActivityState.BreakfastStatus, Is.EqualTo(ActivityStatus.Completed));
            Assert.That(playerStats.Aura, Is.EqualTo(initialAura).Within(0.001f));
            Assert.That(fakeProgressSync.LastOutcome, Is.EqualTo("POROTTA_WAIT"));
            Assert.That(playerMovement.enabled, Is.True);
            Assert.That(firstPersonLook.enabled, Is.True);
            Assert.That(CanteenQueueUI.IsOpen, Is.False);
        }

        [Test]
        public void PorottaRoute_SkipBreakfast_MissesActivity_Deducts5Aura()
        {
            float initialAura = playerStats.Aura;

            InvokeMethod(breakfastCounter, "OnSelectPorotta");
            InvokeMethod(breakfastCounter, "OnSkipBreakfast");

            Assert.That(breakfastCounter.IsQueueActive, Is.False);
            Assert.That(breakfastCounter.CurrentState, Is.EqualTo(CanteenBreakfastState.Completed));
            Assert.That(campusDayState.HasCompletedBreakfastEvent, Is.True);
            Assert.That(dailyActivityState.BreakfastStatus, Is.EqualTo(ActivityStatus.Missed));
            Assert.That(playerStats.Aura, Is.EqualTo(initialAura - 5f).Within(0.001f));
            Assert.That(fakeProgressSync.LastOutcome, Is.EqualTo("SKIP_BREAKFAST"));
            Assert.That(playerMovement.enabled, Is.True);
            Assert.That(firstPersonLook.enabled, Is.True);
        }

        [Test]
        public void PorottaRoute_SkipLine_Completes_Deducts10Aura()
        {
            float initialAura = playerStats.Aura;

            InvokeMethod(breakfastCounter, "OnSelectPorotta");
            InvokeMethod(breakfastCounter, "OnSkipLine");

            Assert.That(dailyActivityState.BreakfastStatus, Is.EqualTo(ActivityStatus.Completed));
            Assert.That(playerStats.Aura, Is.EqualTo(initialAura - 10f).Within(0.001f));
            Assert.That(fakeProgressSync.LastOutcome, Is.EqualTo("SKIP_LINE"));
            Assert.That(playerMovement.enabled, Is.True);
        }

        [Test]
        public void DefensiveTeardown_RestoresControls_DoesNotCompleteEvent_NoAuraDelta()
        {
            float initialAura = playerStats.Aura;

            InvokeMethod(breakfastCounter, "OnSelectPorotta");
            breakfastCounter.TeardownQueue(isDefensive: true);

            Assert.That(breakfastCounter.IsQueueActive, Is.False);
            Assert.That(campusDayState.HasCompletedBreakfastEvent, Is.False);
            Assert.That(dailyActivityState.BreakfastStatus, Is.EqualTo(ActivityStatus.Pending));
            Assert.That(playerStats.Aura, Is.EqualTo(initialAura).Within(0.001f));
            Assert.That(playerMovement.enabled, Is.True);
            Assert.That(firstPersonLook.enabled, Is.True);
            Assert.That(CanteenQueueUI.IsOpen, Is.False);
        }

        [Test]
        public void BeginCampusDay_ResetsBreakfastEvent_AllowsInteractionAgain()
        {
            InvokeMethod(breakfastCounter, "OnSelectRice");
            Assert.That(campusDayState.HasCompletedBreakfastEvent, Is.True);

            campusDayState.BeginCampusDay();
            Assert.That(campusDayState.HasCompletedBreakfastEvent, Is.False);
            Assert.That(dailyActivityState.BreakfastStatus, Is.EqualTo(ActivityStatus.Pending));

            string response = breakfastCounter.Interact();
            Assert.That(response, Is.Null);
            Assert.That(breakfastCounter.CurrentState, Is.EqualTo(CanteenBreakfastState.Ordering));
        }

        [Test]
        public void VisitingNeptuneWithoutChoice_DoesNotResolveBreakfast()
        {
            string response = breakfastCounter.Interact();
            Assert.That(response, Is.Null);
            Assert.That(breakfastCounter.CurrentState, Is.EqualTo(CanteenBreakfastState.Ordering));
            Assert.That(campusDayState.HasCompletedBreakfastEvent, Is.False);
            Assert.That(dailyActivityState.BreakfastStatus, Is.EqualTo(ActivityStatus.Pending));
            Assert.That(fakeProgressSync.ResolveCount, Is.EqualTo(0));
            DialogueUI.Instance.Hide();
        }

        [Test]
        public void NetworkFailure_DoesNotAwardAuraOrResolveActivity()
        {
            float initialAura = playerStats.Aura;
            fakeProgressSync.FailNextResolve = true;

            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, "[CanteenBreakfastCounter] Breakfast resolve failed for Rice. Local state unchanged.");

            InvokeMethod(breakfastCounter, "OnSelectRice");

            Assert.That(campusDayState.HasCompletedBreakfastEvent, Is.False);
            Assert.That(dailyActivityState.BreakfastStatus, Is.EqualTo(ActivityStatus.Pending));
            Assert.That(playerStats.Aura, Is.EqualTo(initialAura).Within(0.001f));
            Assert.That(breakfastCounter.CurrentState, Is.EqualTo(CanteenBreakfastState.NotStarted));
        }

        [Test]
        public void DuplicateResolve_DoesNotReapplyAura()
        {
            float initialAura = playerStats.Aura;
            InvokeMethod(breakfastCounter, "OnSelectRice");
            Assert.That(playerStats.Aura, Is.EqualTo(initialAura + 5f).Within(0.001f));

            InvokeMethod(breakfastCounter, "OnSelectRice");
            Assert.That(fakeProgressSync.ResolveCount, Is.EqualTo(1), "Second rice choice must be gated by resolved breakfast.");
            Assert.That(playerStats.Aura, Is.EqualTo(initialAura + 5f).Within(0.001f));
        }

        [Test]
        public void SideStore_DoesNotResolveBreakfast()
        {
            GameObject storeGo = new GameObject("TestSideStore");
            storeGo.AddComponent<BoxCollider>();
            CanteenSideStore store = storeGo.AddComponent<CanteenSideStore>();

            string result = store.Interact();
            Assert.That(result, Is.Null);
            Assert.That(DialogueUI.IsOpen, Is.True);
            Assert.That(campusDayState.HasCompletedBreakfastEvent, Is.False);
            Assert.That(dailyActivityState.BreakfastStatus, Is.EqualTo(ActivityStatus.Pending));
            Assert.That(fakeProgressSync.ResolveCount, Is.EqualTo(0));

            DialogueUI.Instance.Hide();
            UnityEngine.Object.DestroyImmediate(storeGo);
        }

        [Test]
        public void QueueUI_InputBleedProtection_DisarmedOnOpeningFrame_ArmedAfterwards()
        {
            bool skipBreakfastCalled = false;
            bool skipLineCalled = false;

            canteenQueueUI.Show(() => skipBreakfastCalled = true, () => skipLineCalled = true);

            Button skipBreakfastBtn = GetField<Button>(canteenQueueUI, "skipBreakfastButton");
            Button skipLineBtn = GetField<Button>(canteenQueueUI, "skipLineButton");
            Assert.That(GetField<bool>(canteenQueueUI, "isArmed"), Is.False);

            InvokeMethod(canteenQueueUI, "TriggerSkipBreakfast");
            InvokeMethod(canteenQueueUI, "TriggerSkipLine");
            Assert.That(skipBreakfastCalled, Is.False);
            Assert.That(skipLineCalled, Is.False);

            SetField(canteenQueueUI, "openedFrame", Time.frameCount - 1);
            InvokeMethod(canteenQueueUI, "ArmInput");
            InvokeMethod(canteenQueueUI, "TriggerSkipBreakfast");
            Assert.That(skipBreakfastCalled, Is.True);

            canteenQueueUI.Hide();
        }

        [Test]
        public void MissingActivitySync_DoesNotMutateStatsOrResolve()
        {
            float initialAura = playerStats.Aura;
            breakfastCounter.SetActivitySyncForTesting(null);

            UnityEngine.TestTools.LogAssert.Expect(
                LogType.Error,
                "[CanteenBreakfastCounter] Activity sync missing. Cannot resolve breakfast.");

            InvokeMethod(breakfastCounter, "OnSelectRice");

            Assert.That(campusDayState.HasCompletedBreakfastEvent, Is.False);
            Assert.That(playerStats.Aura, Is.EqualTo(initialAura).Within(0.001f));
            Assert.That(breakfastCounter.CurrentState, Is.EqualTo(CanteenBreakfastState.NotStarted));
        }

        [Test]
        public void ContinueHydration_RestoresCompletedBreakfast()
        {
            dailyActivityState.ApplyServerActivity(new ActivityRecord(
                ActivityIds.Breakfast,
                ActivityStatus.Completed,
                "RICE",
                5,
                0,
                1));
            campusDayState.ApplyHydratedBreakfastResolved();

            Assert.That(campusDayState.HasCompletedBreakfastEvent, Is.True);
            Assert.That(dailyActivityState.BreakfastStatus, Is.EqualTo(ActivityStatus.Completed));

            string blocked = breakfastCounter.Interact();
            Assert.That(blocked, Is.EqualTo("You've already sorted out breakfast today."));
        }

        [Test]
        public void NewGameReset_ClearsBreakfastToPending()
        {
            InvokeMethod(breakfastCounter, "OnSelectRice");
            Assert.That(dailyActivityState.IsBreakfastResolved, Is.True);

            campusDayState.BeginCampusDay();
            Assert.That(dailyActivityState.BreakfastStatus, Is.EqualTo(ActivityStatus.Pending));
            Assert.That(campusDayState.HasCompletedBreakfastEvent, Is.False);
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
                void Run(Action test)
                {
                    tests.SetUp();
                    test();
                    tests.TearDown();
                }

                Run(tests.InitialState_BreakfastNotCompleted_CounterInNotStartedState);
                Run(tests.RiceRoute_CompletesBreakfastImmediately_Awards5Aura);
                Run(tests.RiceRoute_SubsequentInteractionBlocked_ReturnsAlreadySortedMessage);
                Run(tests.PorottaRoute_WaitUntilCompletion_CompletesBreakfast_0AuraDelta);
                Run(tests.PorottaRoute_SkipBreakfast_MissesActivity_Deducts5Aura);
                Run(tests.PorottaRoute_SkipLine_Completes_Deducts10Aura);
                Run(tests.DefensiveTeardown_RestoresControls_DoesNotCompleteEvent_NoAuraDelta);
                Run(tests.BeginCampusDay_ResetsBreakfastEvent_AllowsInteractionAgain);
                Run(tests.VisitingNeptuneWithoutChoice_DoesNotResolveBreakfast);
                Run(tests.NetworkFailure_DoesNotAwardAuraOrResolveActivity);
                Run(tests.DuplicateResolve_DoesNotReapplyAura);
                Run(tests.SideStore_DoesNotResolveBreakfast);
                Run(tests.QueueUI_InputBleedProtection_DisarmedOnOpeningFrame_ArmedAfterwards);
                Run(tests.MissingActivitySync_DoesNotMutateStatsOrResolve);
                Run(tests.ContinueHydration_RestoresCompletedBreakfast);
                Run(tests.NewGameReset_ClearsBreakfastToPending);

                Debug.Log("<color=green><b>[CanteenStateMachineTests] All breakfast activity tests PASSED!</b></color>");
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
