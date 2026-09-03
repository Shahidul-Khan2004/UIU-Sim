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

    /// <summary>Raised when the selected card type changes. Payload is the new type.</summary>
    public event Action<IDCardType> OnCardChanged;

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
    /// Adds one temporary ID card to the inventory.
    /// </summary>
    public void AddTemporaryID()
    {
        temporaryIDCount++;
        Debug.Log($"[PlayerInventory] Temporary ID added. Count: {temporaryIDCount}");

        // Auto-select if the player had nothing selected.
        if (currentIDCard == IDCardType.None)
        {
            SelectCard(IDCardType.Temporary);
        }
    }

    /// <summary>
    /// Consumes one temporary ID card. Returns false if none are available.
    /// After consumption, automatically selects the best available card.
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
