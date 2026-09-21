using UnityEngine;

namespace UIU.Simulator.Gameplay.Classroom
{
    /// <summary>
    /// Persistent ICS classroom location used by the HUD independently of whether
    /// Floor04 (or any additive floor scene) is currently loaded.
    /// Assign the same asset on <see cref="IcsClassroomInteractable"/> so interaction copy stays aligned.
    /// </summary>
    [CreateAssetMenu(
        fileName = "IcsClassroomLocation",
        menuName = "UIU Simulator/Gameplay/ICS Classroom Location")]
    public sealed class IcsClassroomLocationConfig : ScriptableObject
    {
        [Header("Course Identity")]
        [SerializeField] private string courseId = "ICS";
        [SerializeField] private string courseName = "Introduction to Computer Science";
        [SerializeField] private string departmentCode = "CSE";
        [SerializeField] private string studentRole = "STUDENT";

        [Header("Classroom Location")]
        [Tooltip("Classroom number shown in objectives. Must match the Floor04 classroom interactable.")]
        [SerializeField] private string classroomNumber = "427";

        [Tooltip("Floor the classroom is on. Must match FloorSceneLoader floor indices (0=Ground, 1=Floor01, ...).")]
        [SerializeField] private int floor = 4;

        [Header("Schedule")]
        [Tooltip("Must match backend AttendIcsDefinition.SCHEDULED_DAY (default 1).")]
        [SerializeField] private int scheduledGameplayDay = 1;

        public string CourseId => courseId;
        public string CourseName => courseName;
        public string DepartmentCode => departmentCode;
        public string StudentRole => studentRole;
        public string ClassroomNumber => classroomNumber;
        public int Floor => floor;
        public int ScheduledGameplayDay => Mathf.Max(1, scheduledGameplayDay);

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(classroomNumber) && floor >= 0;

        public string BuildObjectiveDescription()
        {
            if (!IsConfigured)
            {
                return string.Empty;
            }

            string name = string.IsNullOrWhiteSpace(courseName)
                ? "Introduction to Computer Science"
                : courseName.Trim();
            return $"Go to Room {classroomNumber.Trim()} on Floor {floor} to attend {name}.";
        }
    }
}
