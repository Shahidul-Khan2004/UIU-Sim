using System;
using UIU.Simulator.Gameplay.Activities;
using UIU.Simulator.Gameplay.Player;
using UIU.Simulator.Gameplay.UI;
using UnityEngine;

namespace UIU.Simulator.Gameplay.Classroom
{
    /// <summary>
    /// Inspector-configurable ICS classroom entrance. Room number and floor are never hardcoded —
    /// assign them (and the interaction collider) in the Unity Inspector on this component.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class IcsClassroomInteractable : MonoBehaviour, IInteractable
    {
        [Header("Course Identity")]
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

        public string CourseId => courseId;
        public string CourseName => courseName;
        public string DepartmentCode => departmentCode;
        public string StudentRole => studentRole;
        public string ClassroomNumber => classroomNumber;
        public int Floor => floor;
        public GameObject ClassroomInteractionTarget =>
            classroomInteractionTarget != null ? classroomInteractionTarget : gameObject;
        public int ScheduledGameplayDay => scheduledGameplayDay;
        public string DisplayedClassTime => displayedClassTime;
        public float LectureDurationSeconds => lectureDurationSeconds;

        public bool HasClassroomConfigured =>
            !string.IsNullOrWhiteSpace(classroomNumber) && floor >= 0;

        public string MissingSetupMessage => missingSetupMessage;

        public string BuildObjectiveDescription()
        {
            if (!HasClassroomConfigured)
            {
                return missingSetupMessage;
            }

            return $"Go to Room {classroomNumber.Trim()} on Floor {floor} to attend {courseName}.";
        }

        public void SetAttendIcsSyncForTesting(IAttendIcsProgressSync sync)
        {
            attendIcsSync = sync;
        }

        public string InteractionPrompt =>
            HasClassroomConfigured ? $"{prompt} (Room {classroomNumber.Trim()})" : prompt;

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
            DailyActivityState activities = FindFirstObjectByType<DailyActivityState>();

            if (save == null || !save.HasActiveUniversityDay)
            {
                return "Complete admission before attending class.";
            }

            if (!IsEligible(save))
            {
                return ineligibleMessage;
            }

            if (activities != null && activities.IsAttendIcsResolved)
            {
                return alreadyResolvedMessage;
            }

            ClassroomChoiceUI.EnsureExists().Show(
                courseName.ToUpperInvariant(),
                $"Room: {classroomNumber.Trim()}",
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

            if (!string.Equals(save.Role, studentRole, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.Equals(save.Department, departmentCode, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return save.CurrentDay == scheduledGameplayDay;
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
                return;
            }

            isBusy = true;
            FreezePlayer(true);

            attendIcsSync.RequestAttendIcsStart(
                onSuccess: result =>
                {
                    isBusy = false;
                    if (result.Record.IsResolved)
                    {
                        FreezePlayer(false);
                        SystemNotificationUI.Show("This class is already resolved for today.");
                        return;
                    }

                    ClassroomLectureUI.EnsureExists().BeginLecture(
                        this,
                        attendIcsSync,
                        result,
                        onClosedCallback: () => FreezePlayer(false));
                },
                onFailure: () =>
                {
                    isBusy = false;
                    FreezePlayer(false);
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
                return;
            }

            EnsureSync();
            if (attendIcsSync == null)
            {
                SystemNotificationUI.Show("Progress sync is not available.");
                return;
            }

            isBusy = true;
            attendIcsSync.RequestAttendIcsProxy(
                onSuccess: result =>
                {
                    isBusy = false;
                    SystemNotificationUI.Show("Proxy — Punched ID and Left");
                },
                onFailure: () => { isBusy = false; });
        }

        private void EnsureSync()
        {
            if (attendIcsSync != null)
            {
                return;
            }

            attendIcsSync = FindFirstObjectByType<PlayerProgressSync>();
        }

        private static void FreezePlayer(bool freeze)
        {
            PlayerMovement movement = FindFirstObjectByType<PlayerMovement>();
            FirstPersonLook look = FindFirstObjectByType<FirstPersonLook>();
            if (movement != null)
            {
                movement.enabled = !freeze;
            }

            if (look != null)
            {
                look.enabled = !freeze;
            }
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
    }
}
