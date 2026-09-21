using System;
using UnityEngine;
using UIU.Simulator.Gameplay.Activities;

/// <summary>
/// Tracks the player's daily campus activities and event completions.
/// Attach to the Player prefab root alongside <see cref="PlayerStats"/> and <see cref="PlayerInventory"/>.
/// <para>
/// Provides <see cref="BeginCampusDay"/> to reset daily states at the start of a university day,
/// and mutation methods like <see cref="CompleteBreakfastEvent"/> to record progress.
/// External systems cannot directly set the state fields.
/// </para>
/// Breakfast resolution is authoritative in <see cref="DailyActivityState"/> when present;
/// this component keeps a compatible gate for canteen interaction.
/// </summary>
[DisallowMultipleComponent]
public sealed class CampusDayState : MonoBehaviour
{
    [Header("Daily Event States")]
    [Tooltip("True once the player has resolved today's breakfast event (eaten, completed queue, or skipped).")]
    [SerializeField] private bool hasCompletedBreakfastEvent;

    private DailyActivityState dailyActivityState;

    // ── Public API — Queries ───────────────────────────────────────────

    /// <summary>
    /// True once the player has resolved today's breakfast event
    /// (COMPLETED or MISSED — including skip breakfast).
    /// </summary>
    public bool HasCompletedBreakfastEvent
    {
        get
        {
            if (dailyActivityState != null)
            {
                return dailyActivityState.IsBreakfastResolved;
            }

            return hasCompletedBreakfastEvent;
        }
    }

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
        bool wasCompleted = HasCompletedBreakfastEvent;
        hasCompletedBreakfastEvent = false;

        if (dailyActivityState == null)
        {
            dailyActivityState = GetComponent<DailyActivityState>();
        }

        dailyActivityState?.ResetForNewDay();

        Debug.Log("[CampusDayState] BeginCampusDay called — daily event states reset.");

        if (wasCompleted)
        {
            OnBreakfastEventCompletedChanged?.Invoke(false);
        }

        OnDayStarted?.Invoke();
    }

    /// <summary>
    /// Records the daily breakfast event as resolved (COMPLETED or MISSED).
    /// Called only after a successful backend activity resolve (or test seam).
    /// </summary>
    public void CompleteBreakfastEvent()
    {
        if (hasCompletedBreakfastEvent)
        {
            return;
        }

        hasCompletedBreakfastEvent = true;
        Debug.Log("[CampusDayState] Breakfast event resolved for today.");
        OnBreakfastEventCompletedChanged?.Invoke(true);
    }

    /// <summary>
    /// Applies a hydrated server breakfast resolution without networking.
    /// Does not reset other day state.
    /// </summary>
    public void ApplyHydratedBreakfastResolved()
    {
        if (HasCompletedBreakfastEvent)
        {
            return;
        }

        hasCompletedBreakfastEvent = true;
        OnBreakfastEventCompletedChanged?.Invoke(true);
    }

    // ── Lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        dailyActivityState = GetComponent<DailyActivityState>();
        // Local day gate starts pending. Server hydration may mark breakfast resolved after Continue.
        // Do NOT wipe persisted activity state here — respawn/Continue must keep outcomes.
        hasCompletedBreakfastEvent = dailyActivityState != null && dailyActivityState.IsBreakfastResolved;
    }
}
