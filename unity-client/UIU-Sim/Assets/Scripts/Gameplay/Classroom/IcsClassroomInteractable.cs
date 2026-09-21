using System;
using UIU.Simulator.Gameplay.Activities;
using UIU.Simulator.Gameplay.Player;
using UIU.Simulator.Gameplay.UI;
using UnityEngine;

namespace UIU.Simulator.Gameplay.Classroom
{
    /// <summary>
    /// ICS classroom entrance on the floor scene. Location for the HUD lives on
    /// <see cref="IcsClassroomLocationConfig"/> so it is available while this floor is unloaded.
    /// Assign the same location asset here so interaction copy stays aligned.
    /// Local Inspector fields remain as fallback. Modal input ownership lives on
    /// ClassroomChoiceUI / ClassroomLectureUI.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class IcsClassroomInteractable : MonoBehaviour, IInteractable
    {
        [Header("Persistent Location")]
        [Tooltip(
            "Shared HUD/interaction location asset. When assigned, course identity and classroom " +
            "location come from this asset. Local Inspector fields below remain as fallback.")]
        [SerializeField] private IcsClassroomLocationConfig locationConfig;

        [Header("Course Identity")]
        [SerializeField] private string activityId = "ATTEND_ICS";
        [SerializeField] private string courseId = "ICS";
        [SerializeField] private string courseName = "Introduction to Computer Science";
        [SerializeField] private string departmentCode = "CSE";
        [SerializeField] private string studentRole = "STUDENT";

        [Header("Classroom Location (Inspector)")]
        [Tooltip("Classroom number shown in objectives and dialogue. Leave blank until configured.")]
        [SerializeField] private string classroomNumber = "";

        [Tooltip("Floor the classroom is on. Must match FloorSceneLoader floor indices (0=Ground, 1=Floor01, ...).")]
        [SerializeField] private int floor = -1;

        [Tooltip("Optional explicit interaction target. Defaults to this GameObject.")]
        [SerializeField] private GameObject classroomInteractionTarget;

        [Header("Schedule")]
        [Tooltip("Must match backend AttendIcsDefinition.SCHEDULED_DAY (default 1).")]
        [SerializeField] private int scheduledGameplayDay = 1;

        [SerializeField] private string displayedClassTime = "09:00–10:30";

        [Header("Lecture Timing")]
        [Tooltip("Real-time lecture duration in seconds. Default 90s = 90 displayed minutes.")]
        [SerializeField, Min(1f)] private float lectureDurationSeconds = 90f;

        [Header("Interaction Copy")]
        [SerializeField] private string prompt = "Enter Classroom";
        [SerializeField, TextArea] private string alreadyResolvedMessage = "You already resolved this class today.";
        [SerializeField, TextArea] private string ineligibleMessage = "This class is only for CSE students on the scheduled day.";
        [SerializeField, TextArea] private string missingSetupMessage =
            "ICS classroom setup is incomplete. Set Classroom Number and Floor in the Inspector on IcsClassroomInteractable.";

        private IAttendIcsProgressSync attendIcsSync;
        private bool isBusy;

        private void OnEnable()
        {
            DailyActivityState activities = ActiveDailyActivityState();
            activities?.RegisterClassroom(this);
        }

        public string ActivityId => ResolveText(locationConfig != null ? locationConfig.ActivityId : null, activityId);
        public bool IsIcsCourse => string.Equals(ActivityId, ActivityIds.AttendIcs, StringComparison.Ordinal);
        public string CourseId => ResolveText(locationConfig != null ? locationConfig.CourseId : null, courseId);
        public string CourseName => ResolveText(locationConfig != null ? locationConfig.CourseName : null, courseName);
        public string DepartmentCode => ResolveText(locationConfig != null ? locationConfig.DepartmentCode : null, departmentCode);
        public string StudentRole => ResolveText(locationConfig != null ? locationConfig.StudentRole : null, studentRole);
        public string ClassroomNumber => ResolveText(locationConfig != null ? locationConfig.ClassroomNumber : null, classroomNumber);
        public int Floor => locationConfig != null && locationConfig.IsConfigured
            ? locationConfig.Floor
            : floor;
        public GameObject ClassroomInteractionTarget =>
            classroomInteractionTarget != null ? classroomInteractionTarget : gameObject;
        public int ScheduledGameplayDay =>
            locationConfig != null ? locationConfig.ScheduledGameplayDay : scheduledGameplayDay;
        public string DisplayedClassTime => displayedClassTime;
        public float LectureDurationSeconds => lectureDurationSeconds;

        public bool HasClassroomConfigured =>
            !string.IsNullOrWhiteSpace(ClassroomNumber) && Floor >= 0;

        public string MissingSetupMessage => missingSetupMessage;

        public string BuildObjectiveDescription()
        {
            if (!HasClassroomConfigured)
            {
                return missingSetupMessage;
            }

            return $"Go to Room {ClassroomNumber.Trim()} on Floor {Floor} to attend {CourseName}.";
        }

        public void SetAttendIcsSyncForTesting(IAttendIcsProgressSync sync)
        {
            attendIcsSync = sync;
        }

        public string InteractionPrompt =>
            HasClassroomConfigured ? $"{prompt} (Room {ClassroomNumber.Trim()})" : prompt;

        public string Interact()
        {
            if (isBusy || DialogueUI.IsOpen || ClassroomLectureUI.IsOpen || ClassroomChoiceUI.IsOpen)
            {
                return null;
            }

            if (!HasClassroomConfigured)
            {
                return missingSetupMessage;
            }

            PlayerSaveState save = PlayerSaveState.Instance ?? FindFirstObjectByType<PlayerSaveState>();
            DailyActivityState activities = ActiveDailyActivityState();

            if (save == null || !save.HasActiveUniversityDay)
            {
                return "Complete admission before attending class.";
            }

            if (!IsEligible(save))
            {
                return ineligibleMessage;
            }

            if (activities != null && activities.IsClassroomResolved(ActivityId))
            {
                string outcome = activities.GetClassroomOutcome(ActivityId);
                if (string.Equals(outcome, "COMPLETED", StringComparison.OrdinalIgnoreCase))
                {
                    return $"You already completed {CourseName} today.";
                }

                if (string.Equals(outcome, "PROXY", StringComparison.OrdinalIgnoreCase))
                {
                    return "You already punched ID and left for this class today.";
                }

                if (string.Equals(outcome, "LEFT_EARLY", StringComparison.OrdinalIgnoreCase))
                {
                    return "You already left this class early today.";
                }

                return alreadyResolvedMessage;
            }

            ClassroomChoiceUI.EnsureExists().Show(
                CourseName.ToUpperInvariant(),
                $"Room: {ClassroomNumber.Trim()}",
                onAttendClass: () => BeginAttend(activities),
                onPunchId: () => BeginProxy(),
                onBackChoice: () => { });

            return null;
        }

        public bool IsEligible(PlayerSaveState save)
        {
            if (save == null || !save.HasActiveUniversityDay)
            {
                return false;
            }

            if (!string.Equals(save.Role, StudentRole, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.Equals(save.Department, DepartmentCode, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return save.CurrentDay == ScheduledGameplayDay;
        }

        private void BeginAttend(DailyActivityState activities)
        {
            if (isBusy)
            {
                return;
            }

            EnsureSync();
            if (attendIcsSync == null)
            {
                SystemNotificationUI.Show("Progress sync is not available.");
                ClassroomChoiceUI.Instance?.RestoreGameplayControlsIfOwned();
                return;
            }

            isBusy = true;
            attendIcsSync.RequestAttendIcsStart(
                onSuccess: result =>
                {
                    isBusy = false;
                    if (result.Record.IsResolved)
                    {
                        SystemNotificationUI.Show("This class is already resolved for today.");
                        ClassroomChoiceUI.Instance?.RestoreGameplayControlsIfOwned();
                        return;
                    }

                    ClassroomLectureUI.EnsureExists().BeginLecture(
                        this,
                        attendIcsSync,
                        result,
                        onClosedCallback: () => { });
                },
                onFailure: () =>
                {
                    isBusy = false;
                    // Choice UI already closed without restore — give controls back so the player is not stuck.
                    ClassroomChoiceUI.Instance?.RestoreGameplayControlsIfOwned();
                });
        }

        private void BeginProxy()
        {
            if (isBusy)
            {
                return;
            }

            PlayerSaveState save = PlayerSaveState.Instance ?? FindFirstObjectByType<PlayerSaveState>();
            if (save == null || !save.IdCardIssued)
            {
                SystemNotificationUI.Show("You need an issued university ID card to punch in.");
                ClassroomChoiceUI.Instance?.RestoreGameplayControlsIfOwned();
                return;
            }

            EnsureSync();
            if (attendIcsSync == null)
            {
                SystemNotificationUI.Show("Progress sync is not available.");
                ClassroomChoiceUI.Instance?.RestoreGameplayControlsIfOwned();
                return;
            }

            isBusy = true;
            attendIcsSync.RequestAttendIcsProxy(
                onSuccess: result =>
                {
                    isBusy = false;
                    SystemNotificationUI.Show("Proxy — Punched ID and Left");
                    ClassroomChoiceUI.Instance?.RestoreGameplayControlsIfOwned();
                },
                onFailure: () =>
                {
                    isBusy = false;
                    ClassroomChoiceUI.Instance?.RestoreGameplayControlsIfOwned();
                });
        }

        private void EnsureSync()
        {
            if (attendIcsSync != null)
            {
                return;
            }

            PlayerProgressSync progress = ActivePlayerProgress();
            if (progress == null)
            {
                return;
            }

            attendIcsSync = IsIcsCourse
                ? progress
                : new ClassroomActivitySync(progress, ActivityId);
        }

        private void OnValidate()
        {
            if (classroomInteractionTarget == null)
            {
                classroomInteractionTarget = gameObject;
            }

            lectureDurationSeconds = Mathf.Max(1f, lectureDurationSeconds);
            scheduledGameplayDay = Mathf.Max(1, scheduledGameplayDay);
        }

        private static string ResolveText(string fromConfig, string fallback)
        {
            return !string.IsNullOrWhiteSpace(fromConfig) ? fromConfig.Trim() : fallback;
        }

        /// <summary>
        /// The visible HUD and the classroom door must share one player.
        /// Floor scenes that also contain a Player would otherwise accept the
        /// server result on a DailyActivityState the HUD never reads.
        /// </summary>
        private static DailyActivityState ActiveDailyActivityState()
        {
            if (StatsHUD.Instance != null)
            {
                DailyActivityState onHud = StatsHUD.Instance.GetComponent<DailyActivityState>();
                if (onHud != null)
                {
                    return onHud;
                }
            }

            return FindFirstObjectByType<DailyActivityState>();
        }

        private static PlayerProgressSync ActivePlayerProgress()
        {
            if (StatsHUD.Instance != null)
            {
                PlayerProgressSync onHud = StatsHUD.Instance.GetComponent<PlayerProgressSync>();
                if (onHud != null)
                {
                    return onHud;
                }
            }

            return FindFirstObjectByType<PlayerProgressSync>();
        }
    }

    /// <summary>
    /// Binds the shared lecture UI to one non-ICS classroom activity.
    /// ICS keeps calling <see cref="PlayerProgressSync"/> directly so its existing routes stay intact.
    /// </summary>
    public sealed class ClassroomActivitySync : IAttendIcsProgressSync
    {
        private readonly PlayerProgressSync progress;
        private readonly string activityId;

        public ClassroomActivitySync(PlayerProgressSync progress, string activityId)
        {
            this.progress = progress;
            this.activityId = activityId;
        }

        public bool IsHydrated => progress != null && progress.IsHydrated;
        public bool IsMutationInFlight => progress != null && progress.IsMutationInFlight;

        public void RequestAttendIcsStart(Action<AttendIcsSessionResult> onSuccess, Action onFailure)
        {
            progress.RequestClassroomStart(activityId, onSuccess, onFailure);
        }

        public void RequestAttendIcsPause(Action<AttendIcsSessionResult> onSuccess, Action onFailure)
        {
            progress.RequestClassroomPause(activityId, onSuccess, onFailure);
        }

        public void RequestAttendIcsResume(Action<AttendIcsSessionResult> onSuccess, Action onFailure)
        {
            progress.RequestClassroomResume(activityId, onSuccess, onFailure);
        }

        public void RequestAttendIcsMilestone(
            int milestoneSeconds,
            Action<AttendIcsSessionResult> onSuccess,
            Action onFailure)
        {
            progress.RequestClassroomMilestone(activityId, milestoneSeconds, onSuccess, onFailure);
        }

        public void RequestAttendIcsLeaveEarly(Action<AttendIcsSessionResult> onSuccess, Action onFailure)
        {
            progress.RequestClassroomLeaveEarly(activityId, onSuccess, onFailure);
        }

        public void RequestAttendIcsProxy(Action<AttendIcsSessionResult> onSuccess, Action onFailure)
        {
            progress.RequestClassroomProxy(activityId, onSuccess, onFailure);
        }
    }
}
