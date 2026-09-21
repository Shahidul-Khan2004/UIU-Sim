using System;
using UIU.Simulator.Gameplay.Player;
using UnityEngine;

namespace UIU.Simulator.Gameplay.Activities
{
    /// <summary>
    /// Minimal current-day activity tracker. Definitions live in Unity Inspector;
    /// resolved outcomes are hydrated from the backend.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DailyActivityState : MonoBehaviour
    {
        public const string BreakfastActivityId = ActivityIds.Breakfast;

        [Header("Breakfast Activity")]
        [SerializeField] private string breakfastTitle = "Have Breakfast";

        [SerializeField, TextArea]
        private string breakfastDescription = "Go to Neptune in the canteen to get your breakfast.";

        [SerializeField] private string breakfastStallDisplayName = "Neptune";

        private ActivityStatus breakfastStatus = ActivityStatus.Pending;
        private string breakfastOutcome = string.Empty;
        private int breakfastAuraDelta;
        private int breakfastReputationDelta;
        private int dayNumber = 1;

        public string BreakfastTitle => breakfastTitle;
        public string BreakfastDescription => breakfastDescription;
        public string BreakfastStallDisplayName => breakfastStallDisplayName;
        public ActivityStatus BreakfastStatus => breakfastStatus;
        public string BreakfastOutcome => breakfastOutcome;
        public int BreakfastAuraDelta => breakfastAuraDelta;
        public int BreakfastReputationDelta => breakfastReputationDelta;
        public int DayNumber => dayNumber;

        /// <summary>True once breakfast is COMPLETED or MISSED for the current day.</summary>
        public bool IsBreakfastResolved =>
            breakfastStatus == ActivityStatus.Completed || breakfastStatus == ActivityStatus.Missed;

        /// <summary>Raised whenever breakfast status changes.</summary>
        public event Action OnBreakfastStatusChanged;

        /// <summary>Raised when day activity state is reset (New Game / future Next Day).</summary>
        public event Action OnActivitiesReset;

        public ActivityRecord GetBreakfastRecord()
        {
            return new ActivityRecord(
                BreakfastActivityId,
                breakfastStatus,
                breakfastOutcome,
                breakfastAuraDelta,
                breakfastReputationDelta,
                dayNumber);
        }

        /// <summary>
        /// Resets all current-day activities to PENDING.
        /// Used by New Game and reserved for future Next Day progression.
        /// </summary>
        public void ResetForNewDay(int newDayNumber = 1)
        {
            dayNumber = Mathf.Max(1, newDayNumber);
            breakfastStatus = ActivityStatus.Pending;
            breakfastOutcome = string.Empty;
            breakfastAuraDelta = 0;
            breakfastReputationDelta = 0;
            OnActivitiesReset?.Invoke();
            OnBreakfastStatusChanged?.Invoke();
            Debug.Log($"[DailyActivityState] Reset for day {dayNumber} — breakfast PENDING.");
        }

        /// <summary>Applies a confirmed server activity row without mutating Aura.</summary>
        public void ApplyServerActivity(ActivityRecord record)
        {
            if (record.ActivityId != BreakfastActivityId)
            {
                return;
            }

            dayNumber = Mathf.Max(1, record.DayNumber);
            breakfastStatus = record.Status;
            breakfastOutcome = record.Outcome ?? string.Empty;
            breakfastAuraDelta = record.AuraDelta;
            breakfastReputationDelta = record.ReputationDelta;
            OnBreakfastStatusChanged?.Invoke();
            Debug.Log(
                $"[DailyActivityState] Applied server breakfast: status={breakfastStatus}, outcome={breakfastOutcome}");
        }

        /// <summary>Test seam to force breakfast status without networking.</summary>
        public void SetBreakfastStatusForTesting(ActivityStatus status, string outcome = null, int auraDelta = 0)
        {
            breakfastStatus = status;
            breakfastOutcome = outcome ?? string.Empty;
            breakfastAuraDelta = auraDelta;
            OnBreakfastStatusChanged?.Invoke();
        }

        /// <summary>
        /// Future Next Day helper: detect pending breakfast so it can be resolved as MISSED (−5 Aura)
        /// before advancing the day. Not invoked in Phase 2A.
        /// </summary>
        public bool HasPendingBreakfastForDayAdvance()
        {
            return breakfastStatus == ActivityStatus.Pending;
        }
    }
}
