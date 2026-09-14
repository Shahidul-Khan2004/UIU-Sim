using UnityEngine;

namespace UIU.Simulator.Gameplay.Doors
{
    /// <summary>
    /// MVP door traversal: press Interact while looking at the door to teleport
    /// a short distance to the opposite side of the same doorway.
    /// <para>
    /// Attach to the door root. Side detection uses this transform's forward
    /// (local Z), matching the standard university door prefabs. The door model
    /// itself never moves.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DoorTeleportInteractable : MonoBehaviour, IInteractable
    {
        private const float PlaneEpsilon = 0.05f;

        [Header("Interaction")]
        [SerializeField] private string interactionPrompt = "Use Door";

        [Header("Traversal")]
        [Tooltip("How far beyond the door plane the player is placed (metres).")]
        [SerializeField, Min(0.1f)] private float exitDistance = 1.1f;

        [Tooltip("Prevents an immediate re-trigger after teleporting.")]
        [SerializeField, Min(0f)] private float interactionCooldown = 0.3f;

        private float lastInteractTime = float.NegativeInfinity;

        public string InteractionPrompt => interactionPrompt;

        public float ExitDistance => exitDistance;

        public string Interact()
        {
            if (Time.time - lastInteractTime < interactionCooldown)
            {
                return null;
            }

            PlayerMovement player = Object.FindFirstObjectByType<PlayerMovement>();
            if (player == null)
            {
                Debug.LogWarning(
                    "[DoorTeleportInteractable] No active PlayerMovement found. Door traversal skipped.",
                    this);
                return null;
            }

            Transform playerTransform = player.transform;
            Quaternion preservedRotation = playerTransform.rotation;

            Vector3 destination = CalculateOppositeSideDestination(
                transform.position,
                transform.forward,
                playerTransform.position,
                playerTransform.forward,
                exitDistance);

            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = false;
            }

            playerTransform.position = destination;
            playerTransform.rotation = preservedRotation;

            if (controller != null)
            {
                controller.enabled = true;
            }

            lastInteractTime = Time.time;
            return null;
        }

        /// <summary>
        /// Projects the player onto the horizontal door plane, then places them
        /// <paramref name="exitDistance"/> metres onto the opposite side.
        /// </summary>
        public static Vector3 CalculateOppositeSideDestination(
            Vector3 doorPosition,
            Vector3 doorForward,
            Vector3 playerPosition,
            Vector3 playerForward,
            float exitDistance)
        {
            Vector3 passageDirection = FlattenHorizontal(doorForward);
            if (passageDirection.sqrMagnitude < 0.0001f)
            {
                passageDirection = Vector3.forward;
            }
            else
            {
                passageDirection.Normalize();
            }

            float signedDistance = Vector3.Dot(playerPosition - doorPosition, passageDirection);

            float destinationSide;
            if (Mathf.Abs(signedDistance) < PlaneEpsilon)
            {
                Vector3 facing = FlattenHorizontal(playerForward);
                float facingDot = facing.sqrMagnitude > 0.0001f
                    ? Vector3.Dot(facing.normalized, passageDirection)
                    : 0f;
                destinationSide = facingDot >= 0f ? 1f : -1f;
            }
            else
            {
                destinationSide = signedDistance >= 0f ? -1f : 1f;
            }

            Vector3 projectedOntoDoorPlane = playerPosition - passageDirection * signedDistance;
            Vector3 destination = projectedOntoDoorPlane + passageDirection * destinationSide * exitDistance;

            // Same-floor doors: keep the player's current height.
            destination.y = playerPosition.y;
            return destination;
        }

        private static Vector3 FlattenHorizontal(Vector3 value)
        {
            value.y = 0f;
            return value;
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(interactionPrompt))
            {
                interactionPrompt = "Use Door";
            }

            exitDistance = Mathf.Max(0.1f, exitDistance);
            interactionCooldown = Mathf.Max(0f, interactionCooldown);
        }
    }
}
