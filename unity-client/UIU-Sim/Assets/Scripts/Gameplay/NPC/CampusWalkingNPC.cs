using System.Collections.Generic;
using UnityEngine;

namespace UIU.Simulator.Gameplay.NPC
{
    /// <summary>
    /// Implemented by components that need a <see cref="CampusWalkingNPC"/> on the same NPC
    /// (self, parent or child) to stand still, e.g. while the player is talking to it.
    /// Sources are discovered when the walker initializes.
    /// </summary>
    public interface ICampusWalkingPauseSource
    {
        bool PausesWalking { get; }
    }

    /// <summary>
    /// Ambient patrol for campus NPC models: walks Start → End → Start forever, pausing at each point.
    /// Assign two or more <see cref="waypoints"/> instead to loop A → B → C → D → A.
    /// Moves and rotates the transform, and sets the Animator's "speed" float (walking / waiting value)
    /// so the model's own controller switches between Idle and Walking.
    /// Independent of every dialogue / interaction system.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CampusWalkingNPC : MonoBehaviour
    {
        public const string MissingPointsWarning = "CampusWalkingNPC requires Start Point and End Point.";
        public const string DefaultSpeedParameter = "speed";

        private const float DirectionEpsilon = 0.0001f;

        [Header("Movement Settings")]
        [Tooltip("Walking speed in metres per second.")]
        [SerializeField, Min(0f)] private float speed = 1.5f;

        [SerializeField] private Transform startPoint;
        [SerializeField] private Transform endPoint;

        [Tooltip("Optional. With two or more entries this route is used instead of Start/End " +
                 "and loops back to the first waypoint after the last.")]
        [SerializeField] private List<Transform> waypoints = new List<Transform>();

        [Tooltip("Horizontal distance at which a point counts as reached.")]
        [SerializeField, Min(0.001f)] private float arrivalDistance = 0.1f;

        [Tooltip("Seconds to pause at each point before walking to the next one.")]
        [SerializeField, Min(0f)] private float waitTimeAtPoint = 2f;

        [Tooltip("Place the NPC on the first point when it starts. Off = walk there from where it stands.")]
        [SerializeField] private bool snapToStartPoint = true;

        [Header("Rotation Settings")]
        [SerializeField] private bool rotateTowardsMovement = true;

        [Tooltip("Turn speed in degrees per second. The NPC also turns toward its next point while waiting.")]
        [SerializeField, Min(0f)] private float rotationSpeed = 360f;

        [Header("Height Settings")]
        [Tooltip("Keep the Y position captured at start; only X and Z change.")]
        [SerializeField] private bool lockYPosition = true;

        [Header("Animation")]
        [Tooltip("Empty = first Animator on this object or its children.")]
        [SerializeField] private Animator animator;

        [Tooltip("Animator state played while walking (base layer). Played directly, so controller exit times " +
                 "never delay it. Auto-filled in the editor with the first state whose name contains \"walk\".")]
        [SerializeField] private string walkingStateName = "Walking";

        [Tooltip("Animator state played while standing still (waiting or talking). " +
                 "Auto-filled in the editor with the first state whose name contains \"idle\".")]
        [SerializeField] private string idleStateName = "Idle";

        [Tooltip("Cross-fade seconds when switching between standing and walking.")]
        [SerializeField, Min(0f)] private float stateBlendTime = 0.1f;

        [Tooltip("This component moves the NPC. Root motion from the walk clip would add extra movement " +
                 "and keep sliding the NPC after it should have stopped.")]
        [SerializeField] private bool disableRootMotion = true;

        [Tooltip("Float parameter driving Idle <-> Walking transitions. Empty = never set.")]
        [SerializeField] private string animatorSpeedParameter = DefaultSpeedParameter;

        [SerializeField] private float walkingParameterValue = 1f;
        [SerializeField] private float waitingParameterValue;

        [Tooltip("Also change Animator.speed (playback rate). Off = playback rate is never touched.")]
        [SerializeField] private bool enableAnimatorSpeedSync;

        [SerializeField, Min(0f)] private float walkingAnimatorSpeed = 1f;

        [Tooltip("Animator speed while waiting at a point. 0 freezes the Animator, including the Idle transition.")]
        [SerializeField, Min(0f)] private float waitingAnimatorSpeed;

        private readonly List<Transform> route = new List<Transform>();
        private bool initialized;
        private bool warned;
        private float lockedY;
        private int targetIndex;
        private float waitRemaining;

        private string cachedParameterName;
        private int parameterHash;
        private Animator checkedAnimator;
        private bool parameterAvailable;
        private bool parameterWarned;

        private Animator stateAnimator;
        private int walkingStateHash;
        private int idleStateHash;
        private bool statesAvailable;
        private bool statesWarned;

        private readonly List<ICampusWalkingPauseSource> pauseSources = new List<ICampusWalkingPauseSource>();
        private bool pausedBySource;

        public bool HasValidRoute => initialized && route.Count >= 2;
        public bool IsWaiting => waitRemaining > 0f;

        /// <summary>True while a component on this NPC (e.g. an active conversation) holds it in place.</summary>
        public bool IsPaused => pausedBySource;
        public int CurrentTargetIndex => targetIndex;
        public Transform CurrentTarget => HasValidRoute ? route[targetIndex] : null;
        public float LockedY => lockedY;

        /// <summary>Null when the configured points form a usable route.</summary>
        public string GetValidationMessage()
        {
            return CountValid(waypoints) >= 2 || (startPoint != null && endPoint != null)
                ? null
                : MissingPointsWarning;
        }

        private void Start()
        {
            Initialize();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>Rebuilds the route from the Inspector fields and restarts from the first point.</summary>
        public void Initialize()
        {
            initialized = true;
            lockedY = transform.position.y;
            waitRemaining = 0f;
            pausedBySource = false;
            BuildRoute();
            FindPauseSources();

            if (route.Count < 2)
            {
                if (!warned)
                {
                    warned = true;
                    Debug.LogWarning(MissingPointsWarning, this);
                }

                return;
            }

            warned = false;
            if (snapToStartPoint)
            {
                transform.position = Constrain(route[0].position);
                targetIndex = 1;
            }
            else
            {
                targetIndex = 0;
            }

            if (disableRootMotion && ResolveAnimator() != null)
            {
                animator.applyRootMotion = false;
            }

            stateAnimator = null;
            SyncAnimator(moving: true);
            ApplyAnimation(moving: true);
        }

        /// <summary>Advances movement by <paramref name="deltaTime"/> seconds.</summary>
        public void Tick(float deltaTime)
        {
            if (!initialized)
            {
                Initialize();
            }

            if (!HasValidRoute || deltaTime <= 0f)
            {
                return;
            }

            if (AnyPauseSourceActive())
            {
                if (!pausedBySource)
                {
                    pausedBySource = true;
                    SyncAnimator(moving: false);
                }

                ApplyAnimation(moving: false);
                return;
            }

            if (pausedBySource)
            {
                pausedBySource = false;
                if (!IsWaiting)
                {
                    SyncAnimator(moving: true);
                }
            }

            StepMovement(deltaTime);

            // Applied every tick: an Animator rebind (e.g. object re-enabled) resets state and parameters.
            ApplyAnimation(moving: !IsWaiting);
        }

        private void StepMovement(float deltaTime)
        {
            Transform target = route[targetIndex];
            if (target == null)
            {
                // A waypoint was destroyed at runtime; skip it rather than stall.
                AdvanceTarget();
                return;
            }

            Vector3 goal = Constrain(target.position);

            if (IsWaiting)
            {
                RotateToward(goal - transform.position, deltaTime);
                waitRemaining -= deltaTime;
                if (!IsWaiting)
                {
                    SyncAnimator(moving: true);
                }

                return;
            }

            Vector3 position = transform.position;
            if (PlanarDistance(position, goal) > arrivalDistance)
            {
                RotateToward(goal - position, deltaTime);
                position = Vector3.MoveTowards(position, goal, speed * deltaTime);
                transform.position = lockYPosition ? new Vector3(position.x, lockedY, position.z) : position;
            }

            if (PlanarDistance(transform.position, goal) <= arrivalDistance)
            {
                AdvanceTarget();
                if (waitTimeAtPoint > 0f)
                {
                    waitRemaining = waitTimeAtPoint;
                    SyncAnimator(moving: false);
                }
            }
        }

        private void BuildRoute()
        {
            route.Clear();
            if (CountValid(waypoints) >= 2)
            {
                foreach (Transform waypoint in waypoints)
                {
                    if (waypoint != null)
                    {
                        route.Add(waypoint);
                    }
                }
            }
            else if (startPoint != null && endPoint != null)
            {
                route.Add(startPoint);
                route.Add(endPoint);
            }
        }

        private void FindPauseSources()
        {
            pauseSources.Clear();
            foreach (ICampusWalkingPauseSource source in GetComponentsInParent<ICampusWalkingPauseSource>(true))
            {
                pauseSources.Add(source);
            }

            foreach (ICampusWalkingPauseSource source in GetComponentsInChildren<ICampusWalkingPauseSource>(true))
            {
                if (!pauseSources.Contains(source))
                {
                    pauseSources.Add(source);
                }
            }
        }

        private bool AnyPauseSourceActive()
        {
            foreach (ICampusWalkingPauseSource source in pauseSources)
            {
                if (source is Object unityObject && unityObject == null)
                {
                    continue;
                }

                if (source.PausesWalking)
                {
                    return true;
                }
            }

            return false;
        }

        private void AdvanceTarget()
        {
            targetIndex = (targetIndex + 1) % route.Count;
        }

        private void RotateToward(Vector3 direction, float deltaTime)
        {
            if (!rotateTowardsMovement)
            {
                return;
            }

            direction.y = 0f;
            if (direction.sqrMagnitude < DirectionEpsilon)
            {
                return;
            }

            Quaternion look = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, rotationSpeed * deltaTime);
        }

        private Vector3 Constrain(Vector3 point)
        {
            return lockYPosition ? new Vector3(point.x, lockedY, point.z) : point;
        }

        private void SyncAnimator(bool moving)
        {
            if (!enableAnimatorSpeedSync)
            {
                return;
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (animator != null)
            {
                animator.speed = moving ? walkingAnimatorSpeed : waitingAnimatorSpeed;
            }
        }

        private Animator ResolveAnimator()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            return animator;
        }

        private void ApplyAnimation(bool moving)
        {
            ApplyAnimatorParameter(moving);
            ApplyAnimatorState(moving);
        }

        /// <summary>
        /// Plays the standing / walking state directly so the switch is immediate
        /// regardless of the controller's transition settings (e.g. Has Exit Time).
        /// </summary>
        private void ApplyAnimatorState(bool moving)
        {
            Animator target = ResolveAnimator();
            if (target == null || target.runtimeAnimatorController == null || !target.isInitialized)
            {
                return;
            }

            if (stateAnimator != target)
            {
                stateAnimator = target;
#if UNITY_EDITOR
                AutoAssignStateNames();
#endif
                bool named = !string.IsNullOrWhiteSpace(walkingStateName) && !string.IsNullOrWhiteSpace(idleStateName);
                walkingStateHash = named ? Animator.StringToHash(walkingStateName) : 0;
                idleStateHash = named ? Animator.StringToHash(idleStateName) : 0;
                statesAvailable = named && target.HasState(0, walkingStateHash) && target.HasState(0, idleStateHash);
                if (named && !statesAvailable && !statesWarned)
                {
                    statesWarned = true;
                    Debug.LogWarning(
                        $"[CampusWalkingNPC] Animator on '{target.name}' has no base-layer states '{walkingStateName}' / " +
                        $"'{idleStateName}'; falling back to the controller's own transitions.",
                        this);
                }
            }

            if (!statesAvailable)
            {
                return;
            }

            int desired = moving ? walkingStateHash : idleStateHash;
            int playing = target.IsInTransition(0)
                ? target.GetNextAnimatorStateInfo(0).shortNameHash
                : target.GetCurrentAnimatorStateInfo(0).shortNameHash;
            if (playing != desired)
            {
                target.CrossFadeInFixedTime(desired, stateBlendTime, 0);
            }
        }

#if UNITY_EDITOR
        private void Reset()
        {
            AutoAssignStateNames();
        }

        private void OnValidate()
        {
            AutoAssignStateNames();
        }

        /// <summary>Fills state names from the assigned controller when the current names don't exist in it.</summary>
        private void AutoAssignStateNames()
        {
            Animator source = animator != null ? animator : GetComponentInChildren<Animator>();
            RuntimeAnimatorController runtime = source != null ? source.runtimeAnimatorController : null;
            if (runtime is AnimatorOverrideController overrideController)
            {
                runtime = overrideController.runtimeAnimatorController;
            }

            var controller = runtime as UnityEditor.Animations.AnimatorController;
            if (controller == null || controller.layers.Length == 0)
            {
                return;
            }

            UnityEditor.Animations.ChildAnimatorState[] states = controller.layers[0].stateMachine.states;
            string walking = ResolveStateName(states, walkingStateName, "walk");
            string idle = ResolveStateName(states, idleStateName, "idle");
            if (walking == walkingStateName && idle == idleStateName)
            {
                return;
            }

            walkingStateName = walking;
            idleStateName = idle;
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }

        private static string ResolveStateName(
            UnityEditor.Animations.ChildAnimatorState[] states,
            string current,
            string keyword)
        {
            string match = null;
            foreach (UnityEditor.Animations.ChildAnimatorState child in states)
            {
                string name = child.state.name;
                if (name == current)
                {
                    return current;
                }

                if (match == null && name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    match = name;
                }
            }

            return match ?? current;
        }
#endif

        private void ApplyAnimatorParameter(bool moving)
        {
            if (string.IsNullOrWhiteSpace(animatorSpeedParameter))
            {
                return;
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
                if (animator == null)
                {
                    return;
                }
            }

            if (cachedParameterName != animatorSpeedParameter)
            {
                cachedParameterName = animatorSpeedParameter;
                parameterHash = Animator.StringToHash(animatorSpeedParameter);
                checkedAnimator = null;
                parameterWarned = false;
            }

            if (animator.runtimeAnimatorController == null)
            {
                WarnParameterOnce("has no Animator Controller");
                return;
            }

            // Parameters are only queryable once the Animator is bound (active and enabled).
            if (!animator.isInitialized)
            {
                return;
            }

            if (checkedAnimator != animator)
            {
                checkedAnimator = animator;
                parameterAvailable = HasFloatParameter(animator, parameterHash);
                if (!parameterAvailable)
                {
                    WarnParameterOnce($"has no float parameter '{animatorSpeedParameter}'");
                }
            }

            if (parameterAvailable)
            {
                animator.SetFloat(parameterHash, moving ? walkingParameterValue : waitingParameterValue);
            }
        }

        private void WarnParameterOnce(string problem)
        {
            if (parameterWarned)
            {
                return;
            }

            parameterWarned = true;
            Debug.LogWarning(
                $"[CampusWalkingNPC] Animator on '{animator.name}' {problem}; walking animation will not switch. Movement continues.",
                this);
        }

        private static bool HasFloatParameter(Animator target, int hash)
        {
            foreach (AnimatorControllerParameter parameter in target.parameters)
            {
                if (parameter.nameHash == hash && parameter.type == AnimatorControllerParameterType.Float)
                {
                    return true;
                }
            }

            return false;
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static int CountValid(List<Transform> points)
        {
            int count = 0;
            if (points == null)
            {
                return count;
            }

            foreach (Transform point in points)
            {
                if (point != null)
                {
                    count++;
                }
            }

            return count;
        }

        private void OnDrawGizmosSelected()
        {
            var points = new List<Transform>();
            if (CountValid(waypoints) >= 2)
            {
                points.AddRange(waypoints.FindAll(p => p != null));
            }
            else if (startPoint != null && endPoint != null)
            {
                points.Add(startPoint);
                points.Add(endPoint);
            }

            Gizmos.color = Color.cyan;
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 from = points[i].position;
                Vector3 to = points[(i + 1) % points.Count].position;
                Gizmos.DrawWireSphere(from, arrivalDistance + 0.1f);
                Gizmos.DrawLine(from, to);
            }
        }
    }
}
