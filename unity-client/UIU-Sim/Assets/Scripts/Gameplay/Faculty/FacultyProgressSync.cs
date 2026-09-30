using System;
using System.Collections;
using UIU.Simulator.Authentication;
using UIU.Simulator.Gameplay.IDCard;
using UIU.Simulator.Gameplay.Player;
using UIU.Simulator.Gameplay.UI;
using UIU.Simulator.Networking;
using UnityEngine;

namespace UIU.Simulator.Gameplay.Faculty
{
    /// <summary>
    /// Hydrates and mutates faculty Reputation. Does not touch PlayerStats or PATCH /me/stats.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FacultyProgress))]
    public sealed class FacultyProgressSync : MonoBehaviour, IFacultyTeachProgressSync
    {
        private const string ProgressPath = "api/players/me/faculty-progress";
        private const string ComputerPath = "api/players/me/faculty-progress/computer";
        private const string CoffeePath = "api/players/me/faculty-progress/coffee";
        private const string PrepareQuestionsPath = "api/players/me/faculty-progress/prepare-questions";
        private const string ScanPath = "api/players/me/faculty-teach/scan";
        private const string CompletePath = "api/players/me/faculty-teach/complete";
        private const string LeavePath = "api/players/me/faculty-teach/leave";

        public static FacultyProgressSync Instance { get; private set; }

        private FacultyProgress facultyProgress;
        private PlayerSaveState playerSaveState;
        private ApiClient apiClient;
        private UserSession userSession;

        private bool isHydrated;
        private bool isHydrating;
        private bool isMutationInFlight;

        public bool IsHydrated => isHydrated;
        public bool IsMutationInFlight => isMutationInFlight;

        public void ResetForNewGame()
        {
            isHydrated = false;
            isHydrating = false;
            isMutationInFlight = false;
            if (facultyProgress != null)
            {
                facultyProgress.ResetToDefaults();
            }
        }

        public event Action<string> OnSyncFailed;

        public static FacultyProgressSync EnsureExists()
        {
            if (Instance != null)
            {
                return Instance;
            }

            FacultyProgressSync existing = FindFirstObjectByType<FacultyProgressSync>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            FacultyProgress progress = FacultyProgress.EnsureExists();
            return progress.gameObject.AddComponent<FacultyProgressSync>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                enabled = false;
                return;
            }

            Instance = this;
            facultyProgress = GetComponent<FacultyProgress>();
            playerSaveState = PlayerSaveState.Instance != null
                ? PlayerSaveState.Instance
                : FindFirstObjectByType<PlayerSaveState>();
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
                playerSaveState.OnHydrated += HandleSaveHydrated;
                playerSaveState.OnAdmissionCompleted += HandleSaveHydrated;
                playerSaveState.OnDayProgressChanged += HandleDayProgressChanged;
            }
        }

        private void OnDisable()
        {
            if (playerSaveState != null)
            {
                playerSaveState.OnHydrated -= HandleSaveHydrated;
                playerSaveState.OnAdmissionCompleted -= HandleSaveHydrated;
                playerSaveState.OnDayProgressChanged -= HandleDayProgressChanged;
            }
        }

        private void Start()
        {
            EnsureDependencies();
            HandleSaveHydrated();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void Configure(ApiClient client, UserSession session, FacultyProgress progress = null)
        {
            apiClient = client;
            userSession = session;
            if (progress != null)
            {
                facultyProgress = progress;
            }
        }

        public void RequestComputerUse()
        {
            RequestMutation(ComputerPath, jsonBody: null, FacultyUpdateSource.GameplayMutation, null, null);
        }

        public void RequestCoffee(string option, Action onSuccess = null, Action onFailure = null)
        {
            if (string.IsNullOrWhiteSpace(option))
            {
                onFailure?.Invoke();
                return;
            }

            string json = JsonUtility.ToJson(new ApiClient.FacultyCoffeeRequestDto(option.Trim()));
            RequestMutation(CoffeePath, json, FacultyUpdateSource.GameplayMutation, onSuccess, onFailure);
        }

        public void RequestPrepareQuestions(Action onSuccess = null, Action onFailure = null)
        {
            if (!Application.isPlaying)
            {
                ApplyPrepareQuestionsLocallyForTesting();
                onSuccess?.Invoke();
                return;
            }

            RequestMutation(PrepareQuestionsPath, jsonBody: null, FacultyUpdateSource.GameplayMutation, onSuccess, onFailure);
        }

        /// <summary>
        /// EditMode / offline seam used when prepare-questions cannot hit the network.
        /// Applies +3 Reputation once and marks questions prepared for the current day.
        /// </summary>
        public void ApplyPrepareQuestionsLocallyForTesting()
        {
            if (facultyProgress == null)
            {
                facultyProgress = GetComponent<FacultyProgress>() ?? FacultyProgress.EnsureExists();
            }

            if (facultyProgress == null || facultyProgress.QuestionsPreparedForCurrentDay)
            {
                return;
            }

            facultyProgress.SetStateForTesting(
                Mathf.Clamp(facultyProgress.Reputation + 3, 0, 100),
                facultyProgress.ComputerUsed,
                facultyProgress.OfficeEntered,
                facultyProgress.ClassroomScanned,
                facultyProgress.LectureCompleted,
                facultyProgress.LectureLeft,
                facultyProgress.IcsMaterialPrepared,
                facultyProgress.DmMaterialPrepared,
                FacultyUpdateSource.GameplayMutation,
                facultyProgress.IcsCompleted,
                facultyProgress.DmCompleted,
                facultyProgress.FacultyIdIssued,
                facultyProgress.CoffeeClaimedForCurrentDay,
                facultyProgress.CoffeeOption,
                questionsPreparedForCurrentDayValue: true);
        }

        public void RequestScan(string courseId, Action onSuccess, Action onFailure)
        {
            string json = JsonUtility.ToJson(new ApiClient.FacultyTeachScanRequestDto(courseId));
            RequestMutation(ScanPath, json, FacultyUpdateSource.GameplayMutation, onSuccess, onFailure);
        }

        public void RequestCompleteLecture(Action onSuccess, Action onFailure)
        {
            RequestMutation(CompletePath, jsonBody: null, FacultyUpdateSource.GameplayMutation, onSuccess, onFailure);
        }

        public void RequestLeaveLecture(Action onSuccess, Action onFailure)
        {
            RequestMutation(LeavePath, jsonBody: null, FacultyUpdateSource.GameplayMutation, onSuccess, onFailure);
        }

        public void RefreshAfterMaterialSaved()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            StartCoroutine(HydrateRoutine(FacultyUpdateSource.GameplayMutation));
        }

        private void HandleSaveHydrated()
        {
            if (playerSaveState == null || !playerSaveState.IsHydrated)
            {
                return;
            }

            if (!FacultyIdentity.Matches(playerSaveState.Role))
            {
                return;
            }

            if (facultyProgress != null)
            {
                facultyProgress.SetFacultyIdIssued(playerSaveState.IdCardIssued);
            }

            FacultyHUD.EnsureExists();
            if (!isHydrating)
            {
                StartCoroutine(HydrateRoutine(FacultyUpdateSource.InitialHydration));
            }
        }

        private void HandleDayProgressChanged()
        {
            if (playerSaveState == null || !playerSaveState.IsHydrated)
            {
                return;
            }

            if (!FacultyIdentity.Matches(playerSaveState.Role))
            {
                return;
            }

            if (!Application.isPlaying)
            {
                return;
            }

            if (!isHydrating)
            {
                StartCoroutine(HydrateRoutine(FacultyUpdateSource.InitialHydration));
            }
        }

        private void RequestMutation(
            string path,
            string jsonBody,
            FacultyUpdateSource source,
            Action onSuccess,
            Action onFailure)
        {
            if (!Application.isPlaying)
            {
                onSuccess?.Invoke();
                return;
            }

            if (isMutationInFlight)
            {
                SystemNotificationUI.Show("Faculty progress update is already in progress.");
                onFailure?.Invoke();
                return;
            }

            EnsureDependencies();
            if (userSession == null || !userSession.HasToken || apiClient == null)
            {
                string msg = "Your session has expired. Please sign in again.";
                SystemNotificationUI.Show(msg);
                OnSyncFailed?.Invoke(msg);
                onFailure?.Invoke();
                return;
            }

            StartCoroutine(MutateRoutine(path, jsonBody, source, onSuccess, onFailure));
        }

        private IEnumerator HydrateRoutine(FacultyUpdateSource source)
        {
            isHydrating = true;
            EnsureDependencies();

            if (userSession == null || !userSession.HasToken || apiClient == null)
            {
                Debug.LogWarning("[FacultyProgressSync] Cannot hydrate faculty progress: missing auth.");
                isHydrating = false;
                yield break;
            }

            bool succeeded = false;
            string responseBody = null;
            yield return apiClient.Get(
                ProgressPath,
                userSession.JwtToken,
                body =>
                {
                    succeeded = true;
                    responseBody = body;
                },
                (error, code) =>
                {
                    Debug.LogWarning($"[FacultyProgressSync] GET faculty-progress failed: {error} (HTTP {code})");
                });

            if (succeeded)
            {
                ApplyResponse(responseBody, source);
                isHydrated = true;
            }

            isHydrating = false;
        }

        private IEnumerator MutateRoutine(
            string path,
            string jsonBody,
            FacultyUpdateSource source,
            Action onSuccess,
            Action onFailure)
        {
            isMutationInFlight = true;
            bool succeeded = false;
            string responseBody = null;
            string errorMessage = "Unable to update faculty reputation.";

            if (string.IsNullOrEmpty(jsonBody))
            {
                yield return apiClient.Post(
                    path,
                    "{}",
                    userSession.JwtToken,
                    body =>
                    {
                        succeeded = true;
                        responseBody = body;
                    },
                    (error, _) => errorMessage = error);
            }
            else
            {
                yield return apiClient.Post(
                    path,
                    jsonBody,
                    userSession.JwtToken,
                    body =>
                    {
                        succeeded = true;
                        responseBody = body;
                    },
                    (error, _) => errorMessage = error);
            }

            if (succeeded)
            {
                ApiClient.FacultyProgressResponseDto dto = ApplyResponse(responseBody, source);
                isHydrated = true;
                if (path == ScanPath
                    && dto != null
                    && !string.IsNullOrWhiteSpace(dto.teachBlockedReason))
                {
                    SystemNotificationUI.Show(dto.teachBlockedReason);
                    OnSyncFailed?.Invoke(dto.teachBlockedReason);
                    onFailure?.Invoke();
                }
                else
                {
                    onSuccess?.Invoke();
                }
            }
            else
            {
                SystemNotificationUI.Show(errorMessage);
                OnSyncFailed?.Invoke(errorMessage);
                onFailure?.Invoke();
            }

            isMutationInFlight = false;
        }

        private ApiClient.FacultyProgressResponseDto ApplyResponse(string json, FacultyUpdateSource source)
        {
            if (string.IsNullOrWhiteSpace(json) || facultyProgress == null)
            {
                return null;
            }

            try
            {
                ApiClient.FacultyProgressResponseDto dto =
                    JsonUtility.FromJson<ApiClient.FacultyProgressResponseDto>(json);
                if (dto == null)
                {
                    return null;
                }

                facultyProgress.ApplyServerState(
                    dto.reputation,
                    dto.computerUsed,
                    dto.officeEntered,
                    dto.classroomScanned,
                    dto.lectureCompleted,
                    dto.lectureLeft,
                    dto.icsMaterialPrepared,
                    dto.dmMaterialPrepared,
                    source,
                    dto.icsCompleted,
                    dto.dmCompleted,
                    dto.facultyIdIssued,
                    dto.coffeeClaimedForCurrentDay,
                    dto.coffeeOption,
                    dto.questionsPreparedForCurrentDay);
                return dto;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FacultyProgressSync] Failed to parse faculty progress: {ex.Message}");
                return null;
            }
        }

        private void EnsureDependencies()
        {
            if (facultyProgress == null)
            {
                facultyProgress = GetComponent<FacultyProgress>();
            }

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
