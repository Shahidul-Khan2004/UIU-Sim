using UnityEngine;

/// <summary>
/// Bridges <see cref="PlayerMovement"/> locomotion into the character Animator.
/// Reads horizontal movement speed only; does not control movement, camera, or input.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerAnimatorController : MonoBehaviour
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    [Header("References")]
    [Tooltip("Animator on the humanoid character model (not the first-person camera).")]
    [SerializeField] private Animator animator;

    [Tooltip("Existing player movement on the Player root. Assigned automatically if left empty.")]
    [SerializeField] private PlayerMovement playerMovement;

    private void Awake()
    {
        if (playerMovement == null)
        {
            playerMovement = GetComponent<PlayerMovement>();
            if (playerMovement == null)
            {
                playerMovement = GetComponentInParent<PlayerMovement>();
            }
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator == null)
        {
            Debug.LogWarning(
                "[PlayerAnimatorController] No Animator assigned or found in children.",
                this);
        }

        if (playerMovement == null)
        {
            Debug.LogWarning(
                "[PlayerAnimatorController] No PlayerMovement assigned or found on this object / parent.",
                this);
        }
    }

    private void LateUpdate()
    {
        if (animator == null)
        {
            return;
        }

        float speed = 0f;
        if (playerMovement != null && playerMovement.enabled)
        {
            Vector3 velocity = playerMovement.Velocity;
            velocity.y = 0f;
            speed = velocity.magnitude;
        }

        animator.SetFloat(SpeedHash, speed);
    }
}
