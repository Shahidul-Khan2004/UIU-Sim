using System;
using System.Collections.Generic;
using UIU.Simulator.Gameplay.Activities;
using UIU.Simulator.Gameplay.Classroom;
using UIU.Simulator.Gameplay.Faculty;
using UIU.Simulator.Networking;
using UnityEngine;

namespace UIU.Simulator.Gameplay.UI
{
    /// <summary>
    /// Student class-routine presenter. Reuses <see cref="ClassRoutineUI"/> and existing
    /// CSE classroom catalog / location assets. Does not create a second canvas or card system.
    /// Physics is omitted because no classroom course or location exists for it.
    /// </summary>
    public static class StudentClassRoutineUI
    {
        public const string Title = "STUDENT CLASS ROUTINE";

        public static void Show()
        {
            ClassRoutineUI.EnsureExists().Show(BuildRoutine(), Title);
        }

        public static ApiClient.FacultyRoutineItemDto[] BuildRoutine()
        {
            List<ApiClient.FacultyRoutineItemDto> routine = CreateCatalog();
            OverlayLiveLocations(routine);
            return routine.ToArray();
        }

        /// <summary>
        /// Mirrors backend ClassroomCourseDefinition + ClassroomLocationCatalog.
        /// ICS Room 427 Floor 4, English Room 702 Floor 7, Discrete Mathematics Room 423 Floor 4.
        /// </summary>
        public static List<ApiClient.FacultyRoutineItemDto> CreateCatalog()
        {
            return new List<ApiClient.FacultyRoutineItemDto>
            {
                new ApiClient.FacultyRoutineItemDto
                {
                    courseId = "ICS",
                    courseName = "Introduction to Computer Science",
                    classroomNumber = "427",
                    floor = 4
                },
                new ApiClient.FacultyRoutineItemDto
                {
                    courseId = "ENGLISH",
                    courseName = "English",
                    classroomNumber = "702",
                    floor = 7
                },
                new ApiClient.FacultyRoutineItemDto
                {
                    courseId = "DM",
                    courseName = "Discrete Mathematics",
                    classroomNumber = "423",
                    floor = 4
                }
            };
        }

        private static void OverlayLiveLocations(List<ApiClient.FacultyRoutineItemDto> routine)
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

            for (int i = 0; i < configs.Length; i++)
            {
                IcsClassroomLocationConfig config = configs[i];
                if (config == null || string.IsNullOrWhiteSpace(config.CourseId))
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
