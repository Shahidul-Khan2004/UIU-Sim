using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Raycasts from the camera each frame to detect <see cref="IInteractable"/> objects
/// and invokes them when the player presses the <c>Player/Interact</c> action.
/// <para>
/// Attach this to the Player GameObject. The camera is resolved automatically
/// from children, matching the existing <see cref="FirstPersonLook"/> hierarchy.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class InteractionController : MonoBehaviour
{
    [Header("Raycast")]
    [Tooltip("Maximum distance to detect interactables.")]
    [SerializeField, Min(0.1f)] private float interactionRange = 3f;

    [Tooltip("Layers the raycast can hit. Leave as Everything unless you want to filter.")]
    [SerializeField] private LayerMask interactionMask = ~0; // Everything

    [Header("Debug")]
    [SerializeField] private bool drawDebugRay = true;

    private Camera playerCamera;
    private InputAction interactAction;
    private IInteractable currentTarget;
    private string lastResponse;
    private float responseTime;

    /// <summary>Duration in seconds the response message stays visible.</summary>
    private const float ResponseDuration = 2.5f;

    // ── Lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        // Reuse the project-wide Input Actions asset (same pattern as FirstPersonLook).
        interactAction = InputSystem.actions?.FindAction("Player/Interact");

        if (interactAction == null)
        {
            Debug.LogWarning(
                "[InteractionController] Player/Interact action not found. " +
                "Interaction input will be disabled.",
                this);
        }
    }

    private void OnEnable()
    {
        interactAction?.Enable();
    }

    private void OnDisable()
    {
        currentTarget = null;
    }

    private void Update()
    {
        // Lazy-resolve camera: the Player is spawned at runtime by PlayerSpawner,
        // so the child FirstPersonCamera may not be available during Awake.
        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
            if (playerCamera == null)
            {
                // Fallback: maybe Camera.main is already the first-person camera.
                playerCamera = Camera.main;
            }

            if (playerCamera != null)
            {
                Debug.Log($"[InteractionController] Camera resolved: {playerCamera.name}", playerCamera);
            }
            else
            {
                return; // Camera not ready yet, try again next frame.
            }
        }

        UpdateTarget();
        HandleInput();

        // Expire the response message after the display duration.
        if (lastResponse != null && Time.time - responseTime > ResponseDuration)
        {
            lastResponse = null;
        }
    }

    // ── Raycast ────────────────────────────────────────────────────────

    private void UpdateTarget()
    {
        IInteractable previousTarget = currentTarget;
        currentTarget = null;

        Transform cameraTransform = playerCamera.transform;
        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);

        if (drawDebugRay)
        {
            Debug.DrawRay(ray.origin, ray.direction * interactionRange, Color.cyan);
        }

        if (!Physics.Raycast(ray, out RaycastHit hit, interactionRange, interactionMask))
        {
            if (previousTarget != null)
            {
                OnTargetLost(previousTarget);
            }
            return;
        }

        // Check the hit object and its parents for an IInteractable.
        IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
        if (interactable == null)
        {
            if (previousTarget != null)
            {
                OnTargetLost(previousTarget);
            }
            return;
        }

        currentTarget = interactable;

        if (currentTarget != previousTarget)
        {
            OnTargetFound(currentTarget);
        }
    }

    // ── Input ──────────────────────────────────────────────────────────

    private void HandleInput()
    {
        if (currentTarget == null || interactAction == null)
        {
            return;
        }

        // Block interaction input while a dialogue panel is open.
        // DialogueUI.IsOpen is a static bool set by DialogueUI.Show / Hide.
        if (DialogueUI.IsOpen)
        {
            return;
        }

        // WasPressedThisFrame fires on initial button-down, ignoring the Hold interaction
        // on the action asset. Swap to WasPerformedThisFrame() if you want hold-to-interact.
        if (interactAction.WasPressedThisFrame())
        {
            Debug.Log($"[Interact] Interacting with: {((MonoBehaviour)currentTarget).gameObject.name}");
            string response = currentTarget.Interact();
            if (!string.IsNullOrEmpty(response))
            {
                lastResponse = response;
                responseTime = Time.time;
            }
        }
    }

    // ── Callbacks (extend here for UI / events) ────────────────────────

    private static void OnTargetFound(IInteractable target)
    {
        Debug.Log($"[Interact] Looking at: \"{target.InteractionPrompt}\"");
    }

    private static void OnTargetLost(IInteractable target)
    {
        Debug.Log("[Interact] Target lost.");
    }

    // ── Public API ─────────────────────────────────────────────────────

    /// <summary>The interactable currently under the crosshair, or null.</summary>
    public IInteractable CurrentTarget => currentTarget;

    /// <summary>True when the player is looking at something interactable.</summary>
    public bool HasTarget => currentTarget != null;

    /// <summary>The response message from the last interaction, or null if expired/none.</summary>
    public string LastResponse => lastResponse;
}
