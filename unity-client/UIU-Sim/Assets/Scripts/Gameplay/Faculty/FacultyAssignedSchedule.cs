using System;
using System.Collections;
using System.Collections.Generic;
using UIU.Simulator.Authentication;
using UIU.Simulator.Gameplay.IDCard;
using UIU.Simulator.Gameplay.Player;
using UIU.Simulator.Networking;
using UnityEngine;

namespace UIU.Simulator.Gameplay.Faculty
{
    /// <summary>
    /// Faculty HUD schedule source. Assigned classrooms come from
    /// GET /api/players/me/faculty-routine (same ICS / DM rooms as the backend catalog).
    /// Display-only — does not change Reputation or FacultyProgress rewards.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FacultyAssignedSchedule : MonoBehaviour
    {
        public const string RoutinePath = "api/players/me/faculty-routine";

        public static FacultyAssignedSchedule Instance { get; private set; }

        private readonly List<ApiClient.FacultyRoutineItemDto> assigned = new List<ApiClient.FacultyRoutineItemDto>();
        private PlayerSaveState playerSaveState;
        private ApiClient apiClient;
        private UserSession userSession;
        private bool isLoading;
        private int taughtCountOverride = -1;

        public event Action OnScheduleChanged;

        public IReadOnlyList<ApiClient.FacultyRoutineItemDto> AssignedClasses => assigned;

        public static FacultyAssignedSchedule EnsureExists()
        {
            if (Instance != null)
            {
                return Instance;
            }

            FacultyAssignedSchedule existing = FindFirstObjectByType<FacultyAssignedSchedule>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            FacultyProgress progress = FacultyProgress.EnsureExists();
            FacultyAssignedSchedule schedule = progress.GetComponent<FacultyAssignedSchedule>();
            return schedule != null
                ? schedule
                : progress.gameObject.AddComponent<FacultyAssignedSchedule>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                enabled = false;
                return;
            }

            Instance = this;
            if (assigned.Count == 0)
            {
                ApplyAssigned(CreateFallbackAssigned(), notify: false);
            }
        }

        private void OnEnable()
        {
            if (playerSaveState == null)
            {
                playerSaveState = PlayerSaveState.Instance != null
                    ? PlayerSaveState.Instance
                    : FindFirstObjectByType<PlayerSaveState>();
            }

            if (playerSaveState != null)
            {
                playerSaveState.OnHydrated += RequestRoutineRefresh;
                playerSaveState.OnAdmissionCompleted += RequestRoutineRefresh;
            }

            RequestRoutineRefresh();
        }

        private void OnDisable()
        {
            if (playerSaveState != null)
            {
                playerSaveState.OnHydrated -= RequestRoutineRefresh;
                playerSaveState.OnAdmissionCompleted -= RequestRoutineRefresh;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void RequestRoutineRefresh()
        {
            if (!Application.isPlaying || isLoading)
            {
                return;
            }

            if (playerSaveState == null || !playerSaveState.IsHydrated
                || !FacultyIdentity.Matches(playerSaveState.Role))
            {
                return;
            }

            StartCoroutine(LoadRoutineRoutine());
        }

        public void ApplyAssigned(IReadOnlyList<ApiClient.FacultyRoutineItemDto> routine, bool notify = true)
        {
            assigned.Clear();
            if (routine != null)
            {
                for (int i = 0; i < routine.Count; i++)
                {
                    ApiClient.FacultyRoutineItemDto item = routine[i];
                    if (item != null && !string.IsNullOrWhiteSpace(item.courseName))
                    {
                        assigned.Add(item);
                    }
                }
            }

            if (assigned.Count == 0)
            {
                assigned.AddRange(CreateFallbackAssigned());
            }

            if (notify)
            {
                OnScheduleChanged?.Invoke();
            }
        }

        public void SetAssignedForTesting(params ApiClient.FacultyRoutineItemDto[] routine)
        {
            taughtCountOverride = -1;
            ApplyAssigned(routine);
        }

        public void SetTaughtCountForTesting(int taughtCount)
        {
            taughtCountOverride = Mathf.Max(0, taughtCount);
            OnScheduleChanged?.Invoke();
        }

        public int ResolveTaughtCount(FacultyProgress progress)
        {
            if (taughtCountOverride >= 0)
            {
                return Mathf.Min(taughtCountOverride, assigned.Count);
            }

            if (progress == null)
            {
                return 0;
            }

            int taught = 0;
            for (int i = 0; i < assigned.Count; i++)
            {
                ApiClient.FacultyRoutineItemDto item = assigned[i];
                if (item == null || !progress.IsCourseCompleted(item.courseId))
                {
                    break;
                }

                taught++;
            }

            return taught;
        }

        public ApiClient.FacultyRoutineItemDto GetCurrentClass(FacultyProgress progress)
        {
            int index = ResolveTaughtCount(progress);
            if (index < 0 || index >= assigned.Count)
            {
                return null;
            }

            return assigned[index];
        }

        public ApiClient.FacultyRoutineItemDto GetClassAt(int index)
        {
            if (index < 0 || index >= assigned.Count)
            {
                return null;
            }

            return assigned[index];
        }

        public static string BuildClassroomLabel(ApiClient.FacultyRoutineItemDto item)
        {
            if (item == null)
            {
                return "Classroom";
            }

            if (!string.IsNullOrWhiteSpace(item.courseId)
                && string.Equals(item.courseId.Trim(), "ICS", StringComparison.OrdinalIgnoreCase))
            {
                return "ICS Classroom";
            }

            string name = string.IsNullOrWhiteSpace(item.courseName)
                ? item.courseId
                : item.courseName.Trim();
            return string.IsNullOrWhiteSpace(name) ? "Classroom" : $"{name} Classroom";
        }

        public static string BuildCurrentObjective(ApiClient.FacultyRoutineItemDto item)
        {
            if (item == null)
            {
                return "Current Objective:\nAll classes completed.";
            }

            string courseName = string.IsNullOrWhiteSpace(item.courseName)
                ? BuildClassroomLabel(item)
                : item.courseName.Trim();
            return
                "Current Objective:\n" +
                $"Go to {courseName}\n" +
                $"Room: {item.classroomNumber}\n" +
                $"Floor: {item.floor}";
        }

        public static string BuildNextActivity(ApiClient.FacultyRoutineItemDto item)
        {
            if (item == null)
            {
                return "Next Activity:\n—";
            }

            return $"Next Activity:\nTeach {item.courseName}";
        }

        public static string BuildChecklistTitle(ApiClient.FacultyRoutineItemDto item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            return $"{item.courseName} - Room {item.classroomNumber}";
        }

        /// <summary>
        /// Offline / EditMode fallback matching FacultyRoutineService + ClassroomLocationCatalog.
        /// Replaced as soon as GET /me/faculty-routine succeeds.
        /// </summary>
        public static List<ApiClient.FacultyRoutineItemDto> CreateFallbackAssigned()
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
                    courseId = "DM",
                    courseName = "Discrete Mathematics",
                    classroomNumber = "423",
                    floor = 4
                }
            };
        }

        private IEnumerator LoadRoutineRoutine()
        {
            isLoading = true;
            EnsureDependencies();

            if (apiClient == null || userSession == null || !userSession.HasToken)
            {
                isLoading = false;
                yield break;
            }

            bool succeeded = false;
            string responseBody = null;
            yield return apiClient.Get(
                RoutinePath,
                userSession.JwtToken,
                body =>
                {
                    succeeded = true;
                    responseBody = body;
                },
                (error, code) =>
                {
                    Debug.LogWarning($"[FacultyAssignedSchedule] GET faculty-routine failed: {error} (HTTP {code})");
                });

            if (succeeded)
            {
                try
                {
                    ApiClient.FacultyRoutineResponseDto dto =
                        JsonUtility.FromJson<ApiClient.FacultyRoutineResponseDto>(responseBody);
                    if (dto != null && dto.routine != null && dto.routine.Length > 0)
                    {
                        ApplyAssigned(dto.routine);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[FacultyAssignedSchedule] Failed to parse faculty-routine: {ex.Message}");
                }
            }

            isLoading = false;
        }

        private void EnsureDependencies()
        {
            if (playerSaveState == null)
            {
                playerSaveState = PlayerSaveState.Instance != null
                    ? PlayerSaveState.Instance
                    : FindFirstObjectByType<PlayerSaveState>();
            }

            if (apiClient != null && userSession != null)
            {
                return;
            }

            AuthHost host = AuthHost.Instance != null ? AuthHost.Instance : AuthHost.EnsureExists();
            if (host == null)
            {
                return;
            }

            if (apiClient == null)
            {
                apiClient = host.ApiClient;
            }

            if (userSession == null && host.AuthManager != null)
            {
                userSession = host.AuthManager.Session;
            }
        }
    }
}
