using System;
using System.Collections.Generic;
using UIU.Simulator.Gameplay.Activities;
using UIU.Simulator.Gameplay.Assessment;
using UIU.Simulator.Gameplay.Player;

namespace UIU.Simulator.Gameplay.NPC
{
    /// <summary>
    /// Snapshot of the player data NPC dialogue reacts to: role, department, trimester/day,
    /// current courses, and whether today is an assessment day.
    /// </summary>
    public sealed class StudentDialogueContext
    {
        public readonly struct CourseInfo
        {
            public CourseInfo(string id, string name)
            {
                Id = id ?? string.Empty;
                Name = string.IsNullOrWhiteSpace(name) ? Id : name.Trim();
            }

            public string Id { get; }
            public string Name { get; }
        }

        // Semester 1 CSE enrollment; used until the Report Card has been hydrated.
        private static readonly CourseInfo[] DefaultCseCourses =
        {
            new CourseInfo("ICS", "Introduction to Computer Science"),
            new CourseInfo("ENGLISH", "English"),
            new CourseInfo("DM", "Discrete Mathematics")
        };

        // Semester 1 BBA enrollment; used until the Report Card has been hydrated.
        private static readonly CourseInfo[] DefaultBbaCourses =
        {
            new CourseInfo("IB", "Introduction to Business"),
            new CourseInfo("POA", "Principles of Accounting"),
            new CourseInfo("BBA-ENGLISH", "English")
        };

        private readonly List<CourseInfo> courses;

        public StudentDialogueContext(
            string role,
            string department,
            int semester,
            int day,
            IEnumerable<CourseInfo> currentCourses = null,
            bool isAssessmentDay = false,
            string playerName = null,
            string universityId = null)
        {
            Role = (role ?? string.Empty).Trim().ToUpperInvariant();
            Department = (department ?? string.Empty).Trim().ToUpperInvariant();
            Semester = Math.Max(0, semester);
            Day = Math.Max(0, day);
            IsAssessmentDay = isAssessmentDay;
            PlayerName = playerName ?? string.Empty;
            UniversityId = universityId ?? string.Empty;
            courses = currentCourses != null ? new List<CourseInfo>(currentCourses) : new List<CourseInfo>();
        }

        public string Role { get; }
        public string Department { get; }
        public int Semester { get; }
        public int Day { get; }
        public bool IsAssessmentDay { get; }
        public string PlayerName { get; }
        public string UniversityId { get; }
        public IReadOnlyList<CourseInfo> Courses => courses;

        public DialoguePlayerRole? PlayerRole => Role switch
        {
            "STUDENT" => DialoguePlayerRole.Student,
            "FACULTY" => DialoguePlayerRole.Faculty,
            _ => null
        };

        public bool IsStudent => PlayerRole == DialoguePlayerRole.Student;

        public bool HasCourse(string courseId)
        {
            return FindCourse(courseId) != null;
        }

        public string CourseName(string courseId)
        {
            return FindCourse(courseId)?.Name;
        }

        private CourseInfo? FindCourse(string courseId)
        {
            if (string.IsNullOrWhiteSpace(courseId))
            {
                return null;
            }

            foreach (CourseInfo course in courses)
            {
                if (string.Equals(course.Id, courseId.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return course;
                }
            }

            return null;
        }

        /// <summary>
        /// Builds the context from hydrated save + activity state. Returns null when the player has no
        /// active university day yet (e.g. before admission), so NPCs do not start conversations.
        /// </summary>
        public static StudentDialogueContext FromGameState(PlayerSaveState save, DailyActivityState activities)
        {
            if (save == null || !save.HasActiveUniversityDay)
            {
                return null;
            }

            bool isStudent = string.Equals(save.Role, "STUDENT", StringComparison.OrdinalIgnoreCase);
            return new StudentDialogueContext(
                save.Role,
                save.Department,
                save.Semester,
                save.CurrentDay,
                isStudent ? ResolveCourses(save, activities) : null,
                activities != null && activities.IsAssessmentDay(save),
                save.PlayerName,
                save.UniversityId);
        }

        private static IEnumerable<CourseInfo> ResolveCourses(PlayerSaveState save, DailyActivityState activities)
        {
            ReportCard card = activities != null ? activities.ReportCard : null;
            if (card?.courses != null && card.courses.Length > 0)
            {
                var result = new List<CourseInfo>(card.courses.Length);
                foreach (CourseResult course in card.courses)
                {
                    if (course != null && !course.IsDropped && !string.IsNullOrWhiteSpace(course.courseId))
                    {
                        result.Add(new CourseInfo(course.courseId, course.courseName));
                    }
                }

                return result;
            }

            if (save.Semester == 1)
            {
                if (string.Equals(save.Department, "CSE", StringComparison.OrdinalIgnoreCase))
                {
                    return DefaultCseCourses;
                }

                if (string.Equals(save.Department, "BBA", StringComparison.OrdinalIgnoreCase))
                {
                    return DefaultBbaCourses;
                }
            }

            return Array.Empty<CourseInfo>();
        }
    }
}
