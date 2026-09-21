using UnityEngine;
using UIU.Simulator.Gameplay.Player;

/// <summary>
/// Campus security scanner. The player presents an ID card by pressing the Interact
/// action while looking at this object.
/// <para>
/// Scan rules:
/// <list type="bullet">
///   <item>While the one-time initial ID tutorial is still pending (server-backed), the first permanent ID scan fails and triggers the ID problem state.</item>
///   <item>While the ID problem state is active, every permanent ID scan continues to fail.</item>
///   <item>Temporary ID scans always succeed, have no random failure, and are consumed after scanning.</item>
///   <item>Consuming a temporary ID clears the ID problem.</item>
///   <item>After tutorial resolution, normal permanent ID scans have a recurring failure chance (10% by default).</item>
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
    [Tooltip("Probability (0.0 to 1.0) that a normal permanent ID scan fails and triggers a new ID problem. Default is 0.10 (10%).")]
    [SerializeField, Range(0f, 1f)] private float permanentFailChance = 0.1f;

    [Header("Gate")]
    [Tooltip("Optional gate controller to open upon successful scan.")]
    [SerializeField] private GateController gateToOpen;

    // ── Cached references ──────────────────────────────────────────────

    private InteractionFeedback feedback;
    private ScannerVisuals scannerVisuals;  // Optional — may be null.
    private PlayerInventory playerInventory;
    private PlayerProgressSync progressSync;

    // ── Public API ─────────────────────────────────────────────────────

    /// <summary>
    /// Failure probability [0, 1] for permanent ID scans after the initial tutorial failure.
    /// Default is 0.10 (10%). Set to 0 or 1 for deterministic testing.
    /// </summary>
    public float PermanentFailChance
    {
        get => permanentFailChance;
        set => permanentFailChance = Mathf.Clamp01(value);
    }

    /// <summary>
    /// Optional gate controller opened when a scan succeeds.
    /// </summary>
    public GateController GateToOpen
    {
        get => gateToOpen;
        set => gateToOpen = value;
    }

    // ── IInteractable ──────────────────────────────────────────────────

    public string InteractionPrompt => prompt;

    public string Interact()
    {
        Debug.Log($"[IDScanner] Interact() started on '{gameObject.name}'.", this);

        // Admission gate: only when PlayerSaveState is present (gameplay). EditMode tests omit it.
        PlayerSaveState saveState = PlayerSaveState.Instance != null
            ? PlayerSaveState.Instance
            : FindFirstObjectByType<PlayerSaveState>();
        if (saveState != null)
        {
            if (!saveState.IsHydrated)
            {
                Debug.Log("[IDScanner] Scan deferred: save state still loading.", this);
                return "Checking ID status…";
            }

            if (saveState.NeedsAdmission)
            {
                Debug.Log("[IDScanner] Scan FAILED: No ID card issued yet (admission required).", this);
                OnScanFailed();
                return "You do not have an ID card yet. Please visit the receptionist.";
            }
        }

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
        // 1. If player already has an active problem state, every scan continues to fail until resolved.
        if (playerInventory.HasIDProblem)
        {
            Debug.Log("[IDScanner] Scan FAILED: Player ID problem is currently active. Needs Reception resolution.", this);
            OnScanFailed();
            return "You don't have an id card, go see the receptionist";
        }

        // 2. One-time initial ID tutorial (server-backed pending flag mirrored into PlayerInventory).
        if (!playerInventory.HasTriggeredInitialIDFailure)
        {
            Debug.Log("[IDScanner] Scan FAILED: Initial ID tutorial pending. Activating ID problem state.", this);
            playerInventory.TriggerInitialIDFailure();
            EnsureProgressSync()?.RequestConsumeInitialIdTutorial();
            OnScanFailed();
            return "You don't have an id card, go see the receptionist";
        }

        // 3. Normal permanent ID scan after tutorial has been resolved: recurring failure chance (10% failure, 90% success by default).
        bool shouldFail = permanentFailChance >= 1f || (permanentFailChance > 0f && Random.value < permanentFailChance);
        if (shouldFail)
        {
            Debug.Log($"[IDScanner] Scan FAILED: Recurring permanent ID failure rolled ({permanentFailChance * 100f:0.#}% chance). Activating ID problem state.", this);
            playerInventory.TriggerIDProblem();
            OnScanFailed();
            return "You don't have an id card, go see the receptionist";
        }

        Debug.Log("[IDScanner] Scan SUCCEEDED: Permanent ID accepted.", this);
        OnScanSucceeded();
        return "Access granted. Welcome!";
    }

    private string HandleTemporaryScan()
    {
        if (playerInventory.TemporaryIDCount <= 0)
        {
            Debug.Log("[IDScanner] Scan FAILED: No temporary ID cards in inventory.", this);
            OnScanFailed();
            return "No temporary ID card available.";
        }

        // Temporary IDs always succeed, have no random failure, and are consumed after scanning.
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
        gateToOpen?.Open();
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

    private PlayerProgressSync EnsureProgressSync()
    {
        if (progressSync != null)
        {
            return progressSync;
        }

        if (playerInventory != null)
        {
            progressSync = playerInventory.GetComponent<PlayerProgressSync>();
        }

        if (progressSync == null)
        {
            progressSync = FindFirstObjectByType<PlayerProgressSync>();
        }

        return progressSync;
    }
}
