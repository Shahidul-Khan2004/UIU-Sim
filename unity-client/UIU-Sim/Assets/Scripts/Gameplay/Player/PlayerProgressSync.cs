using System;
using System.Collections;
using UIU.Simulator.Authentication;
using UIU.Simulator.Gameplay.UI;
using UIU.Simulator.Networking;
using UnityEngine;

namespace UIU.Simulator.Gameplay.Player
{
    /// <summary>
    /// Centralized component managing persistent Player Aura and Academic Reputation.
    /// Responsibilities:
    /// - Hydrates initial canonical stats via GET /api/players/me
    /// - Sends single-attempt PATCH /api/players/me/stats for confirmed deltas
    /// - Guarantees only one in-flight mutation at a time
    /// - Guards against mutations before initial hydration completes
    /// - Emits sync failure messages via SystemNotificationUI
    /// Attach to the Player prefab root alongside PlayerStats.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerStats))]
    public sealed class PlayerProgressSync : MonoBehaviour, IPlayerProgressSync
    {
        private PlayerStats playerStats;
        private ApiClient apiClient;
        private UserSession userSession;

        private bool isHydrated;
        private bool isMutationInFlight;
        private bool isHydrating;

        public bool IsHydrated => isHydrated;
        public bool IsMutationInFlight => isMutationInFlight;

        public event Action<string> OnSyncFailed;
        public event Action<float, float> OnHydrated;

        private void Awake()
        {
            playerStats = GetComponent<PlayerStats>();
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
                            OnHydrated?.Invoke(dto.aura, dto.academicReputation);
                            Debug.Log($"[PlayerProgressSync] Hydrated canonical stats from server: Aura={dto.aura}, Reputation={dto.academicReputation}");
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

            isHydrating = false;
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

        private static string ClassifyErrorMessage(long code, string rawError)
        {
            if (code == 401)
            {
                return "Your session has expired. Please sign in again.";
            }

            if (code == 0 || (rawError != null && (rawError.Contains("Cannot connect") || rawError.Contains("timeout") || rawError.Contains("Resolution"))))
            {
                return "Disconnected. Progress cannot be saved right now.";
            }

            return "The game server is currently unavailable. Progress cannot be saved right now.";
        }
    }
}
