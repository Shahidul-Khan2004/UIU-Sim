using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UIU.Simulator.Gameplay.Activities;
using UIU.Simulator.Gameplay.NPC;
using UIU.Simulator.Gameplay.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace UIU.Simulator.Gameplay.Tests
{
    [TestFixture]
    public sealed class CampusWalkingNPCTests
    {
        private const float Step = 0.1f;
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in created)
            {
                if (obj != null)
                {
                    Object.DestroyImmediate(obj);
                }
            }

            created.Clear();
        }

        [Test]
        public void MovesTowardTarget_SmoothlyAtConfiguredSpeed()
        {
            CampusWalkingNPC npc = CreateNpc(Vector3.zero, Point(0, 0, 0), Point(10, 0, 0), speed: 2f);

            npc.Tick(0.5f);

            Assert.That(npc.transform.position.x, Is.EqualTo(1f).Within(0.001f), "speed * deltaTime, no teleport.");
            Assert.That(npc.transform.position.z, Is.EqualTo(0f).Within(0.001f));
            Assert.That(npc.CurrentTargetIndex, Is.EqualTo(1));
            Assert.That(npc.IsWaiting, Is.False);
        }

        [Test]
        public void ReachingTarget_WaitsThenSwitchesDirection()
        {
            CampusWalkingNPC npc = CreateNpc(Vector3.zero, Point(0, 0, 0), Point(2, 0, 0), speed: 2f, wait: 1f);

            npc.Tick(1f);

            Assert.That(npc.transform.position.x, Is.EqualTo(2f).Within(0.001f));
            Assert.That(npc.IsWaiting, Is.True);
            Assert.That(npc.CurrentTargetIndex, Is.EqualTo(0), "Next target is the start point.");

            npc.Tick(0.5f);
            Assert.That(npc.transform.position.x, Is.EqualTo(2f).Within(0.001f), "Stays put while waiting.");

            npc.Tick(0.5f);
            npc.Tick(0.5f);
            Assert.That(npc.IsWaiting, Is.False);
            Assert.That(npc.transform.position.x, Is.EqualTo(1f).Within(0.001f), "Walks back toward the start point.");
        }

        [Test]
        public void YPosition_StaysLocked_EvenWhenPointsAreAtDifferentHeights()
        {
            CampusWalkingNPC npc = CreateNpc(new Vector3(0, 0.75f, 0), Point(0, -3, 0), Point(4, 6, 4), speed: 1f);

            for (int i = 0; i < 100; i++)
            {
                npc.Tick(Step);
                Assert.That(npc.transform.position.y, Is.EqualTo(0.75f).Within(0.0001f));
            }

            Assert.That(npc.transform.position.x, Is.GreaterThan(0f), "X/Z still move.");
            Assert.That(npc.transform.position.z, Is.GreaterThan(0f));
        }

        [Test]
        public void Rotates_TowardMovementDirection_AndTurnsAroundAtEnd()
        {
            CampusWalkingNPC npc = CreateNpc(Vector3.zero, Point(0, 0, 0), Point(0, 0, 3), speed: 1f, wait: 1f, rotationSpeed: 3600f);
            npc.transform.rotation = Quaternion.LookRotation(Vector3.right);

            npc.Tick(Step);
            Assert.That(Vector3.Angle(npc.transform.forward, Vector3.forward), Is.LessThan(1f), "Faces +Z toward the end point.");

            TickUntil(npc, () => npc.IsWaiting);
            npc.Tick(0.5f);
            Assert.That(Vector3.Angle(npc.transform.forward, Vector3.back), Is.LessThan(1f), "Turns around while waiting.");
        }

        [Test]
        public void Rotation_IsGradual_AndCanBeDisabled()
        {
            CampusWalkingNPC slow = CreateNpc(Vector3.zero, Point(0, 0, 0), Point(5, 0, 0), rotationSpeed: 90f);
            slow.transform.rotation = Quaternion.LookRotation(Vector3.back);
            slow.Tick(0.5f);
            Assert.That(Vector3.Angle(slow.transform.forward, Vector3.back), Is.EqualTo(45f).Within(0.5f));

            CampusWalkingNPC fixedFacing = CreateNpc(Vector3.zero, Point(0, 0, 0), Point(5, 0, 0), rotate: false);
            Quaternion before = Quaternion.LookRotation(Vector3.back);
            fixedFacing.transform.rotation = before;
            fixedFacing.Tick(0.5f);
            Assert.That(Quaternion.Angle(fixedFacing.transform.rotation, before), Is.LessThan(0.01f));
        }

        [Test]
        public void TwoPoints_LoopForever_ABAB()
        {
            CampusWalkingNPC npc = CreateNpc(Vector3.zero, Point(0, 0, 0), Point(1, 0, 0), speed: 1f, wait: 0.2f);

            var arrivals = RecordArrivals(npc, count: 5);

            Assert.That(arrivals, Is.EqualTo(new[] { 1, 0, 1, 0, 1 }));
        }

        [Test]
        public void Waypoints_LoopInOrder_ABCDA()
        {
            CampusWalkingNPC npc = CreateNpc(Vector3.zero, null, null, speed: 2f, wait: 0.1f);
            SetWaypoints(npc, Point(0, 0, 0), Point(2, 0, 0), Point(2, 0, 2), Point(0, 0, 2));

            var arrivals = RecordArrivals(npc, count: 6);

            Assert.That(arrivals, Is.EqualTo(new[] { 1, 2, 3, 0, 1, 2 }));
        }

        [Test]
        public void SnapToStartPoint_PlacesNpcOnFirstPoint_KeepingHeight()
        {
            CampusWalkingNPC npc = CreateNpc(new Vector3(9, 1.2f, 9), Point(3, 0, 4), Point(8, 0, 4));

            npc.Initialize();

            Assert.That(npc.transform.position, Is.EqualTo(new Vector3(3, 1.2f, 4)));
            Assert.That(npc.CurrentTargetIndex, Is.EqualTo(1));
        }

        [Test]
        public void MissingPoints_LogsWarning_AndDoesNotMove()
        {
            CampusWalkingNPC npc = CreateNpc(new Vector3(1, 0, 1), Point(0, 0, 0), null);
            Assert.That(npc.GetValidationMessage(), Is.EqualTo("CampusWalkingNPC requires Start Point and End Point."));

            LogAssert.Expect(LogType.Warning, "CampusWalkingNPC requires Start Point and End Point.");
            npc.Tick(Step);
            npc.Tick(Step);

            Assert.That(npc.HasValidRoute, Is.False);
            Assert.That(npc.transform.position, Is.EqualTo(new Vector3(1, 0, 1)));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ValidPoints_HaveNoValidationMessage()
        {
            CampusWalkingNPC pair = CreateNpc(Vector3.zero, Point(0, 0, 0), Point(1, 0, 0));
            CampusWalkingNPC list = CreateNpc(Vector3.zero, null, null);
            SetWaypoints(list, Point(0, 0, 0), Point(1, 0, 0));

            Assert.That(pair.GetValidationMessage(), Is.Null);
            Assert.That(list.GetValidationMessage(), Is.Null);
        }

        [Test]
        public void AnimatorSync_SetsSpeedOnlyWhenEnabled()
        {
            CampusWalkingNPC synced = CreateNpc(Vector3.zero, Point(0, 0, 0), Point(0.5f, 0, 0), speed: 1f, wait: 1f);
            Animator syncedAnimator = synced.gameObject.AddComponent<Animator>();
            Set(synced, "enableAnimatorSpeedSync", p => p.boolValue = true);

            synced.Tick(Step);
            Assert.That(syncedAnimator.speed, Is.EqualTo(1f));
            TickUntil(synced, () => synced.IsWaiting);
            Assert.That(syncedAnimator.speed, Is.EqualTo(0f));

            CampusWalkingNPC untouched = CreateNpc(Vector3.zero, Point(0, 0, 0), Point(0.5f, 0, 0), speed: 1f, wait: 1f);
            Animator untouchedAnimator = untouched.gameObject.AddComponent<Animator>();
            untouchedAnimator.speed = 0.7f;
            TickUntil(untouched, () => untouched.IsWaiting);
            Assert.That(untouchedAnimator.speed, Is.EqualTo(0.7f).Within(0.0001f));
        }

        [Test]
        public void MissingAnimator_DoesNotThrow_AndMovementContinues()
        {
            CampusWalkingNPC npc = CreateNpc(Vector3.zero, Point(0, 0, 0), Point(3, 0, 0), speed: 1f, wait: 0.5f);
            Assert.That(npc.GetComponentInChildren<Animator>(), Is.Null);

            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 60; i++)
                {
                    npc.Tick(Step);
                }
            });

            Assert.That(npc.transform.position.x, Is.GreaterThan(0f));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void AnimatorWithoutController_WarnsOnce_AndMovementContinues()
        {
            CampusWalkingNPC npc = CreateNpc(Vector3.zero, Point(0, 0, 0), Point(3, 0, 0), speed: 1f, wait: 0.2f);
            npc.gameObject.AddComponent<Animator>();

            int warnings = CountCampusWalkingLogs(() =>
            {
                for (int i = 0; i < 60; i++)
                {
                    npc.Tick(Step);
                }
            });

            Assert.That(warnings, Is.EqualTo(1), "Warn once, not every frame.");
            Assert.That(npc.transform.position.x, Is.GreaterThan(0f));
        }

        [Test]
        public void ShippedWalkingController_HasStatesTheWalkerCanDrive()
        {
            const string path = "Assets/walking npc.controller";
            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(path);
            if (controller == null)
            {
                Assert.Ignore($"{path} not found.");
            }

            Assert.That(
                controller.parameters,
                Has.Some.Matches<AnimatorControllerParameter>(p => p.name == "speed" && p.type == AnimatorControllerParameterType.Float));

            var names = new List<string>();
            foreach (var child in controller.layers[0].stateMachine.states)
            {
                names.Add(child.state.name);
            }

            // Transition settings (e.g. Has Exit Time) don't matter: the walker plays these states directly.
            Assert.That(names, Has.Some.Contains("Walk").IgnoreCase, "Auto-detected walking state.");
            Assert.That(names, Has.Some.Contains("Idle").IgnoreCase, "Auto-detected standing state.");
        }

        internal static int CountCampusWalkingLogs(System.Action action)
        {
            int count = 0;
            void Handler(string message, string stack, LogType type)
            {
                if (message.Contains("[CampusWalkingNPC]") || message.Contains("does not exist"))
                {
                    count++;
                }
            }

            Application.logMessageReceived += Handler;
            try
            {
                action();
            }
            finally
            {
                Application.logMessageReceived -= Handler;
            }

            return count;
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private Transform Point(float x, float y, float z)
        {
            var go = new GameObject($"Point_{x}_{y}_{z}");
            go.transform.position = new Vector3(x, y, z);
            created.Add(go);
            return go.transform;
        }

        private CampusWalkingNPC CreateNpc(
            Vector3 position,
            Transform start,
            Transform end,
            float speed = 1.5f,
            float wait = 0f,
            float rotationSpeed = 360f,
            bool rotate = true)
        {
            var go = new GameObject("WalkingStudent");
            go.transform.position = position;
            created.Add(go);
            CampusWalkingNPC npc = go.AddComponent<CampusWalkingNPC>();

            var so = new SerializedObject(npc);
            so.FindProperty("startPoint").objectReferenceValue = start;
            so.FindProperty("endPoint").objectReferenceValue = end;
            so.FindProperty("speed").floatValue = speed;
            so.FindProperty("waitTimeAtPoint").floatValue = wait;
            so.FindProperty("rotationSpeed").floatValue = rotationSpeed;
            so.FindProperty("rotateTowardsMovement").boolValue = rotate;
            so.ApplyModifiedPropertiesWithoutUndo();
            return npc;
        }

        private static void SetWaypoints(CampusWalkingNPC npc, params Transform[] points)
        {
            var so = new SerializedObject(npc);
            SerializedProperty list = so.FindProperty("waypoints");
            list.arraySize = points.Length;
            for (int i = 0; i < points.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(CampusWalkingNPC npc, string property, System.Action<SerializedProperty> apply)
        {
            var so = new SerializedObject(npc);
            apply(so.FindProperty(property));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void TickUntil(CampusWalkingNPC npc, System.Func<bool> condition, int maxSteps = 1000)
        {
            for (int i = 0; i < maxSteps && !condition(); i++)
            {
                npc.Tick(Step);
            }

            Assert.That(condition(), Is.True, "Condition not reached.");
        }

        /// <summary>Returns the index of each point the NPC arrives at, in order.</summary>
        private static List<int> RecordArrivals(CampusWalkingNPC npc, int count)
        {
            var arrivals = new List<int>();
            npc.Initialize();
            int target = npc.CurrentTargetIndex;
            for (int i = 0; i < 5000 && arrivals.Count < count; i++)
            {
                npc.Tick(Step);
                if (npc.CurrentTargetIndex != target)
                {
                    arrivals.Add(target);
                    target = npc.CurrentTargetIndex;
                }
            }

            return arrivals;
        }
    }

    /// <summary>
    /// Animator parameter integration. Needs play mode: Animator parameters and states only
    /// evaluate on a bound, updating Animator.
    /// </summary>
    [TestFixture]
    public sealed class CampusWalkingNPCAnimatorTests
    {
        private static readonly int SpeedHash = Animator.StringToHash("speed");
        private AnimatorController controller;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return new EnterPlayMode();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (controller != null)
            {
                Object.DestroyImmediate(controller);
            }

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator Moving_SetsSpeedParameterToWalkingValue_AndEntersWalkingState()
        {
            controller = BuildController(withSpeedParameter: true);
            (CampusWalkingNPC npc, Animator animator) = CreateNpc(new Vector3(10, 0, 0), wait: 1f);

            yield return WaitFor(() => InState(animator, "Walking"), 3f);

            Assert.That(npc.IsWaiting, Is.False);
            Assert.That(animator.GetFloat(SpeedHash), Is.EqualTo(1f));
            Assert.That(InState(animator, "Walking"), Is.True, "Idle -> Walking should fire from the speed parameter.");
            Assert.That(npc.transform.position.x, Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator Waiting_SetsSpeedParameterToWaitingValue_AndReturnsToIdle()
        {
            controller = BuildController(withSpeedParameter: true);
            (CampusWalkingNPC npc, Animator animator) = CreateNpc(new Vector3(0.3f, 0, 0), wait: 3f);

            yield return WaitFor(() => npc.IsWaiting, 3f);
            Assert.That(npc.IsWaiting, Is.True, "NPC should reach the end point.");
            Assert.That(animator.GetFloat(SpeedHash), Is.EqualTo(0f));

            yield return WaitFor(() => InState(animator, "Idle"), 2f);
            Assert.That(InState(animator, "Idle"), Is.True, "Walking -> Idle should fire while waiting.");
            Assert.That(npc.IsWaiting, Is.True);
        }

        [UnityTest]
        public IEnumerator MissingParameter_WarnsOnce_AndMovementContinues()
        {
            controller = BuildController(withSpeedParameter: false);
            int warnings = 0;
            void Handler(string message, string stack, LogType type)
            {
                if (message.Contains("[CampusWalkingNPC]") || message.Contains("does not exist"))
                {
                    warnings++;
                }
            }

            Application.logMessageReceived += Handler;
            try
            {
                (CampusWalkingNPC npc, _) = CreateNpc(new Vector3(10, 0, 0), wait: 0f);
                yield return WaitFor(() => npc.transform.position.x > 0.5f, 3f);

                Assert.That(npc.transform.position.x, Is.GreaterThan(0.5f), "Movement must not depend on animation setup.");
                Assert.That(warnings, Is.EqualTo(1), "One warning, not one per frame.");
            }
            finally
            {
                Application.logMessageReceived -= Handler;
            }
        }

        [UnityTest]
        public IEnumerator ExitTimeController_SwitchesWalkAndIdleImmediately_AndDisablesRootMotion()
        {
            controller = BuildExitTimeController();
            (CampusWalkingNPC npc, Animator animator) = CreateNpc(new Vector3(1.5f, 0, 0), wait: 5f);
            animator.applyRootMotion = true;

            float started = Time.realtimeSinceStartup;
            yield return WaitFor(() => InState(animator, "Walking 0"), 2f);
            Assert.That(InState(animator, "Walking 0"), Is.True, "Auto-detected walking state should play.");
            Assert.That(Time.realtimeSinceStartup - started, Is.LessThan(0.6f), "Idle exit time (~1.9 s) must not delay walking.");
            Assert.That(animator.applyRootMotion, Is.False, "Walker owns movement; root motion would add drift.");

            yield return WaitFor(() => npc.IsWaiting, 3f);
            Assert.That(npc.IsWaiting, Is.True);
            float stopped = Time.realtimeSinceStartup;
            yield return WaitFor(() => InState(animator, "Idle (1)"), 2f);
            Assert.That(InState(animator, "Idle (1)"), Is.True);
            Assert.That(Time.realtimeSinceStartup - stopped, Is.LessThan(0.6f), "Walking exit time must not delay standing.");
        }

        // ── Helpers ───────────────────────────────────────────────────────

        /// <summary>
        /// Mirrors the project's "walking npc" controller as authored in the Animator window:
        /// Unity-default state names and Has Exit Time on, with 2 s clips.
        /// </summary>
        internal static AnimatorController BuildExitTimeController()
        {
            var built = new AnimatorController { name = "ExitTimeWalkingController" };
            built.AddParameter("speed", AnimatorControllerParameterType.Float);
            built.AddLayer("Base Layer");
            AnimatorStateMachine machine = built.layers[0].stateMachine;
            AnimatorState idle = machine.AddState("Idle (1)");
            AnimatorState walking = machine.AddState("Walking 0");
            idle.motion = TwoSecondClip("Idle");
            walking.motion = TwoSecondClip("Walking");
            machine.defaultState = idle;

            AnimatorStateTransition toWalking = idle.AddTransition(walking);
            toWalking.hasExitTime = true;
            toWalking.exitTime = 0.97f;
            toWalking.duration = 0.25f;
            toWalking.AddCondition(AnimatorConditionMode.Greater, 0.1f, "speed");

            AnimatorStateTransition toIdle = walking.AddTransition(idle);
            toIdle.hasExitTime = true;
            toIdle.exitTime = 0.758f;
            toIdle.duration = 0.25f;
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "speed");
            return built;
        }

        private static AnimationClip TwoSecondClip(string name)
        {
            var clip = new AnimationClip { name = name };
            clip.SetCurve("UnboundChild", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 2f, 0f));
            return clip;
        }

        private (CampusWalkingNPC, Animator) CreateNpc(Vector3 end, float wait)
        {
            var start = new GameObject("PointA").transform;
            var finish = new GameObject("PointB").transform;
            finish.position = end;

            var go = new GameObject("WalkingStudent");
            Animator animator = go.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            CampusWalkingNPC npc = go.AddComponent<CampusWalkingNPC>();

            var so = new SerializedObject(npc);
            so.FindProperty("startPoint").objectReferenceValue = start;
            so.FindProperty("endPoint").objectReferenceValue = finish;
            so.FindProperty("waitTimeAtPoint").floatValue = wait;
            so.ApplyModifiedPropertiesWithoutUndo();
            return (npc, animator);
        }

        internal static AnimatorController BuildController(bool withSpeedParameter)
        {
            var built = new AnimatorController { name = "TestWalkingController" };
            if (withSpeedParameter)
            {
                built.AddParameter("speed", AnimatorControllerParameterType.Float);
            }

            built.AddLayer("Base Layer");
            AnimatorStateMachine machine = built.layers[0].stateMachine;
            AnimatorState idle = machine.AddState("Idle");
            AnimatorState walking = machine.AddState("Walking");
            machine.defaultState = idle;

            if (withSpeedParameter)
            {
                AnimatorStateTransition toWalking = idle.AddTransition(walking);
                toWalking.hasExitTime = false;
                toWalking.duration = 0.1f;
                toWalking.AddCondition(AnimatorConditionMode.Greater, 0.1f, "speed");

                AnimatorStateTransition toIdle = walking.AddTransition(idle);
                toIdle.hasExitTime = false;
                toIdle.duration = 0.1f;
                toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "speed");
            }

            return built;
        }

        internal static bool InState(Animator animator, string state)
        {
            return !animator.IsInTransition(0) && animator.GetCurrentAnimatorStateInfo(0).IsName(state);
        }

        internal static IEnumerator WaitFor(Func<bool> condition, float timeoutSeconds)
        {
            float end = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
        }
    }

    /// <summary>A walking Batchmate/Senior stands still (Idle) while talking and walks on afterwards.</summary>
    [TestFixture]
    public sealed class CampusWalkingNPCConversationTests
    {
        private static readonly int SpeedHash = Animator.StringToHash("speed");
        private readonly List<Object> assets = new List<Object>();

        private sealed class AcceptingSync : IActivityProgressSync
        {
            public bool IsHydrated => true;
            public bool IsMutationInFlight => false;

            public void RequestActivityResolve(
                string activityId,
                string outcome,
                Action<ActivityResolveResult> onSuccess,
                Action onFailure)
            {
                onSuccess?.Invoke(new ActivityResolveResult(
                    new ActivityRecord(activityId, ActivityStatus.Completed, outcome, 2, 0, 1),
                    false,
                    52f,
                    50f));
            }
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return new EnterPlayMode();
            if (PlayerSaveState.Instance != null)
            {
                Object.DestroyImmediate(PlayerSaveState.Instance.gameObject);
            }

            StudentConversationMemory.Clear();
            var player = new GameObject("WalkingNpcTestPlayer");
            player.AddComponent<PlayerStats>().ApplyServerState(50f, 50f, StatUpdateSource.InitialHydration);
            player.AddComponent<DailyActivityState>();
            player.AddComponent<DialogueUI>();
            PlayerSaveState save = player.AddComponent<PlayerSaveState>();
            save.enabled = false; // No live hydration/network requests in this fixture.
            save.SetIdentityForTesting("STUDENT", "CSE");
            save.SetDayProgressForTesting(1, 1);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (DialogueUI.Instance != null && DialogueUI.IsOpen)
            {
                DialogueUI.Instance.Hide();
            }

            foreach (Object asset in assets)
            {
                if (asset != null)
                {
                    Object.DestroyImmediate(asset);
                }
            }

            assets.Clear();
            StudentConversationMemory.Clear();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator PressingE_StopsWalkingInIdle_AndCompletedConversationResumesWalking()
        {
            (CampusWalkingNPC walker, BatchmateStudentNPC mate, Animator animator) = CreateWalkingBatchmate();
            yield return CampusWalkingNPCAnimatorTests.WaitFor(() => CampusWalkingNPCAnimatorTests.InState(animator, "Walking 0"), 2f);
            yield return CampusWalkingNPCAnimatorTests.WaitFor(() => walker.transform.position.x > 0.3f, 3f);
            Assert.That(animator.GetFloat(SpeedHash), Is.EqualTo(1f), "Walking before the conversation.");
            Assert.That(CampusWalkingNPCAnimatorTests.InState(animator, "Walking 0"), Is.True);

            Assert.That(mate.Interact(), Is.Null);
            Assert.That(DialogueUI.IsOpen, Is.True);
            float pressedAt = Time.realtimeSinceStartup;
            yield return null;
            float stoppedAt = walker.transform.position.x;

            yield return CampusWalkingNPCAnimatorTests.WaitFor(() => CampusWalkingNPCAnimatorTests.InState(animator, "Idle (1)"), 2f);
            Assert.That(walker.IsPaused, Is.True);
            Assert.That(animator.GetFloat(SpeedHash), Is.EqualTo(0f));
            Assert.That(CampusWalkingNPCAnimatorTests.InState(animator, "Idle (1)"), Is.True);
            Assert.That(Time.realtimeSinceStartup - pressedAt, Is.LessThan(0.6f), "Standing pose right after pressing E.");
            Assert.That(walker.transform.position.x, Is.EqualTo(stoppedAt).Within(0.0001f), "Stands still while talking.");

            StudentConversationRunner runner = mate.ActiveConversation;
            for (int guard = 0; guard < 20 && runner.State == StudentConversationRunner.RunState.Running; guard++)
            {
                DialogueUI.Instance.SelectChoice(0);
            }

            Assert.That(runner.State, Is.EqualTo(StudentConversationRunner.RunState.Completed));
            Assert.That(walker.transform.position.x, Is.EqualTo(stoppedAt).Within(0.0001f));

            yield return CampusWalkingNPCAnimatorTests.WaitFor(() => walker.transform.position.x > stoppedAt + 0.2f, 3f);
            Assert.That(walker.IsPaused, Is.False);
            Assert.That(walker.transform.position.x, Is.GreaterThan(stoppedAt + 0.2f), "Walks on after the goodbye.");
            Assert.That(animator.GetFloat(SpeedHash), Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator LeavingConversationEarly_AlsoResumesWalking()
        {
            (CampusWalkingNPC walker, BatchmateStudentNPC mate, _) = CreateWalkingBatchmate();
            yield return CampusWalkingNPCAnimatorTests.WaitFor(() => walker.transform.position.x > 0.3f, 3f);

            mate.Interact();
            yield return null;
            yield return null;
            Assert.That(walker.IsPaused, Is.True);
            float stoppedAt = walker.transform.position.x;

            DialogueUI.Instance.SelectChoice(3); // three responses, then "(Leave)"
            Assert.That(mate.ActiveConversation.State, Is.EqualTo(StudentConversationRunner.RunState.Abandoned));

            yield return CampusWalkingNPCAnimatorTests.WaitFor(() => walker.transform.position.x > stoppedAt + 0.2f, 3f);
            Assert.That(walker.IsPaused, Is.False);
            Assert.That(walker.transform.position.x, Is.GreaterThan(stoppedAt + 0.2f));
        }

        private (CampusWalkingNPC, BatchmateStudentNPC, Animator) CreateWalkingBatchmate()
        {
            AnimatorController controller = CampusWalkingNPCAnimatorTests.BuildExitTimeController();
            StudentDialogueTree tree = StudentDialogueTestData.BuildTree("walk_a", StudentNpcType.Batchmate);
            StudentDialogueDatabase database = StudentDialogueTestData.BuildDatabase(tree);
            assets.Add(controller);
            assets.Add(tree);
            assets.Add(database);

            Transform start = new GameObject("PointA").transform;
            Transform end = new GameObject("PointB").transform;
            end.position = new Vector3(10f, 0f, 0f);

            var go = new GameObject("WalkingBatchmate");
            Animator animator = go.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            BatchmateStudentNPC mate = go.AddComponent<BatchmateStudentNPC>();
            mate.SetNpcIdForTesting("WALKING_MATE");
            mate.SetProgressSyncForTesting(new AcceptingSync());
            mate.SetDatabaseForTesting(database);
            mate.SetRandomSeedForTesting(1);

            CampusWalkingNPC walker = go.AddComponent<CampusWalkingNPC>();
            var so = new SerializedObject(walker);
            so.FindProperty("startPoint").objectReferenceValue = start;
            so.FindProperty("endPoint").objectReferenceValue = end;
            so.FindProperty("waitTimeAtPoint").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return (walker, mate, animator);
        }
    }
}
