using System;
using UIU.Simulator.Gameplay.Player;
using UnityEngine;

namespace UIU.Simulator.Gameplay.Activities
{
    /// <summary>
    /// Minimal current-day activity tracker. Definitions live in Unity Inspector;
    /// resolved outcomes are hydrated from the backend.
    /// GET_ID_CARD is one-time (not reset by Next Day); BREAKFAST resets each day.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DailyActivityState : MonoBehaviour
    {
        public const string BreakfastActivityId = ActivityIds.Breakfast;
        public const string GetIdCardActivityId = ActivityIds.GetIdCard;

        [Header("ID Card Activity")]
        [SerializeField] private string getIdCardTitle = "Get Your ID Card";

        [SerializeField, TextArea]
        private string getIdCardDescription = "Visit the receptionist to receive your university ID card.";

        [Header("Breakfast Activity")]
        [SerializeField] private string breakfastTitle = "Have Breakfast";

        [SerializeField, TextArea]
        private string breakfastDescription = "Go to Neptune in the canteen to get your breakfast.";

        [SerializeField] private string breakfastStallDisplayName = "Neptune";

        private ActivityStatus getIdCardStatus = ActivityStatus.Pending;
        private string getIdCardOutcome = string.Empty;
        private int getIdCardAuraDelta;
        private int getIdCardReputationDelta;

        private ActivityStatus breakfastStatus = ActivityStatus.Pending;
        private string breakfastOutcome = string.Empty;
        private int breakfastAuraDelta;
        private int breakfastReputationDelta;
        private int dayNumber = 1;

        public string GetIdCardTitle => getIdCardTitle;
        public string GetIdCardDescription => getIdCardDescription;
        public ActivityStatus GetIdCardStatus => getIdCardStatus;
        public string GetIdCardOutcome => getIdCardOutcome;
        public int GetIdCardAuraDelta => getIdCardAuraDelta;
        public int GetIdCardReputationDelta => getIdCardReputationDelta;

        public string BreakfastTitle => breakfastTitle;
        public string BreakfastDescription => breakfastDescription;
        public string BreakfastStallDisplayName => breakfastStallDisplayName;
        public ActivityStatus BreakfastStatus => breakfastStatus;
        public string BreakfastOutcome => breakfastOutcome;
        public int BreakfastAuraDelta => breakfastAuraDelta;
        public int BreakfastReputationDelta => breakfastReputationDelta;
        public int DayNumber => dayNumber;

        /// <summary>True once GET_ID_CARD is COMPLETED for this journey.</summary>
        public bool IsGetIdCardResolved =>
            getIdCardStatus == ActivityStatus.Completed || getIdCardStatus == ActivityStatus.Missed;

        /// <summary>True once breakfast is COMPLETED or MISSED for the current day.</summary>
        public bool IsBreakfastResolved =>
            breakfastStatus == ActivityStatus.Completed || breakfastStatus == ActivityStatus.Missed;

        /// <summary>Raised whenever GET_ID_CARD status changes.</summary>
        public event Action OnGetIdCardStatusChanged;

        /// <summary>Raised whenever breakfast status changes.</summary>
        public event Action OnBreakfastStatusChanged;

        /// <summary>Raised when day activity state is reset (New Game / future Next Day).</summary>
        public event Action OnActivitiesReset;

        public ActivityRecord GetIdCardRecord()
        {
            return new ActivityRecord(
                GetIdCardActivityId,
                getIdCardStatus,
                getIdCardOutcome,
                getIdCardAuraDelta,
                getIdCardReputationDelta,
                dayNumber);
        }

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
        /// Resets daily activities to PENDING. Preserves one-time GET_ID_CARD.
        /// Used by future Next Day progression. New Game resets GET_ID_CARD via
        /// <see cref="ResetForNewGame"/>.
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
            Debug.Log($"[DailyActivityState] Reset for day {dayNumber} — breakfast PENDING (GET_ID_CARD preserved).");
        }

        /// <summary>
        /// Full journey reset used by New Game after the server clears activity rows.
        /// </summary>
        public void ResetForNewGame()
        {
            dayNumber = 1;
            getIdCardStatus = ActivityStatus.Pending;
            getIdCardOutcome = string.Empty;
            getIdCardAuraDelta = 0;
            getIdCardReputationDelta = 0;
            breakfastStatus = ActivityStatus.Pending;
            breakfastOutcome = string.Empty;
            breakfastAuraDelta = 0;
            breakfastReputationDelta = 0;
            OnActivitiesReset?.Invoke();
            OnGetIdCardStatusChanged?.Invoke();
            OnBreakfastStatusChanged?.Invoke();
            Debug.Log("[DailyActivityState] New Game reset — GET_ID_CARD and breakfast PENDING.");
        }

        /// <summary>Applies a confirmed server activity row without mutating Aura.</summary>
        public void ApplyServerActivity(ActivityRecord record)
        {
            if (record.ActivityId == GetIdCardActivityId)
            {
                dayNumber = Mathf.Max(1, record.DayNumber);
                getIdCardStatus = record.Status;
                getIdCardOutcome = record.Outcome ?? string.Empty;
                getIdCardAuraDelta = record.AuraDelta;
                getIdCardReputationDelta = record.ReputationDelta;
                OnGetIdCardStatusChanged?.Invoke();
                Debug.Log(
                    $"[DailyActivityState] Applied server GET_ID_CARD: status={getIdCardStatus}, outcome={getIdCardOutcome}");
                return;
            }

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

        /// <summary>Test seam to force GET_ID_CARD status without networking.</summary>
        public void SetGetIdCardStatusForTesting(ActivityStatus status, string outcome = null, int auraDelta = 0)
        {
            getIdCardStatus = status;
            getIdCardOutcome = outcome ?? string.Empty;
            getIdCardAuraDelta = auraDelta;
            OnGetIdCardStatusChanged?.Invoke();
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
