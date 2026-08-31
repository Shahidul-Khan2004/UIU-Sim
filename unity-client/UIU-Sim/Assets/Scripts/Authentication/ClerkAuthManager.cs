using System;
using System.Collections;
using UIU.Simulator.Networking;
using UnityEngine;

namespace UIU.Simulator.Authentication
{
    /// <summary>
    /// Opens browser Clerk login and coordinates logout.
    /// Dev workflow: HTTP session bridge (Linux/Editor). Deep links are optional.
    /// </summary>
    public sealed class ClerkAuthManager : MonoBehaviour
    {
        [SerializeField] private string backendBaseUrl = "http://localhost:8080";
        [SerializeField] private ApiClient apiClient;
        [SerializeField] private AuthCallbackHandler callbackHandler;
        [SerializeField] private bool restoreSessionOnStart;
        [Tooltip("When false (Unity auth testing), a valid JWT authenticates locally without calling Spring Boot.")]
        [SerializeField] private bool requireBackendValidation;
        [SerializeField] private float bridgePollTimeoutSeconds = 300f;
        [SerializeField] private float bridgePollIntervalSeconds = 1f;

        private Coroutine bridgePollRoutine;

        public UserSession Session { get; } = new UserSession();

        public string BackendBaseUrl => backendBaseUrl.TrimEnd('/');

        public event Action<UserSession> SessionChanged;

        public void Configure(
            string baseUrl,
            ApiClient client,
            AuthCallbackHandler handler,
            bool restoreOnStart = false,
            bool requireBackend = false)
        {
            backendBaseUrl = baseUrl;
            apiClient = client;
            callbackHandler = handler;
            restoreSessionOnStart = restoreOnStart;
            requireBackendValidation = requireBackend;
            callbackHandler?.Initialize(this);
        }

        private void Awake()
        {
            if (apiClient == null)
            {
                apiClient = GetComponent<ApiClient>();
            }

            if (callbackHandler == null)
            {
                callbackHandler = GetComponent<AuthCallbackHandler>();
            }

            callbackHandler?.Initialize(this);
            Session.StateChanged += _ => SessionChanged?.Invoke(Session);
        }

        private void Start()
        {
            if (restoreSessionOnStart)
            {
                Session.TryRestoreAuthenticatedSession();
                SessionChanged?.Invoke(Session);
            }
        }

        public void StartLogin()
        {
            if (string.IsNullOrWhiteSpace(backendBaseUrl))
            {
                Debug.LogError("[ClerkAuthManager] backendBaseUrl is empty — cannot open Clerk login.");
                Session.SetFailed("Missing backend base URL");
                return;
            }

            if (apiClient == null)
            {
                Session.SetFailed("ApiClient is not assigned");
                return;
            }

            Session.SetAuthenticating();

            string sessionId = Guid.NewGuid().ToString("N");
            string loginUrl = $"{BackendBaseUrl}/auth/login?session={sessionId}";
            Debug.Log($"[ClerkAuthManager] Opening Clerk authentication URL: {loginUrl}");
            Debug.Log("[ClerkAuthManager] Using HTTP auth bridge (recommended on Linux/Editor). Deep links are not required.");

            try
            {
                Application.OpenURL(loginUrl);
                Debug.Log("[ClerkAuthManager] Authentication browser launch requested");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ClerkAuthManager] Application.OpenURL failed: {ex.Message}");
                Session.SetFailed("Could not open browser");
                return;
            }

            if (bridgePollRoutine != null)
            {
                StopCoroutine(bridgePollRoutine);
            }

            bridgePollRoutine = StartCoroutine(PollAuthBridge(sessionId));
        }

        private IEnumerator PollAuthBridge(string sessionId)
        {
            Debug.Log($"[ClerkAuthManager] Polling auth bridge for session {sessionId}");
            yield return apiClient.PollDevAuthBridge(
                sessionId,
                bridgePollTimeoutSeconds,
                bridgePollIntervalSeconds,
                onToken: token =>
                {
                    Debug.Log("[ClerkAuthManager] Auth bridge returned a token");
                    HandleAuthCallbackToken(token);
                },
                onError: error =>
                {
                    Debug.LogWarning($"[ClerkAuthManager] Auth bridge: {error}");
                    Session.SetFailed(error);
                    SessionChanged?.Invoke(Session);
                });

            bridgePollRoutine = null;
        }

        public void Logout()
        {
            if (bridgePollRoutine != null)
            {
                StopCoroutine(bridgePollRoutine);
                bridgePollRoutine = null;
            }

            Session.Clear();
            SessionChanged?.Invoke(Session);
            Debug.Log("[ClerkAuthManager] Logged out");
        }

        public void HandleAuthCallbackToken(string jwtToken)
        {
            if (string.IsNullOrWhiteSpace(jwtToken))
            {
                Session.SetFailed("Callback token was empty");
                return;
            }

            if (bridgePollRoutine != null)
            {
                StopCoroutine(bridgePollRoutine);
                bridgePollRoutine = null;
            }

            Debug.Log("[ClerkAuthManager] Auth callback token received");
            Session.ApplyToken(jwtToken);
            Session.SetAuthenticating();

            if (requireBackendValidation)
            {
                StartCoroutine(CompleteBackendLogin());
                return;
            }

            CompleteLocalLogin(jwtToken);
        }

        private void CompleteLocalLogin(string jwtToken)
        {
            if (!JwtClaimsParser.TryRead(jwtToken, out string subject, out string email, out string username, out bool expired)
                || expired)
            {
                Session.SetFailed(expired ? "JWT expired" : "JWT payload invalid");
                SessionChanged?.Invoke(Session);
                return;
            }

            Session.ApplyPlayerProfile(subject, email, username);
            SessionChanged?.Invoke(Session);
            Debug.Log($"[ClerkAuthManager] Local authentication succeeded for {username} ({subject})");
        }

        private IEnumerator CompleteBackendLogin()
        {
            if (apiClient == null)
            {
                Session.SetFailed("ApiClient is not assigned");
                yield break;
            }

            yield return apiClient.LoginWithBearerToken(
                Session.JwtToken,
                onSuccess: player =>
                {
                    Session.ApplyPlayerProfile(player.clerkUserId, player.email, player.username);
                    SessionChanged?.Invoke(Session);
                    Debug.Log($"[ClerkAuthManager] Backend authentication succeeded for {player.username}");
                },
                onError: error =>
                {
                    Session.SetFailed(error);
                    SessionChanged?.Invoke(Session);
                });
        }
    }
}
