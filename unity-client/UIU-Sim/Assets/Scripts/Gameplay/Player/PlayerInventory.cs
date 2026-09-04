using System;
using UnityEngine;

/// <summary>
/// Manages the player's campus ID cards.
/// Attach to the Player prefab root alongside <see cref="PlayerStats"/>.
/// <para>
/// The player always starts with a permanent ID card selected.
/// Temporary cards are single-use passes obtained from the Receptionist.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerInventory : MonoBehaviour
{
    [Header("Initial State")]
    [Tooltip("Whether the player begins the game with a permanent ID card.")]
    [SerializeField] private bool startWithPermanentID = true;

    [Header("ID Status")]
    [Tooltip("True while the player is in the ID problem state.")]
    [SerializeField] private bool hasIDProblem;

    [Tooltip("True once the player has triggered the initial tutorial ID failure.")]
    [SerializeField] private bool hasTriggeredInitialIDFailure;

    private bool hasPermanentID;
    private int temporaryIDCount;
    private IDCardType currentIDCard;

    // ── Public API — Queries ───────────────────────────────────────────

    /// <summary>The card type the player will present at the next scanner.</summary>
    public IDCardType CurrentIDCard => currentIDCard;

    /// <summary>True if the player owns a permanent university ID (never consumed).</summary>
    public bool HasPermanentID => hasPermanentID;

    /// <summary>Number of single-use temporary ID cards in the player's possession.</summary>
    public int TemporaryIDCount => temporaryIDCount;

    /// <summary>True while the player is in the ID problem state. Every permanent scan fails until resolved.</summary>
    public bool HasIDProblem => hasIDProblem;

    /// <summary>True once the player has triggered the initial tutorial ID failure.</summary>
    public bool HasTriggeredInitialIDFailure => hasTriggeredInitialIDFailure;

    /// <summary>Raised when the selected card type changes. Payload is the new type.</summary>
    public event Action<IDCardType> OnCardChanged;

    /// <summary>Raised when the ID problem state changes. Payload is true if problem active, false if resolved.</summary>
    public event Action<bool> OnIDProblemChanged;

    // ── Public API — Mutations ─────────────────────────────────────────

    /// <summary>
    /// Grants the permanent university ID card. Idempotent — calling twice is safe.
    /// </summary>
    public void GrantPermanentID()
    {
        if (hasPermanentID)
        {
            return;
        }

        hasPermanentID = true;
        Debug.Log("[PlayerInventory] Permanent ID granted.");

        // Auto-select if the player had nothing selected.
        if (currentIDCard == IDCardType.None)
        {
            SelectCard(IDCardType.Permanent);
        }
    }

    /// <summary>
    /// Puts the player into the ID problem state. While active, scans fail until resolved.
    /// </summary>
    public void TriggerIDProblem()
    {
        if (hasIDProblem)
        {
            return;
        }

        hasIDProblem = true;
        Debug.Log("[PlayerInventory] ID problem state activated. Scans will fail until resolved by Reception.");
        OnIDProblemChanged?.Invoke(true);
    }

    /// <summary>
    /// Marks the initial tutorial ID failure as triggered and activates the ID problem state.
    /// </summary>
    public void TriggerInitialIDFailure()
    {
        hasTriggeredInitialIDFailure = true;
        TriggerIDProblem();
    }

    /// <summary>
    /// Sets whether the initial tutorial ID failure has occurred.
    /// Used for testing and future backend persistence synchronization.
    /// </summary>
    public void SetTriggeredInitialIDFailure(bool value)
    {
        hasTriggeredInitialIDFailure = value;
    }

    /// <summary>
    /// Clears the active ID problem state. Does not reset HasTriggeredInitialIDFailure.
    /// </summary>
    public void ResolveIDProblem()
    {
        bool wasProblem = hasIDProblem;
        hasIDProblem = false;

        Debug.Log("[PlayerInventory] ID problem resolved. Permanent ID can now be scanned.");
        if (wasProblem)
        {
            OnIDProblemChanged?.Invoke(false);
        }
    }

    /// <summary>
    /// Adds one temporary ID card to the inventory and auto-selects it.
    /// </summary>
    public void AddTemporaryID(bool autoSelect = true)
    {
        temporaryIDCount++;
        Debug.Log($"[PlayerInventory] Temporary ID added. Count: {temporaryIDCount}");

        if (autoSelect || currentIDCard == IDCardType.None)
        {
            SelectCard(IDCardType.Temporary);
        }
    }

    /// <summary>
    /// Consumes one temporary ID card. Returns false if none are available.
    /// Clears any active ID problem state.
    /// </summary>
    public bool ConsumeTemporaryID()
    {
        if (temporaryIDCount <= 0)
        {
            Debug.LogWarning("[PlayerInventory] Tried to consume a temporary ID but none remain.");
            return false;
        }

        temporaryIDCount--;
        Debug.Log($"[PlayerInventory] Temporary ID consumed. Remaining: {temporaryIDCount}");

        // Consuming a temporary ID clears the active ID problem.
        ResolveIDProblem();

        // Auto-select the best available card after consumption.
        if (currentIDCard == IDCardType.Temporary && temporaryIDCount <= 0)
        {
            if (hasPermanentID)
            {
                SelectCard(IDCardType.Permanent);
            }
            else
            {
                SelectCard(IDCardType.None);
            }
        }

        return true;
    }

    /// <summary>
    /// Manually switches the selected card type.
    /// Validates that the player actually holds the requested card.
    /// </summary>
    public void SelectCard(IDCardType type)
    {
        switch (type)
        {
            case IDCardType.Permanent when !hasPermanentID:
                Debug.LogWarning("[PlayerInventory] Cannot select Permanent — player doesn't have one.");
                return;

            case IDCardType.Temporary when temporaryIDCount <= 0:
                Debug.LogWarning("[PlayerInventory] Cannot select Temporary — none in inventory.");
                return;
        }

        if (currentIDCard == type)
        {
            return;
        }

        IDCardType previous = currentIDCard;
        currentIDCard = type;
        Debug.Log($"[PlayerInventory] Card switched: {previous} → {currentIDCard}");
        OnCardChanged?.Invoke(currentIDCard);
    }

    // ── Lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        hasPermanentID = startWithPermanentID;
        temporaryIDCount = 0;
        currentIDCard = hasPermanentID ? IDCardType.Permanent : IDCardType.None;
    }
}
