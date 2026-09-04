using UnityEngine;

/// <summary>
/// Campus security scanner. The player presents an ID card by pressing the Interact
/// action while looking at this object.
/// <para>
/// Scan rules:
/// <list type="bullet">
///   <item>First scan with a permanent ID always fails and triggers the ID problem state.</item>
///   <item>While the ID problem state is active, scans fail until reception resolves it.</item>
///   <item>Temporary ID scans always succeed and are consumed after scanning.</item>
///   <item>Consuming a temporary ID resolves the problem and permanent ID works normally forever.</item>
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

    // ── Cached references ──────────────────────────────────────────────

    private InteractionFeedback feedback;
    private ScannerVisuals scannerVisuals;  // Optional — may be null.
    private PlayerInventory playerInventory;

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
        // If the permanent ID problem has been resolved by reception, it works normally forever.
        if (playerInventory.IsPermanentIDResolved)
        {
            Debug.Log("[IDScanner] Scan SUCCEEDED: Permanent ID is verified and active forever.", this);
            OnScanSucceeded();
            return "Access granted. Welcome!";
        }

        // If player already triggered the problem state, every scan continues to fail until resolved.
        if (playerInventory.HasIDProblem)
        {
            Debug.Log("[IDScanner] Scan FAILED: Player ID problem is currently active. Needs Reception resolution.", this);
            OnScanFailed();
            return "You don't have an id card, go see the receptionist";
        }

        // First permanent ID scan always fails and triggers the player's ID problem state.
        Debug.Log("[IDScanner] Scan FAILED: First permanent ID scan failed. Activating ID problem state.", this);
        playerInventory.TriggerIDProblem();
        OnScanFailed();
        return "You don't have an id card, go see the receptionist";
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
