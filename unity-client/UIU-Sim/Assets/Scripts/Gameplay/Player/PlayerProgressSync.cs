using System;
using System.Collections;
using UIU.Simulator.Authentication;
using UIU.Simulator.Gameplay.Activities;
using UIU.Simulator.Gameplay.UI;
using UIU.Simulator.Networking;
using UnityEngine;

namespace UIU.Simulator.Gameplay.Player
{
    /// <summary>
    /// Centralized component managing persistent Player Aura, Academic Reputation, and day activities.
    /// Responsibilities:
    /// - Hydrates initial canonical stats via GET /api/players/me
    /// - Hydrates current-day activities via GET /api/players/me/activities
    /// - Syncs one-time initial ID tutorial pending flag into PlayerInventory
    /// - Sends single-attempt PATCH /api/players/me/stats for confirmed deltas
    /// - Sends single-attempt POST /api/players/me/activities/resolve for atomic activity+stat commits
    /// - Atomically consumes initial ID tutorial via POST /api/players/me/initial-id-tutorial/consume
    /// - Guarantees only one in-flight mutation at a time
    /// - Guards against mutations before initial hydration completes
    /// - Emits sync failure messages via SystemNotificationUI
    /// Attach to the Player prefab root alongside PlayerStats.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerStats))]
    public sealed class PlayerProgressSync : MonoBehaviour, IPlayerProgressSync, IActivityProgressSync
    {
        private const string ActivitiesPath = "api/players/me/activities";
        private const string ResolvePath = "api/players/me/activities/resolve";
        private const string FinalizeDayPath = "api/players/me/day/finalize";
        private const string AdvanceDayPath = "api/players/me/day/advance";

        private PlayerStats playerStats;
        private PlayerInventory playerInventory;
        private DailyActivityState dailyActivityState;
        private CampusDayState campusDayState;
        private PlayerSaveState playerSaveState;
        private ApiClient apiClient;
        private UserSession userSession;

        private bool isHydrated;
        private bool isMutationInFlight;
        private bool isHydrating;
        private bool isTutorialConsumeInFlight;

        public bool IsHydrated => isHydrated;
        public bool IsMutationInFlight => isMutationInFlight;

        public event Action<string> OnSyncFailed;
        public event Action<float, float> OnHydrated;

        private void Awake()
        {
            playerStats = GetComponent<PlayerStats>();
            playerInventory = GetComponent<PlayerInventory>();
            dailyActivityState = GetComponent<DailyActivityState>();
            campusDayState = GetComponent<CampusDayState>();
            playerSaveState = PlayerSaveState.Instance != null
                ? PlayerSaveState.Instance
                : FindFirstObjectByType<PlayerSaveState>();
        }

        private void Start()
        {
            EnsureDependencies();
            StartCoroutine(HydrateInitialStatsRoutine());
        }

        public void Configure(ApiClient client, UserSession session, PlayerStats stats = null)
        {
            apiClient = client;
            userSession = session;
            if (stats != null)
            {
                playerStats = stats;
            }
        }

        private void EnsureDependencies()
        {
            if (playerStats == null)
            {
                playerStats = GetComponent<PlayerStats>();
            }

            if (playerInventory == null)
            {
                playerInventory = GetComponent<PlayerInventory>();
            }

            if (dailyActivityState == null)
            {
                dailyActivityState = GetComponent<DailyActivityState>();
            }

            if (campusDayState == null)
            {
                campusDayState = GetComponent<CampusDayState>();
            }

            if (playerSaveState == null)
            {
                playerSaveState = PlayerSaveState.Instance != null
                    ? PlayerSaveState.Instance
                    : FindFirstObjectByType<PlayerSaveState>();
            }

            if (apiClient == null || userSession == null)
            {
                AuthHost host = AuthHost.Instance != null ? AuthHost.Instance : AuthHost.EnsureExists();
                if (host != null)
                {
                    if (apiClient == null) apiClient = host.ApiClient;
                    if (userSession == null && host.AuthManager != null) userSession = host.AuthManager.Session;
                }
            }
        }

        public IEnumerator HydrateInitialStatsRoutine()
        {
            if (isHydrating || isHydrated)
            {
                yield break;
            }

            isHydrating = true;
            EnsureDependencies();

            if (userSession == null || !userSession.HasToken)
            {
                userSession?.TryRestoreAuthenticatedSession();
            }

            if (userSession == null || !userSession.HasToken)
            {
                Debug.LogWarning("[PlayerProgressSync] Cannot hydrate stats: no authenticated session token.");
                isHydrating = false;
                yield break;
            }

            if (apiClient == null)
            {
                Debug.LogError("[PlayerProgressSync] ApiClient is not assigned. Cannot hydrate stats.");
                isHydrating = false;
                yield break;
            }

            Debug.Log($"[AuthDebug] PlayerProgressSync.HydrateInitialStatsRoutine: syncID={this.GetInstanceID()}, apiClientID={apiClient.GetInstanceID()}, sessionFp={(userSession != null ? AuthTokenProvider.Fingerprint(userSession.JwtToken) : "null")}");

            yield return apiClient.Get(
                "api/players/me",
                userSession.JwtToken,
                onSuccess: json =>
                {
                    try
                    {
                        ApiClient.PlayerDto dto = JsonUtility.FromJson<ApiClient.PlayerDto>(json);
                        if (dto != null)
                        {
                            isHydrated = true;
                            playerStats.ApplyServerState(dto.aura, dto.academicReputation, StatUpdateSource.InitialHydration);
                            // pending=true means tutorial not yet consumed → local "triggered" is false
                            if (playerInventory != null)
                            {
                                playerInventory.SetTriggeredInitialIDFailure(!dto.initialIdTutorialPending);
                            }
                            OnHydrated?.Invoke(dto.aura, dto.academicReputation);
                            Debug.Log($"[PlayerProgressSync] Hydrated canonical stats from server: Aura={dto.aura}, Reputation={dto.academicReputation}, initialIdTutorialPending={dto.initialIdTutorialPending}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[PlayerProgressSync] Failed to parse GET /api/players/me response: {ex.Message}");
                    }
                },
                onError: (error, code) =>
                {
                    string userMessage = ClassifyErrorMessage(code, error);
                    Debug.LogWarning($"[PlayerProgressSync] Initial hydration failed: {error} (HTTP {code})");
                    SystemNotificationUI.Show(userMessage);
                    OnSyncFailed?.Invoke(userMessage);
                }
            );

            if (isHydrated)
            {
                yield return HydrateActivitiesRoutine();
            }

            isHydrating = false;
        }

        private IEnumerator HydrateActivitiesRoutine()
        {
            EnsureDependencies();
            if (apiClient == null || userSession == null || !userSession.HasToken)
            {
                yield break;
            }

            yield return apiClient.Get(
                ActivitiesPath,
                userSession.JwtToken,
                onSuccess: json =>
                {
                    try
                    {
                        ApplyActivitiesPayload(json);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[PlayerProgressSync] Failed to parse GET /api/players/me/activities: {ex.Message}");
                    }
                },
                onError: (error, code) =>
                {
                    Debug.LogWarning($"[PlayerProgressSync] Activity hydration failed: {error} (HTTP {code})");
                }
            );
        }

        private void ApplyActivitiesPayload(string json)
        {
            ApiClient.ActivityListResponseDto dto = JsonUtility.FromJson<ApiClient.ActivityListResponseDto>(json);
            if (dto == null)
            {
                return;
            }

            if (dailyActivityState == null)
            {
                dailyActivityState = GetComponent<DailyActivityState>();
            }

            if (campusDayState == null)
            {
                campusDayState = GetComponent<CampusDayState>();
            }

            bool breakfastFound = false;
            bool getIdCardFound = false;
            if (dto.activities != null)
            {
                for (int i = 0; i < dto.activities.Length; i++)
                {
                    ApiClient.ActivityStateDto activity = dto.activities[i];
                    if (activity == null || string.IsNullOrWhiteSpace(activity.activityId))
                    {
                        continue;
                    }

                    ActivityStatus status = ParseActivityStatus(activity.status);
                    ActivityRecord record = new ActivityRecord(
                        activity.activityId,
                        status,
                        activity.outcome,
                        activity.auraDelta,
                        activity.reputationDelta,
                        activity.dayNumber > 0 ? activity.dayNumber : dto.dayNumber);

                    if (activity.activityId == ActivityIds.GetIdCard)
                    {
                        getIdCardFound = true;
                        dailyActivityState?.ApplyServerActivity(record);
                        continue;
                    }

                    if (activity.activityId != ActivityIds.Breakfast)
                    {
                        continue;
                    }

                    breakfastFound = true;
                    dailyActivityState?.ApplyServerActivity(record);
                    if (record.IsResolved)
                    {
                        campusDayState?.ApplyHydratedBreakfastResolved();
                    }
                }
            }

            if (!breakfastFound && dailyActivityState != null && dto.dayNumber > 0)
            {
                // Explicit pending for the current journey day (preserves GET_ID_CARD).
                if (dailyActivityState.BreakfastStatus != ActivityStatus.Pending
                    || dailyActivityState.DayNumber != dto.dayNumber)
                {
                    dailyActivityState.ResetForNewDay(dto.dayNumber);
                }
                else
                {
                    dailyActivityState.SetDayNumber(dto.dayNumber);
                }
            }
            else if (dto.dayNumber > 0)
            {
                dailyActivityState?.SetDayNumber(dto.dayNumber);
            }

            Debug.Log(
                $"[PlayerProgressSync] Hydrated activities for day={dto.dayNumber}, " +
                $"getIdCardResolved={getIdCardFound}, breakfastResolved={breakfastFound}");
        }

        /// <summary>
        /// Closes the current day on the server (auto-misses pending breakfast once) and returns the summary.
        /// </summary>
        public void RequestFinalizeDay(Action<DayFinalizeResult> onSuccess, Action<string> onFailure)
        {
            if (!isHydrated)
            {
                string msg = "Player progress is still loading.";
                SystemNotificationUI.Show(msg);
                onFailure?.Invoke(msg);
                return;
            }

            if (isMutationInFlight)
            {
                string msg = "Progress update is already in progress.";
                SystemNotificationUI.Show(msg);
                onFailure?.Invoke(msg);
                return;
            }

            EnsureDependencies();
            if (userSession == null || !userSession.HasToken || apiClient == null)
            {
                string msg = "Your session has expired. Please sign in again.";
                SystemNotificationUI.Show(msg);
                OnSyncFailed?.Invoke(msg);
                onFailure?.Invoke(msg);
                return;
            }

            StartCoroutine(FinalizeDayRoutine(onSuccess, onFailure));
        }

        /// <summary>
        /// Advances from the expected semester/day. Duplicate retries with the same expected day are idempotent.
        /// </summary>
        public void RequestAdvanceDay(
            int expectedSemester,
            int expectedDay,
            Action<DayAdvanceResult> onSuccess,
            Action<string> onFailure)
        {
            if (!isHydrated)
            {
                string msg = "Player progress is still loading.";
                SystemNotificationUI.Show(msg);
                onFailure?.Invoke(msg);
                return;
            }

            if (isMutationInFlight)
            {
                string msg = "Progress update is already in progress.";
                SystemNotificationUI.Show(msg);
                onFailure?.Invoke(msg);
                return;
            }

            EnsureDependencies();
            if (userSession == null || !userSession.HasToken || apiClient == null)
            {
                string msg = "Your session has expired. Please sign in again.";
                SystemNotificationUI.Show(msg);
                OnSyncFailed?.Invoke(msg);
                onFailure?.Invoke(msg);
                return;
            }

            StartCoroutine(AdvanceDayRoutine(expectedSemester, expectedDay, onSuccess, onFailure));
        }

        private IEnumerator FinalizeDayRoutine(Action<DayFinalizeResult> onSuccess, Action<string> onFailure)
        {
            isMutationInFlight = true;

            yield return apiClient.Post(
                FinalizeDayPath,
                "{}",
                userSession.JwtToken,
                onSuccess: json =>
                {
                    isMutationInFlight = false;
                    try
                    {
                        ApiClient.DayFinalizeResponseDto response =
                            JsonUtility.FromJson<ApiClient.DayFinalizeResponseDto>(json);
                        if (response == null)
                        {
                            onFailure?.Invoke("Invalid day finalize response.");
                            return;
                        }

                        DaySummaryActivity[] activities = MapSummaryActivities(response.activities);
                        for (int i = 0; i < activities.Length; i++)
                        {
                            DaySummaryActivity item = activities[i];
                            ActivityRecord record = new ActivityRecord(
                                item.ActivityId,
                                item.Status,
                                item.Outcome,
                                item.AuraDelta,
                                item.AcademicReputationDelta,
                                response.day);
                            dailyActivityState?.ApplyServerActivity(record);
                            if (item.ActivityId == ActivityIds.Breakfast && item.Status != ActivityStatus.Pending)
                            {
                                campusDayState?.CompleteBreakfastEvent();
                            }
                        }

                        dailyActivityState?.SetDayNumber(Mathf.Max(1, response.day));
                        playerStats.ApplyServerState(
                            response.aura,
                            response.academicReputation,
                            StatUpdateSource.GameplayMutation);

                        DayFinalizeResult result = new DayFinalizeResult(
                            response.semester,
                            response.day,
                            activities,
                            response.totalAuraDelta,
                            response.totalAcademicReputationDelta,
                            response.aura,
                            response.academicReputation);
                        onSuccess?.Invoke(result);
                        Debug.Log(
                            $"[PlayerProgressSync] Day finalized: semester={response.semester} day={response.day} " +
                            $"totalAuraDelta={response.totalAuraDelta}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[PlayerProgressSync] Failed to parse day finalize response: {ex.Message}");
                        onFailure?.Invoke("Could not read day summary.");
                    }
                },
                onError: (error, code) =>
                {
                    isMutationInFlight = false;
                    string userMessage = ClassifyDayErrorMessage(code, error);
                    Debug.LogWarning($"[PlayerProgressSync] Day finalize failed (HTTP {code}): {error}");
                    SystemNotificationUI.Show(userMessage);
                    OnSyncFailed?.Invoke(userMessage);
                    onFailure?.Invoke(userMessage);
                }
            );
        }

        private IEnumerator AdvanceDayRoutine(
            int expectedSemester,
            int expectedDay,
            Action<DayAdvanceResult> onSuccess,
            Action<string> onFailure)
        {
            isMutationInFlight = true;

            ApiClient.DayAdvanceRequestDto requestDto =
                new ApiClient.DayAdvanceRequestDto(expectedSemester, expectedDay);
            string jsonBody = JsonUtility.ToJson(requestDto);

            yield return apiClient.Post(
                AdvanceDayPath,
                jsonBody,
                userSession.JwtToken,
                onSuccess: json =>
                {
                    isMutationInFlight = false;
                    try
                    {
                        ApiClient.DayAdvanceResponseDto response =
                            JsonUtility.FromJson<ApiClient.DayAdvanceResponseDto>(json);
                        if (response == null)
                        {
                            onFailure?.Invoke("Invalid day advance response.");
                            return;
                        }

                        if (playerSaveState == null)
                        {
                            playerSaveState = PlayerSaveState.EnsureExists();
                        }

                        playerSaveState.ApplyAdvancedDay(
                            response.semester,
                            response.currentDay,
                            response.idCardIssued);

                        campusDayState?.BeginCampusDay(response.currentDay);
                        playerStats.ApplyServerState(
                            response.aura,
                            response.academicReputation,
                            StatUpdateSource.InitialHydration);

                        DayAdvanceResult result = new DayAdvanceResult(
                            response.semester,
                            response.currentDay,
                            response.alreadyAdvanced,
                            response.idCardIssued,
                            response.aura,
                            response.academicReputation);
                        onSuccess?.Invoke(result);
                        Debug.Log(
                            $"[PlayerProgressSync] Day advanced: semester={response.semester} " +
                            $"currentDay={response.currentDay} alreadyAdvanced={response.alreadyAdvanced}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[PlayerProgressSync] Failed to parse day advance response: {ex.Message}");
                        onFailure?.Invoke("Could not advance the day.");
                    }
                },
                onError: (error, code) =>
                {
                    isMutationInFlight = false;
                    string userMessage = ClassifyDayErrorMessage(code, error);
                    Debug.LogWarning($"[PlayerProgressSync] Day advance failed (HTTP {code}): {error}");
                    SystemNotificationUI.Show(userMessage);
                    OnSyncFailed?.Invoke(userMessage);
                    onFailure?.Invoke(userMessage);
                }
            );
        }

        private static DaySummaryActivity[] MapSummaryActivities(ApiClient.DaySummaryActivityDto[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<DaySummaryActivity>();
            }

            DaySummaryActivity[] mapped = new DaySummaryActivity[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                ApiClient.DaySummaryActivityDto item = source[i];
                mapped[i] = new DaySummaryActivity(
                    item != null ? item.activityId : string.Empty,
                    ParseActivityStatus(item != null ? item.status : null),
                    item != null ? item.outcome : string.Empty,
                    item != null ? item.auraDelta : 0,
                    item != null ? item.academicReputationDelta : 0);
            }

            return mapped;
        }

        private static string ClassifyDayErrorMessage(long code, string rawError)
        {
            if (code == 401)
            {
                return "Your session has expired. Please sign in again.";
            }

            if (code == 404)
            {
                return "Complete admission before ending the day.";
            }

            if (code == 400 && !string.IsNullOrWhiteSpace(rawError)
                && rawError.IndexOf("Semester progression", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Semester progression is not available yet.";
            }

            if (code == 0 || (rawError != null && (rawError.Contains("Cannot connect") || rawError.Contains("timeout") || rawError.Contains("Resolution"))))
            {
                return "Disconnected. Progress cannot be saved right now.";
            }

            if (!string.IsNullOrWhiteSpace(rawError) && rawError.Length < 120)
            {
                return rawError;
            }

            return "The game server is currently unavailable. Progress cannot be saved right now.";
        }

        /// <summary>
        /// Persistently consumes the one-time initial ID tutorial flag. Idempotent on the server.
        /// </summary>
        public void RequestConsumeInitialIdTutorial()
        {
            if (isTutorialConsumeInFlight)
            {
                return;
            }

            EnsureDependencies();
            if (userSession == null || !userSession.HasToken || apiClient == null)
            {
                Debug.LogWarning("[PlayerProgressSync] Cannot consume initial ID tutorial: missing auth or ApiClient.");
                return;
            }

            StartCoroutine(ConsumeInitialIdTutorialRoutine());
        }

        private IEnumerator ConsumeInitialIdTutorialRoutine()
        {
            isTutorialConsumeInFlight = true;

            yield return apiClient.Post(
                "api/players/me/initial-id-tutorial/consume",
                "{}",
                userSession.JwtToken,
                onSuccess: json =>
                {
                    isTutorialConsumeInFlight = false;
                    try
                    {
                        ApiClient.InitialIdTutorialConsumeResponseDto response =
                            JsonUtility.FromJson<ApiClient.InitialIdTutorialConsumeResponseDto>(json);
                        if (response != null)
                        {
                            if (playerInventory != null)
                            {
                                playerInventory.SetTriggeredInitialIDFailure(true);
                            }
                            Debug.Log($"[PlayerProgressSync] Initial ID tutorial consume result: consumed={response.consumed}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[PlayerProgressSync] Failed to parse tutorial consume response: {ex.Message}");
                    }
                },
                onError: (error, code) =>
                {
                    isTutorialConsumeInFlight = false;
                    Debug.LogWarning($"[PlayerProgressSync] Initial ID tutorial consume failed (HTTP {code}): {error}");
                }
            );
        }

        public void RequestStatDelta(int auraDelta, int academicReputationDelta)
        {
            if (auraDelta == 0 && academicReputationDelta == 0)
            {
                return;
            }

            // 1. Guard against pre-hydration mutations
            if (!isHydrated)
            {
                Debug.LogWarning("[PlayerProgressSync] Mutation rejected: Player progress is still loading.");
                SystemNotificationUI.Show("Player progress is still loading.");
                return;
            }

            // 2. Guard against concurrent in-flight mutations (Single in-flight request rule)
            if (isMutationInFlight)
            {
                Debug.LogWarning("[PlayerProgressSync] Mutation rejected: Progress update is already in progress.");
                SystemNotificationUI.Show("Progress update is already in progress.");
                return;
            }

            EnsureDependencies();
            if (userSession == null || !userSession.HasToken)
            {
                string msg = "Your session has expired. Please sign in again.";
                SystemNotificationUI.Show(msg);
                OnSyncFailed?.Invoke(msg);
                return;
            }

            StartCoroutine(MutateStatsRoutine(auraDelta, academicReputationDelta));
        }

        public void RequestActivityResolve(
            string activityId,
            string outcome,
            Action<ActivityResolveResult> onSuccess,
            Action onFailure)
        {
            if (string.IsNullOrWhiteSpace(activityId) || string.IsNullOrWhiteSpace(outcome))
            {
                onFailure?.Invoke();
                return;
            }

            if (!isHydrated)
            {
                Debug.LogWarning("[PlayerProgressSync] Activity resolve rejected: Player progress is still loading.");
                SystemNotificationUI.Show("Player progress is still loading.");
                onFailure?.Invoke();
                return;
            }

            if (isMutationInFlight)
            {
                Debug.LogWarning("[PlayerProgressSync] Activity resolve rejected: Progress update is already in progress.");
                SystemNotificationUI.Show("Progress update is already in progress.");
                onFailure?.Invoke();
                return;
            }

            EnsureDependencies();
            if (userSession == null || !userSession.HasToken)
            {
                string msg = "Your session has expired. Please sign in again.";
                SystemNotificationUI.Show(msg);
                OnSyncFailed?.Invoke(msg);
                onFailure?.Invoke();
                return;
            }

            StartCoroutine(ResolveActivityRoutine(activityId, outcome, onSuccess, onFailure));
        }

        private IEnumerator MutateStatsRoutine(int auraDelta, int academicReputationDelta)
        {
            isMutationInFlight = true;
            Debug.Log($"[AuthDebug] PlayerProgressSync.MutateStatsRoutine: syncID={this.GetInstanceID()}, apiClientID={(apiClient != null ? apiClient.GetInstanceID().ToString() : "null")}, sessionFp={(userSession != null ? AuthTokenProvider.Fingerprint(userSession.JwtToken) : "null")}");

            ApiClient.PlayerStatsDeltaRequestDto requestDto = new ApiClient.PlayerStatsDeltaRequestDto(auraDelta, academicReputationDelta);
            string jsonBody = JsonUtility.ToJson(requestDto);

            yield return apiClient.Patch(
                "api/players/me/stats",
                jsonBody,
                userSession.JwtToken,
                onSuccess: json =>
                {
                    isMutationInFlight = false;
                    try
                    {
                        ApiClient.PlayerStatsResponseDto response = JsonUtility.FromJson<ApiClient.PlayerStatsResponseDto>(json);
                        if (response != null)
                        {
                            playerStats.ApplyServerState(response.aura, response.academicReputation, StatUpdateSource.GameplayMutation);
                            Debug.Log($"[PlayerProgressSync] Confirmed persistent stat delta: Aura={response.aura}, Reputation={response.academicReputation}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[PlayerProgressSync] Failed to parse PATCH response: {ex.Message}");
                    }
                },
                onError: (error, code) =>
                {
                    isMutationInFlight = false;
                    string userMessage = ClassifyErrorMessage(code, error);
                    Debug.LogWarning($"[PlayerProgressSync] Stat mutation failed (HTTP {code}): {error}. Local stats unchanged.");
                    SystemNotificationUI.Show(userMessage);
                    OnSyncFailed?.Invoke(userMessage);
                }
            );
        }

        private IEnumerator ResolveActivityRoutine(
            string activityId,
            string outcome,
            Action<ActivityResolveResult> onSuccess,
            Action onFailure)
        {
            isMutationInFlight = true;

            ApiClient.ActivityResolveRequestDto requestDto = new ApiClient.ActivityResolveRequestDto(activityId, outcome);
            string jsonBody = JsonUtility.ToJson(requestDto);

            yield return apiClient.Post(
                ResolvePath,
                jsonBody,
                userSession.JwtToken,
                onSuccess: json =>
                {
                    isMutationInFlight = false;
                    try
                    {
                        ApiClient.ActivityResolveResponseDto response =
                            JsonUtility.FromJson<ApiClient.ActivityResolveResponseDto>(json);
                        if (response == null)
                        {
                            onFailure?.Invoke();
                            return;
                        }

                        ActivityRecord record = new ActivityRecord(
                            response.activityId,
                            ParseActivityStatus(response.status),
                            response.outcome,
                            response.auraDelta,
                            response.reputationDelta,
                            response.dayNumber);

                        dailyActivityState?.ApplyServerActivity(record);
                        if (record.IsResolved && record.ActivityId == ActivityIds.Breakfast)
                        {
                            campusDayState?.CompleteBreakfastEvent();
                        }

                        StatUpdateSource source = response.alreadyResolved
                            ? StatUpdateSource.InitialHydration
                            : StatUpdateSource.GameplayMutation;
                        playerStats.ApplyServerState(response.aura, response.academicReputation, source);

                        ActivityResolveResult result = new ActivityResolveResult(
                            record,
                            response.alreadyResolved,
                            response.aura,
                            response.academicReputation);
                        onSuccess?.Invoke(result);
                        Debug.Log(
                            $"[PlayerProgressSync] Activity resolve confirmed: {response.activityId} {response.outcome} " +
                            $"alreadyResolved={response.alreadyResolved} Aura={response.aura}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[PlayerProgressSync] Failed to parse activity resolve response: {ex.Message}");
                        onFailure?.Invoke();
                    }
                },
                onError: (error, code) =>
                {
                    isMutationInFlight = false;
                    string userMessage = ClassifyErrorMessage(code, error);
                    Debug.LogWarning($"[PlayerProgressSync] Activity resolve failed (HTTP {code}): {error}. Local state unchanged.");
                    SystemNotificationUI.Show(userMessage);
                    OnSyncFailed?.Invoke(userMessage);
                    onFailure?.Invoke();
                }
            );
        }

        private static ActivityStatus ParseActivityStatus(string raw)
        {
            switch ((raw ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "COMPLETED":
                    return ActivityStatus.Completed;
                case "MISSED":
                    return ActivityStatus.Missed;
                default:
                    return ActivityStatus.Pending;
            }
        }

        private static string ClassifyErrorMessage(long code, string rawError)
        {
            if (code == 401)
            {
                return "Your session has expired. Please sign in again.";
            }

            if (code == 404)
            {
                return "Complete admission before recording campus activities.";
            }

            if (code == 0 || (rawError != null && (rawError.Contains("Cannot connect") || rawError.Contains("timeout") || rawError.Contains("Resolution"))))
            {
                return "Disconnected. Progress cannot be saved right now.";
            }

            return "The game server is currently unavailable. Progress cannot be saved right now.";
        }
    }
}
