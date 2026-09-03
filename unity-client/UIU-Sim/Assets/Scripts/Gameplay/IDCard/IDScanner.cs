using UnityEngine;

/// <summary>
/// Campus security scanner. The player presents an ID card by pressing the Interact
/// action while looking at this object.
/// <para>
/// Scan rules:
/// <list type="bullet">
///   <item>First scan with a permanent ID always fails (tutorial moment).</item>
///   <item>Subsequent permanent ID scans have a 5 % random failure chance.</item>
///   <item>Temporary ID scans always succeed but consume the card.</item>
/// </list>
/// </para>
/// <para>
/// Requires <see cref="InteractionFeedback"/> on the same GameObject for audio/LED
/// feedback, and optionally <see cref="ScannerVisuals"/> for light/emission effects.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(InteractionFeedback))]
public sealed class IDScanner : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [SerializeField] private string prompt = "Scan ID";

    [Header("Scan Settings")]
    [Tooltip("Chance (0–1) that a permanent ID scan fails after the guaranteed first failure.")]
    [SerializeField, Range(0f, 1f)] private float permanentFailChance = 0.05f;

    // ── Cached references ──────────────────────────────────────────────

    private InteractionFeedback feedback;
    private ScannerVisuals scannerVisuals;  // Optional — may be null.
    private PlayerInventory playerInventory;
    private bool isFirstAttempt = true;

    // ── IInteractable ──────────────────────────────────────────────────

    public string InteractionPrompt => prompt;

    public string Interact()
    {
        Debug.Log($"[IDScanner] Interact() started on '{gameObject.name}'.", this);

        if (!EnsurePlayerInventory())
        {
            Debug.LogError("[IDScanner] Interact() aborted: PlayerInventory not found in scene.", this);
            return "Error: player inventory not found.";
        }

        IDCardType card = playerInventory.CurrentIDCard;
        Debug.Log($"[IDScanner] Interact() card evaluated: {card}", this);

        switch (card)
        {
            case IDCardType.None:
                Debug.Log("[IDScanner] Scan FAILED: No card selected in inventory.", this);
                OnScanFailed();
                return "No ID card selected.";

            case IDCardType.Permanent:
                return HandlePermanentScan();

            case IDCardType.Temporary:
                return HandleTemporaryScan();

            default:
                Debug.LogError($"[IDScanner] Unhandled card type: {card}", this);
                return "Unknown card type.";
        }
    }

    // ── Lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        feedback = GetComponent<InteractionFeedback>();
        scannerVisuals = GetComponent<ScannerVisuals>();
        Debug.Log($"[IDScanner] Awake on '{gameObject.name}' — feedback={(feedback != null ? "Found" : "MISSING")}, scannerVisuals={(scannerVisuals != null ? "Found" : "None")}", this);
    }

    // ── Scan Handlers ──────────────────────────────────────────────────

    private string HandlePermanentScan()
    {
        // First scan with a permanent ID always fails — a tutorial/narrative beat.
        if (isFirstAttempt)
        {
            isFirstAttempt = false;
            Debug.Log("[IDScanner] Scan FAILED: Permanent ID first-attempt rule triggered.", this);
            OnScanFailed();
            return "ID scan failed! Card not recognized. Try again or visit the Receptionist.";
        }

        // Subsequent scans have a small random failure chance.
        if (Random.value < permanentFailChance)
        {
            Debug.Log("[IDScanner] Scan FAILED: Permanent ID 5% random failure rolled.", this);
            OnScanFailed();
            return "ID scan failed! Please try again.";
        }

        Debug.Log("[IDScanner] Scan SUCCEEDED: Permanent ID accepted.", this);
        OnScanSucceeded();
        return "Access granted. Welcome!";
    }

    private string HandleTemporaryScan()
    {
        // Temporary IDs always succeed but are consumed.
        Debug.Log("[IDScanner] Scan SUCCEEDED: Temporary ID accepted, consuming card.", this);
        playerInventory.ConsumeTemporaryID();
        OnScanSucceeded();
        return "Temporary access granted.";
    }

    // ── Feedback ───────────────────────────────────────────────────────

    private void OnScanSucceeded()
    {
        Debug.Log($"[IDScanner] Calling feedback.PlaySuccess() immediately on '{gameObject.name}'", this);
        feedback.PlaySuccess();
        if (scannerVisuals != null)
        {
            scannerVisuals.ShowSuccess();
        }
    }

    private void OnScanFailed()
    {
        Debug.Log($"[IDScanner] Calling feedback.PlayFailure() immediately on '{gameObject.name}'", this);
        feedback.PlayFailure();
        if (scannerVisuals != null)
        {
            scannerVisuals.ShowFailure();
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private bool EnsurePlayerInventory()
    {
        if (playerInventory != null)
        {
            return true;
        }

        playerInventory = FindFirstObjectByType<PlayerInventory>();

        if (playerInventory == null)
        {
            Debug.LogError(
                "[IDScanner] No PlayerInventory found in the scene. " +
                "Make sure the Player prefab has a PlayerInventory component.",
                this);
            return false;
        }

        return true;
    }
}
