using System;
using UnityEngine;

/// <summary>
/// Tracks the player's daily campus activities and event completions.
/// Attach to the Player prefab root alongside <see cref="PlayerStats"/> and <see cref="PlayerInventory"/>.
/// <para>
/// Provides <see cref="BeginCampusDay"/> to reset daily states at the start of a university day,
/// and mutation methods like <see cref="CompleteBreakfastEvent"/> to record progress.
/// External systems cannot directly set the state fields.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class CampusDayState : MonoBehaviour
{
    [Header("Daily Event States")]
    [Tooltip("True once the player has resolved today's breakfast event (eaten, completed queue, or skipped).")]
    [SerializeField] private bool hasCompletedBreakfastEvent;

    // ── Public API — Queries ───────────────────────────────────────────

    /// <summary>
    /// True once the player has resolved today's breakfast event
    /// (by having Rice, waiting for Porotta, skipping breakfast, or skipping the line).
    /// </summary>
    public bool HasCompletedBreakfastEvent => hasCompletedBreakfastEvent;

    /// <summary>
    /// Raised whenever <see cref="HasCompletedBreakfastEvent"/> changes.
    /// Payload is the new boolean state.
    /// </summary>
    public event Action<bool> OnBreakfastEventCompletedChanged;

    /// <summary>
    /// Raised when <see cref="BeginCampusDay"/> is called.
    /// Future systems (e.g. attendance, daily quests) can subscribe to this.
    /// </summary>
    public event Action OnDayStarted;

    // ── Public API — Mutations ─────────────────────────────────────────

    /// <summary>
    /// Resets daily event progression for a new university day.
    /// Idempotent and safe to call when a new day/session begins.
    /// </summary>
    public void BeginCampusDay()
    {
        bool wasCompleted = hasCompletedBreakfastEvent;
        hasCompletedBreakfastEvent = false;

        Debug.Log("[CampusDayState] BeginCampusDay called — daily event states reset.");

        if (wasCompleted)
        {
            OnBreakfastEventCompletedChanged?.Invoke(false);
        }

        OnDayStarted?.Invoke();
    }

    /// <summary>
    /// Records the daily breakfast event as completed.
    /// Called by the canteen breakfast counter upon resolving any terminal outcome.
    /// </summary>
    public void CompleteBreakfastEvent()
    {
        if (hasCompletedBreakfastEvent)
        {
            return;
        }

        hasCompletedBreakfastEvent = true;
        Debug.Log("[CampusDayState] Breakfast event completed for today.");
        OnBreakfastEventCompletedChanged?.Invoke(true);
    }

    // ── Lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        // MVP: Initialize daily state when the player object is instantiated.
        BeginCampusDay();
    }
}
