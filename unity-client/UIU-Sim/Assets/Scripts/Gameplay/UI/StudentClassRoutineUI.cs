using System;
using System.Collections.Generic;
using UIU.Simulator.Gameplay.Activities;
using UIU.Simulator.Gameplay.Classroom;
using UIU.Simulator.Gameplay.Faculty;
using UIU.Simulator.Gameplay.Player;
using UIU.Simulator.Networking;
using UnityEngine;

namespace UIU.Simulator.Gameplay.UI
{
    /// <summary>
    /// Student class-routine presenter. Reuses <see cref="ClassRoutineUI"/> and existing
    /// classroom catalog / location assets. Filters rows by the player's department
    /// (same CSE / BBA split as <c>ClassroomCourseDefinition.forDepartment</c>).
    /// Physics is omitted because no classroom course or location exists for it.
    /// </summary>
    public static class StudentClassRoutineUI
    {
        public const string Title = "STUDENT CLASS ROUTINE";

        private sealed class CatalogEntry
        {
            public string CourseId;
            public string CourseName;
            public string ClassroomNumber;
            public int Floor;
            public string DepartmentCode;
        }

        /// <summary>
        /// Mirrors backend ClassroomCourseDefinition + ClassroomLocationCatalog.
        /// CSE: ICS Room 427 Floor 4, English Room 702 Floor 7, Discrete Mathematics Room 423 Floor 4.
        /// BBA: IB Room 523 Floor 5, POA Room 527 Floor 5, BBA-ENGLISH Room 701 Floor 7.
        /// </summary>
        private static readonly CatalogEntry[] AllCourses =
        {
            new CatalogEntry
            {
                CourseId = "ICS",
                CourseName = "Introduction to Computer Science",
                ClassroomNumber = "427",
                Floor = 4,
                DepartmentCode = "CSE"
            },
            new CatalogEntry
            {
                CourseId = "ENGLISH",
                CourseName = "English",
                ClassroomNumber = "702",
                Floor = 7,
                DepartmentCode = "CSE"
            },
            new CatalogEntry
            {
                CourseId = "DM",
                CourseName = "Discrete Mathematics",
                ClassroomNumber = "423",
                Floor = 4,
                DepartmentCode = "CSE"
            },
            new CatalogEntry
            {
                CourseId = "IB",
                CourseName = "Introduction to Business",
                ClassroomNumber = "523",
                Floor = 5,
                DepartmentCode = "BBA"
            },
            new CatalogEntry
            {
                CourseId = "POA",
                CourseName = "Principles of Accounting",
                ClassroomNumber = "527",
                Floor = 5,
                DepartmentCode = "BBA"
            },
            new CatalogEntry
            {
                CourseId = "BBA-ENGLISH",
                CourseName = "English",
                ClassroomNumber = "701",
                Floor = 7,
                DepartmentCode = "BBA"
            }
        };

        public static void Show()
        {
            ClassRoutineUI.EnsureExists().Show(BuildRoutine(), Title);
        }

        public static ApiClient.FacultyRoutineItemDto[] BuildRoutine()
        {
            string department = ResolveDepartment();
            List<ApiClient.FacultyRoutineItemDto> routine = CreateCatalog(department);
            OverlayLiveLocations(routine, department);
            return routine.ToArray();
        }

        /// <summary>
        /// Department-scoped catalog. Blank department defaults to CSE (historical student routine).
        /// </summary>
        public static List<ApiClient.FacultyRoutineItemDto> CreateCatalog(string departmentCode = null)
        {
            string department = NormalizeDepartment(departmentCode);
            var routine = new List<ApiClient.FacultyRoutineItemDto>(3);
            for (int i = 0; i < AllCourses.Length; i++)
            {
                CatalogEntry entry = AllCourses[i];
                if (!MatchesDepartment(entry.DepartmentCode, department))
                {
                    continue;
                }

                routine.Add(new ApiClient.FacultyRoutineItemDto
                {
                    courseId = entry.CourseId,
                    courseName = entry.CourseName,
                    classroomNumber = entry.ClassroomNumber,
                    floor = entry.Floor
                });
            }

            return routine;
        }

        private static void OverlayLiveLocations(
            List<ApiClient.FacultyRoutineItemDto> routine,
            string departmentCode)
        {
            if (routine == null)
            {
                return;
            }

            DailyActivityState daily = UnityEngine.Object.FindFirstObjectByType<DailyActivityState>();
            if (daily == null)
            {
                return;
            }

            IcsClassroomLocationConfig[] configs = daily.SnapshotClassroomLocations();
            if (configs == null || configs.Length == 0)
            {
                return;
            }

            string department = NormalizeDepartment(departmentCode);
            for (int i = 0; i < configs.Length; i++)
            {
                IcsClassroomLocationConfig config = configs[i];
                if (config == null || string.IsNullOrWhiteSpace(config.CourseId))
                {
                    continue;
                }

                if (!MatchesDepartment(config.DepartmentCode, department))
                {
                    continue;
                }

                ApiClient.FacultyRoutineItemDto live = FromConfig(config);
                int index = IndexOfCourse(routine, live.courseId);
                if (index >= 0)
                {
                    routine[index] = live;
                    continue;
                }

                routine.Add(live);
            }
        }

        private static string ResolveDepartment()
        {
            PlayerSaveState save = PlayerSaveState.Instance;
            if (save != null && !string.IsNullOrWhiteSpace(save.Department))
            {
                return save.Department;
            }

            return "CSE";
        }

        private static string NormalizeDepartment(string departmentCode)
        {
            return string.IsNullOrWhiteSpace(departmentCode)
                ? "CSE"
                : departmentCode.Trim();
        }

        private static bool MatchesDepartment(string courseDepartment, string playerDepartment)
        {
            return string.Equals(
                courseDepartment?.Trim(),
                playerDepartment,
                StringComparison.OrdinalIgnoreCase);
        }

        private static ApiClient.FacultyRoutineItemDto FromConfig(IcsClassroomLocationConfig config)
        {
            return new ApiClient.FacultyRoutineItemDto
            {
                courseId = config.CourseId.Trim(),
                courseName = string.IsNullOrWhiteSpace(config.CourseName)
                    ? config.CourseId.Trim()
                    : config.CourseName.Trim(),
                classroomNumber = config.ClassroomNumber,
                floor = config.Floor
            };
        }

        private static int IndexOfCourse(List<ApiClient.FacultyRoutineItemDto> routine, string courseId)
        {
            for (int i = 0; i < routine.Count; i++)
            {
                ApiClient.FacultyRoutineItemDto item = routine[i];
                if (item != null
                    && string.Equals(item.courseId, courseId, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
