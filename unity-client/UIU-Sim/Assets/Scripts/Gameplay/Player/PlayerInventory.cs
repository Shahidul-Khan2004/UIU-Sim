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

    [Tooltip("True once the permanent ID has been resolved and works normally forever.")]
    [SerializeField] private bool isPermanentIDResolved;

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

    /// <summary>True while the player is in the ID problem state. Every scan fails until resolved.</summary>
    public bool HasIDProblem => hasIDProblem;

    /// <summary>True once the permanent ID has been resolved and works normally forever.</summary>
    public bool IsPermanentIDResolved => isPermanentIDResolved;

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
    /// Clears the ID problem state and marks the permanent ID as working normally forever.
    /// </summary>
    public void ResolveIDProblem()
    {
        bool wasProblem = hasIDProblem;
        hasIDProblem = false;
        isPermanentIDResolved = true;

        Debug.Log("[PlayerInventory] ID problem resolved. Permanent ID is now valid forever.");
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
    /// Resolves any active ID problem and enables the permanent ID forever.
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

        // After temporary ID is consumed, the ID problem is resolved and permanent ID works normally forever.
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
