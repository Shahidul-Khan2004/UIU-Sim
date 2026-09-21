using System;
using UIU.Simulator.Gameplay.Classroom;
using UIU.Simulator.Gameplay.Player;
using UnityEngine;

namespace UIU.Simulator.Gameplay.Activities
{
    /// <summary>
    /// Minimal current-day activity tracker. Definitions live in Unity Inspector;
    /// resolved outcomes are hydrated from the backend.
    /// GET_ID_CARD is one-time (not reset by Next Day); BREAKFAST and ATTEND_ICS reset each day.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DailyActivityState : MonoBehaviour
    {
        public const string BreakfastActivityId = ActivityIds.Breakfast;
        public const string GetIdCardActivityId = ActivityIds.GetIdCard;
        public const string AttendIcsActivityId = ActivityIds.AttendIcs;

        [Header("ID Card Activity")]
        [SerializeField] private string getIdCardTitle = "Get Your ID Card";

        [SerializeField, TextArea]
        private string getIdCardDescription = "Visit the receptionist to receive your university ID card.";

        [Header("Breakfast Activity")]
        [SerializeField] private string breakfastTitle = "Have Breakfast";

        [SerializeField, TextArea]
        private string breakfastDescription = "Go to Neptune in the canteen to get your breakfast.";

        [SerializeField] private string breakfastStallDisplayName = "Neptune";

        [Header("ICS Classroom Activity")]
        [Tooltip("Optional scene classroom used to build the objective HUD text.")]
        [SerializeField] private IcsClassroomInteractable icsClassroom;

        private ActivityStatus getIdCardStatus = ActivityStatus.Pending;
        private string getIdCardOutcome = string.Empty;
        private int getIdCardAuraDelta;
        private int getIdCardReputationDelta;

        private ActivityStatus breakfastStatus = ActivityStatus.Pending;
        private string breakfastOutcome = string.Empty;
        private int breakfastAuraDelta;
        private int breakfastReputationDelta;

        private ActivityStatus attendIcsStatus = ActivityStatus.Pending;
        private string attendIcsOutcome = string.Empty;
        private int attendIcsAuraDelta;
        private int attendIcsReputationDelta;
        private int attendIcsMilestoneSeconds;

        private int dayNumber = 1;

        private void Awake()
        {
            if (icsClassroom == null)
            {
                icsClassroom = FindFirstObjectByType<IcsClassroomInteractable>();
            }
        }

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

        public ActivityStatus AttendIcsStatus => attendIcsStatus;
        public string AttendIcsOutcome => attendIcsOutcome;
        public int AttendIcsAuraDelta => attendIcsAuraDelta;
        public int AttendIcsReputationDelta => attendIcsReputationDelta;
        public int AttendIcsMilestoneSeconds => attendIcsMilestoneSeconds;
        public int DayNumber => dayNumber;

        public IcsClassroomInteractable IcsClassroom
        {
            get => icsClassroom;
            set => icsClassroom = value;
        }

        /// <summary>True once GET_ID_CARD is COMPLETED for this journey.</summary>
        public bool IsGetIdCardResolved =>
            getIdCardStatus == ActivityStatus.Completed || getIdCardStatus == ActivityStatus.Missed;

        /// <summary>True once breakfast is COMPLETED or MISSED for the current day.</summary>
        public bool IsBreakfastResolved =>
            breakfastStatus == ActivityStatus.Completed || breakfastStatus == ActivityStatus.Missed;

        /// <summary>True once ATTEND_ICS reached a terminal outcome for the current day.</summary>
        public bool IsAttendIcsResolved =>
            attendIcsStatus == ActivityStatus.Completed || attendIcsStatus == ActivityStatus.Missed;

        public event Action OnGetIdCardStatusChanged;
        public event Action OnBreakfastStatusChanged;
        public event Action OnAttendIcsStatusChanged;
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

        public ActivityRecord GetAttendIcsRecord()
        {
            return new ActivityRecord(
                AttendIcsActivityId,
                attendIcsStatus,
                attendIcsOutcome,
                attendIcsAuraDelta,
                attendIcsReputationDelta,
                dayNumber,
                attendIcsMilestoneSeconds);
        }

        public string BuildAttendIcsObjectiveTitle()
        {
            return "Attend Introduction to Computer Science";
        }

        public string BuildAttendIcsObjectiveDescription()
        {
            if (icsClassroom == null)
            {
                return "ICS classroom is not configured. Assign floor, room number, and classroom interaction in the Inspector.";
            }

            if (!icsClassroom.HasClassroomConfigured)
            {
                return icsClassroom.MissingSetupMessage;
            }

            return icsClassroom.BuildObjectiveDescription();
        }

        /// <summary>
        /// Whether the HUD should show ICS today for this player save.
        /// </summary>
        public bool ShouldShowAttendIcsObjective(PlayerSaveState saveState)
        {
            if (icsClassroom == null)
            {
                return false;
            }

            if (saveState == null || !saveState.HasActiveUniversityDay)
            {
                return false;
            }

            if (!string.Equals(saveState.Role, "STUDENT", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.Equals(saveState.Department, icsClassroom.DepartmentCode, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return saveState.CurrentDay == icsClassroom.ScheduledGameplayDay;
        }

        public void ResetForNewDay(int newDayNumber = 1)
        {
            dayNumber = Mathf.Max(1, newDayNumber);
            breakfastStatus = ActivityStatus.Pending;
            breakfastOutcome = string.Empty;
            breakfastAuraDelta = 0;
            breakfastReputationDelta = 0;
            attendIcsStatus = ActivityStatus.Pending;
            attendIcsOutcome = string.Empty;
            attendIcsAuraDelta = 0;
            attendIcsReputationDelta = 0;
            attendIcsMilestoneSeconds = 0;
            OnActivitiesReset?.Invoke();
            OnBreakfastStatusChanged?.Invoke();
            OnAttendIcsStatusChanged?.Invoke();
            Debug.Log($"[DailyActivityState] Reset for day {dayNumber} — breakfast/ICS PENDING (GET_ID_CARD preserved).");
        }

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
            attendIcsStatus = ActivityStatus.Pending;
            attendIcsOutcome = string.Empty;
            attendIcsAuraDelta = 0;
            attendIcsReputationDelta = 0;
            attendIcsMilestoneSeconds = 0;
            OnActivitiesReset?.Invoke();
            OnGetIdCardStatusChanged?.Invoke();
            OnBreakfastStatusChanged?.Invoke();
            OnAttendIcsStatusChanged?.Invoke();
            Debug.Log("[DailyActivityState] New Game reset — GET_ID_CARD, breakfast, and ICS PENDING.");
        }

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
                return;
            }

            if (record.ActivityId == AttendIcsActivityId)
            {
                dayNumber = Mathf.Max(1, record.DayNumber);
                attendIcsStatus = record.Status == ActivityStatus.Pending && !string.IsNullOrEmpty(record.Outcome)
                    ? ActivityStatus.InProgress
                    : record.Status;
                if (string.Equals(record.Outcome, "ATTENDING", StringComparison.OrdinalIgnoreCase))
                {
                    attendIcsStatus = ActivityStatus.InProgress;
                }

                attendIcsOutcome = record.Outcome ?? string.Empty;
                attendIcsAuraDelta = record.AuraDelta;
                attendIcsReputationDelta = record.ReputationDelta;
                attendIcsMilestoneSeconds = record.MilestoneSeconds;
                OnAttendIcsStatusChanged?.Invoke();
                Debug.Log(
                    $"[DailyActivityState] Applied server ATTEND_ICS: status={attendIcsStatus}, outcome={attendIcsOutcome}, milestone={attendIcsMilestoneSeconds}");
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
        }

        public void SetGetIdCardStatusForTesting(ActivityStatus status, string outcome = null, int auraDelta = 0)
        {
            getIdCardStatus = status;
            getIdCardOutcome = outcome ?? string.Empty;
            getIdCardAuraDelta = auraDelta;
            OnGetIdCardStatusChanged?.Invoke();
        }

        public void SetBreakfastStatusForTesting(ActivityStatus status, string outcome = null, int auraDelta = 0)
        {
            breakfastStatus = status;
            breakfastOutcome = outcome ?? string.Empty;
            breakfastAuraDelta = auraDelta;
            OnBreakfastStatusChanged?.Invoke();
        }

        public void SetAttendIcsStatusForTesting(
            ActivityStatus status,
            string outcome = null,
            int auraDelta = 0,
            int reputationDelta = 0,
            int milestoneSeconds = 0)
        {
            attendIcsStatus = status;
            attendIcsOutcome = outcome ?? string.Empty;
            attendIcsAuraDelta = auraDelta;
            attendIcsReputationDelta = reputationDelta;
            attendIcsMilestoneSeconds = milestoneSeconds;
            OnAttendIcsStatusChanged?.Invoke();
        }

        public bool HasPendingBreakfastForDayAdvance()
        {
            return breakfastStatus == ActivityStatus.Pending;
        }

        public void SetDayNumber(int newDayNumber)
        {
            dayNumber = Mathf.Max(1, newDayNumber);
            OnActivitiesReset?.Invoke();
        }
    }
}
