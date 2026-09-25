using System;
using System.Collections.Generic;
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
        [Tooltip(
            "Persistent ICS classroom location used by the HUD even when Floor04 is unloaded. " +
            "Assign Assets/Data/Gameplay/IcsClassroomLocation.asset.")]
        [SerializeField] private IcsClassroomLocationConfig icsLocationConfig;

        [Tooltip("Optional loaded-floor classroom. Not required for HUD location text.")]
        [SerializeField] private IcsClassroomInteractable icsClassroom;

        [Header("Additional CSE Classrooms")]
        [Tooltip(
            "English and Discrete Mathematics location assets. The HUD reads these even when " +
            "Floor07 or Floor04 is unloaded. Order is the objective order after ICS.")]
        [SerializeField] private IcsClassroomLocationConfig[] additionalClassroomLocations;

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

        private ActivityStatus libraryStudyStatus = ActivityStatus.Pending;
        private string libraryStudyOutcome = string.Empty;
        private int libraryStudyAuraDelta;
        private int libraryStudyReputationDelta;

        private readonly Dictionary<string, ClassroomRuntime> extraClassrooms = new Dictionary<string, ClassroomRuntime>();

        private int dayNumber = 1;

        // Cached from the floor scene classroom so the HUD stays correct when that
        // additive floor unloads (Unity destroyed references become fake-null).
        private bool hasCachedClassroomConfig;
        private int cachedScheduledDay = 1;
        private string cachedDepartmentCode = "CSE";
        private string cachedObjectiveDescription = string.Empty;

        private void Awake()
        {
            ResolveClassroomReference();
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

        public ActivityStatus LibraryStudyStatus => libraryStudyStatus;
        public string LibraryStudyOutcome => libraryStudyOutcome;
        public int LibraryStudyReputationDelta => libraryStudyReputationDelta;
        public bool IsLibraryStudyCompleted => libraryStudyStatus == ActivityStatus.Completed;

        public IcsClassroomInteractable IcsClassroom
        {
            get
            {
                ResolveClassroomReference();
                return icsClassroom;
            }
            set
            {
                icsClassroom = value;
                if (value != null)
                {
                    CacheClassroomConfig(value);
                    OnAttendIcsStatusChanged?.Invoke();
                }
            }
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
        public event Action OnLibraryStudyStatusChanged;
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
            ResolveClassroomReference();

            if (icsClassroom != null && icsClassroom.HasClassroomConfigured)
            {
                string live = icsClassroom.BuildObjectiveDescription();
                cachedObjectiveDescription = live;
                return live;
            }

            if (icsLocationConfig != null && icsLocationConfig.IsConfigured)
            {
                cachedObjectiveDescription = icsLocationConfig.BuildObjectiveDescription();
                return cachedObjectiveDescription;
            }

            if (!string.IsNullOrEmpty(cachedObjectiveDescription))
            {
                return cachedObjectiveDescription;
            }

            if (icsClassroom != null)
            {
                return icsClassroom.MissingSetupMessage;
            }

            return "ICS classroom is not configured. Assign floor, room number, and classroom interaction in the Inspector.";
        }

        /// <summary>
        /// Whether the HUD should show ICS today for this player save.
        /// Uses a cached classroom schedule so the objective survives floor unload.
        /// </summary>
        public bool ShouldShowAttendIcsObjective(PlayerSaveState saveState)
        {
            if (saveState == null || !saveState.HasActiveUniversityDay)
            {
                return false;
            }

            if (!string.Equals(saveState.Role, "STUDENT", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            ResolveClassroomReference();

            string departmentCode = ResolveIcsDepartmentCode();
            int scheduledDay = ResolveIcsScheduledDay();

            if (!string.Equals(saveState.Department, departmentCode, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return saveState.CurrentDay == scheduledDay;
        }

        /// <summary>
        /// Called by floor classroom objects when they load so the HUD can bind without
        /// requiring the classroom scene to stay loaded.
        /// </summary>
        public void RegisterClassroom(IcsClassroomInteractable classroom)
        {
            if (classroom == null)
            {
                return;
            }

            if (classroom.IsIcsCourse)
            {
                IcsClassroom = classroom;
                return;
            }

            OnAttendIcsStatusChanged?.Invoke();
        }

        public bool IsClassroomResolved(string activityId)
        {
            if (string.IsNullOrEmpty(activityId) || activityId == AttendIcsActivityId)
            {
                return IsAttendIcsResolved;
            }

            ClassroomRuntime runtime = GetExtra(activityId);
            return runtime.Status == ActivityStatus.Completed || runtime.Status == ActivityStatus.Missed;
        }

        public string GetClassroomOutcome(string activityId)
        {
            if (string.IsNullOrEmpty(activityId) || activityId == AttendIcsActivityId)
            {
                return attendIcsOutcome;
            }

            return GetExtra(activityId).Outcome;
        }

        public string CourseNameForActivity(string activityId)
        {
            IcsClassroomLocationConfig config = FindLocationConfig(activityId);
            if (config != null && !string.IsNullOrWhiteSpace(config.CourseName))
            {
                return config.CourseName.Trim();
            }

            if (activityId == ActivityIds.AttendEnglish)
            {
                return "English";
            }

            if (activityId == ActivityIds.AttendDm)
            {
                return "Discrete Mathematics";
            }

            return "Introduction to Computer Science";
        }

        public ClassroomObjectiveView[] BuildAdditionalClassroomObjectives(PlayerSaveState saveState)
        {
            if (additionalClassroomLocations == null || additionalClassroomLocations.Length == 0)
            {
                return Array.Empty<ClassroomObjectiveView>();
            }

            var views = new List<ClassroomObjectiveView>(additionalClassroomLocations.Length);
            for (int i = 0; i < additionalClassroomLocations.Length; i++)
            {
                IcsClassroomLocationConfig config = additionalClassroomLocations[i];
                if (config == null || !ShouldShowConfiguredClassroom(saveState, config))
                {
                    continue;
                }

                ClassroomRuntime runtime = GetExtra(config.ActivityId);
                string description = config.IsConfigured
                    ? config.BuildObjectiveDescription()
                    : "Classroom location is not configured.";
                views.Add(new ClassroomObjectiveView(
                    config.ActivityId,
                    config.BuildObjectiveTitle(),
                    description,
                    runtime.Status,
                    runtime.Outcome));
            }

            return views.ToArray();
        }

        public void SetAdditionalLocationConfigsForTesting(IcsClassroomLocationConfig[] configs)
        {
            additionalClassroomLocations = configs;
            OnAttendIcsStatusChanged?.Invoke();
        }

        public void SetClassroomStatusForTesting(
            string activityId,
            ActivityStatus status,
            string outcome = null,
            int auraDelta = 0,
            int reputationDelta = 0,
            int milestoneSeconds = 0)
        {
            if (activityId == AttendIcsActivityId)
            {
                SetAttendIcsStatusForTesting(status, outcome, auraDelta, reputationDelta, milestoneSeconds);
                return;
            }

            ClassroomRuntime runtime = GetExtra(activityId);
            runtime.Status = status;
            runtime.Outcome = outcome ?? string.Empty;
            runtime.AuraDelta = auraDelta;
            runtime.ReputationDelta = reputationDelta;
            runtime.MilestoneSeconds = milestoneSeconds;
            OnAttendIcsStatusChanged?.Invoke();
        }

        /// <summary>
        /// After hydration, classroom rows missing from the current-day payload return to pending.
        /// </summary>
        public void ResetAbsentClassrooms(ICollection<string> presentActivityIds)
        {
            presentActivityIds ??= Array.Empty<string>();
            if (!ContainsActivity(presentActivityIds, AttendIcsActivityId)
                && attendIcsStatus != ActivityStatus.Pending)
            {
                attendIcsStatus = ActivityStatus.Pending;
                attendIcsOutcome = string.Empty;
                attendIcsAuraDelta = 0;
                attendIcsReputationDelta = 0;
                attendIcsMilestoneSeconds = 0;
            }

            if (extraClassrooms.Count > 0)
            {
                var keys = new List<string>(extraClassrooms.Keys);
                for (int i = 0; i < keys.Count; i++)
                {
                    if (!ContainsActivity(presentActivityIds, keys[i]))
                    {
                        extraClassrooms[keys[i]] = new ClassroomRuntime();
                    }
                }
            }

            OnAttendIcsStatusChanged?.Invoke();
        }

        public void SetLocationConfigForTesting(IcsClassroomLocationConfig config)
        {
            icsLocationConfig = config;
            OnAttendIcsStatusChanged?.Invoke();
        }

        private void ResolveClassroomReference()
        {
            // Destroyed Unity objects compare equal to null.
            if (icsClassroom != null)
            {
                CacheClassroomConfig(icsClassroom);
                return;
            }

            IcsClassroomInteractable[] loaded =
                FindObjectsByType<IcsClassroomInteractable>(FindObjectsSortMode.None);
            for (int i = 0; i < loaded.Length; i++)
            {
                if (loaded[i] != null && loaded[i].IsIcsCourse)
                {
                    icsClassroom = loaded[i];
                    CacheClassroomConfig(icsClassroom);
                    return;
                }
            }
        }

        private void CacheClassroomConfig(IcsClassroomInteractable classroom)
        {
            if (classroom == null)
            {
                return;
            }

            cachedScheduledDay = Mathf.Max(1, classroom.ScheduledGameplayDay);
            cachedDepartmentCode = string.IsNullOrWhiteSpace(classroom.DepartmentCode)
                ? "CSE"
                : classroom.DepartmentCode.Trim();
            hasCachedClassroomConfig = true;

            if (classroom.HasClassroomConfigured)
            {
                cachedObjectiveDescription = classroom.BuildObjectiveDescription();
            }

            WarnIfLiveClassroomDiffersFromPersistentConfig(classroom);
        }

        private string ResolveIcsDepartmentCode()
        {
            if (icsClassroom != null && !string.IsNullOrWhiteSpace(icsClassroom.DepartmentCode))
            {
                return icsClassroom.DepartmentCode;
            }

            if (icsLocationConfig != null && !string.IsNullOrWhiteSpace(icsLocationConfig.DepartmentCode))
            {
                return icsLocationConfig.DepartmentCode;
            }

            if (hasCachedClassroomConfig && !string.IsNullOrWhiteSpace(cachedDepartmentCode))
            {
                return cachedDepartmentCode;
            }

            // Match AttendIcsDefinition.REQUIRED_DEPARTMENT_CODE so Day 1 CSE students
            // still see the objective before Floor04 has loaded.
            return "CSE";
        }

        private int ResolveIcsScheduledDay()
        {
            if (icsClassroom != null)
            {
                return Mathf.Max(1, icsClassroom.ScheduledGameplayDay);
            }

            if (icsLocationConfig != null)
            {
                return icsLocationConfig.ScheduledGameplayDay;
            }

            if (hasCachedClassroomConfig)
            {
                return Mathf.Max(1, cachedScheduledDay);
            }

            // Match AttendIcsDefinition.SCHEDULED_DAY.
            return 1;
        }

        private void WarnIfLiveClassroomDiffersFromPersistentConfig(IcsClassroomInteractable classroom)
        {
            if (icsLocationConfig == null
                || !icsLocationConfig.IsConfigured
                || classroom == null
                || !classroom.HasClassroomConfigured)
            {
                return;
            }

            bool roomMatches = string.Equals(
                classroom.ClassroomNumber.Trim(),
                icsLocationConfig.ClassroomNumber.Trim(),
                StringComparison.Ordinal);
            if (roomMatches && classroom.Floor == icsLocationConfig.Floor)
            {
                return;
            }

            Debug.LogWarning(
                $"[DailyActivityState] Loaded ICS classroom (Room {classroom.ClassroomNumber}, Floor {classroom.Floor}) " +
                $"differs from persistent location config (Room {icsLocationConfig.ClassroomNumber}, Floor {icsLocationConfig.Floor}). " +
                "Update Assets/Data/Gameplay/IcsClassroomLocation.asset so the HUD and classroom stay aligned.",
                this);
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
            extraClassrooms.Clear();
            ClearLibraryStudyFields();
            OnActivitiesReset?.Invoke();
            OnBreakfastStatusChanged?.Invoke();
            OnAttendIcsStatusChanged?.Invoke();
            OnLibraryStudyStatusChanged?.Invoke();
            Debug.Log($"[DailyActivityState] Reset for day {dayNumber} — breakfast and classrooms PENDING (GET_ID_CARD preserved).");
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
            extraClassrooms.Clear();
            ClearLibraryStudyFields();
            OnActivitiesReset?.Invoke();
            OnGetIdCardStatusChanged?.Invoke();
            OnBreakfastStatusChanged?.Invoke();
            OnAttendIcsStatusChanged?.Invoke();
            OnLibraryStudyStatusChanged?.Invoke();
            Debug.Log("[DailyActivityState] New Game reset — GET_ID_CARD, breakfast, and classrooms PENDING.");
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
                attendIcsStatus = NormalizeClassroomStatus(record.Status, record.Outcome);
                attendIcsOutcome = record.Outcome ?? string.Empty;
                attendIcsAuraDelta = record.AuraDelta;
                attendIcsReputationDelta = record.ReputationDelta;
                attendIcsMilestoneSeconds = record.MilestoneSeconds;
                OnAttendIcsStatusChanged?.Invoke();
                Debug.Log(
                    $"[DailyActivityState] Applied server ATTEND_ICS: status={attendIcsStatus}, outcome={attendIcsOutcome}, milestone={attendIcsMilestoneSeconds}");
                return;
            }

            if (ActivityIds.IsClassroomActivity(record.ActivityId))
            {
                dayNumber = Mathf.Max(1, record.DayNumber);
                ClassroomRuntime runtime = GetExtra(record.ActivityId);
                runtime.Status = NormalizeClassroomStatus(record.Status, record.Outcome);
                runtime.Outcome = record.Outcome ?? string.Empty;
                runtime.AuraDelta = record.AuraDelta;
                runtime.ReputationDelta = record.ReputationDelta;
                runtime.MilestoneSeconds = record.MilestoneSeconds;
                OnAttendIcsStatusChanged?.Invoke();
                Debug.Log(
                    $"[DailyActivityState] Applied server {record.ActivityId}: status={runtime.Status}, outcome={runtime.Outcome}, milestone={runtime.MilestoneSeconds}");
                return;
            }

            if (record.ActivityId == ActivityIds.LibraryStudy)
            {
                dayNumber = Mathf.Max(1, record.DayNumber);
                libraryStudyStatus = record.Status == ActivityStatus.Completed
                    ? ActivityStatus.Completed
                    : record.Status == ActivityStatus.InProgress
                        ? ActivityStatus.InProgress
                        : ActivityStatus.Pending;
                libraryStudyOutcome = record.Outcome ?? string.Empty;
                libraryStudyAuraDelta = record.AuraDelta;
                libraryStudyReputationDelta = record.ReputationDelta;
                OnLibraryStudyStatusChanged?.Invoke();
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

        public void SetLibraryStudyStatusForTesting(
            ActivityStatus status,
            string outcome = null,
            int reputationDelta = 0)
        {
            libraryStudyStatus = status;
            libraryStudyOutcome = outcome ?? string.Empty;
            libraryStudyAuraDelta = 0;
            libraryStudyReputationDelta = reputationDelta;
            OnLibraryStudyStatusChanged?.Invoke();
        }

        /// <summary>
        /// Current-day payload omitted Library Study, so today's attempt is available again.
        /// </summary>
        public void ClearLibraryStudy()
        {
            if (libraryStudyStatus == ActivityStatus.Pending
                && string.IsNullOrEmpty(libraryStudyOutcome)
                && libraryStudyReputationDelta == 0)
            {
                return;
            }

            ClearLibraryStudyFields();
            OnLibraryStudyStatusChanged?.Invoke();
        }

        private void ClearLibraryStudyFields()
        {
            libraryStudyStatus = ActivityStatus.Pending;
            libraryStudyOutcome = string.Empty;
            libraryStudyAuraDelta = 0;
            libraryStudyReputationDelta = 0;
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

        private bool ShouldShowConfiguredClassroom(PlayerSaveState saveState, IcsClassroomLocationConfig config)
        {
            if (saveState == null || config == null || !saveState.HasActiveUniversityDay)
            {
                return false;
            }

            if (!string.Equals(saveState.Role, config.StudentRole, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.Equals(saveState.Department, config.DepartmentCode, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return saveState.CurrentDay == config.ScheduledGameplayDay;
        }

        private IcsClassroomLocationConfig FindLocationConfig(string activityId)
        {
            if (icsLocationConfig != null
                && string.Equals(icsLocationConfig.ActivityId, activityId, StringComparison.Ordinal))
            {
                return icsLocationConfig;
            }

            if (additionalClassroomLocations == null)
            {
                return null;
            }

            for (int i = 0; i < additionalClassroomLocations.Length; i++)
            {
                IcsClassroomLocationConfig config = additionalClassroomLocations[i];
                if (config != null && string.Equals(config.ActivityId, activityId, StringComparison.Ordinal))
                {
                    return config;
                }
            }

            return null;
        }

        private ClassroomRuntime GetExtra(string activityId)
        {
            if (!extraClassrooms.TryGetValue(activityId, out ClassroomRuntime runtime))
            {
                runtime = new ClassroomRuntime();
                extraClassrooms[activityId] = runtime;
            }

            return runtime;
        }

        private static ActivityStatus NormalizeClassroomStatus(ActivityStatus parsed, string outcome)
        {
            if (string.Equals(outcome, "LEFT_EARLY", StringComparison.OrdinalIgnoreCase)
                || string.Equals(outcome, "SKIPPED", StringComparison.OrdinalIgnoreCase))
            {
                return ActivityStatus.Missed;
            }

            if (string.Equals(outcome, "COMPLETED", StringComparison.OrdinalIgnoreCase)
                || string.Equals(outcome, "PROXY", StringComparison.OrdinalIgnoreCase))
            {
                return ActivityStatus.Completed;
            }

            if (string.Equals(outcome, "ATTENDING", StringComparison.OrdinalIgnoreCase)
                || parsed == ActivityStatus.InProgress)
            {
                return ActivityStatus.InProgress;
            }

            if (parsed == ActivityStatus.Pending && !string.IsNullOrEmpty(outcome))
            {
                return ActivityStatus.InProgress;
            }

            return parsed;
        }

        private static bool ContainsActivity(ICollection<string> activityIds, string activityId)
        {
            foreach (string id in activityIds)
            {
                if (id == activityId)
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class ClassroomRuntime
        {
            public ActivityStatus Status = ActivityStatus.Pending;
            public string Outcome = string.Empty;
            public int AuraDelta;
            public int ReputationDelta;
            public int MilestoneSeconds;
        }
    }

    public readonly struct ClassroomObjectiveView
    {
        public ClassroomObjectiveView(
            string activityId,
            string title,
            string description,
            ActivityStatus status,
            string outcome)
        {
            ActivityId = activityId ?? string.Empty;
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            Status = status;
            Outcome = outcome ?? string.Empty;
        }

        public string ActivityId { get; }
        public string Title { get; }
        public string Description { get; }
        public ActivityStatus Status { get; }
        public string Outcome { get; }
    }
}
