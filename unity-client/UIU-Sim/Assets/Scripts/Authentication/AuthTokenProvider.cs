using System;
using System.Collections;
using System.Collections.Generic;
using UIU.Simulator.Networking;
using UnityEngine;

namespace UIU.Simulator.Authentication
{
    /// <summary>
    /// Centralized token lifecycle manager.
    /// Responsibilities:
    /// - Holds current Clerk session JWT and application-defined refreshSecret in process memory only.
    /// - Proactively refreshes expiring tokens before API requests (with a 15-second safety buffer).
    /// - Coalesces concurrent API requests into a single in-flight refresh (single-flight rule).
    /// - Rotates refreshSecret on every successful refresh (replay prevention).
    /// - Provides one-time force refresh routine for HTTP 401 recovery.
    /// - Purges credentials and clears UserSession if refresh fails with 401.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AuthTokenProvider : MonoBehaviour
    {
        private const int TokenExpiryBufferSeconds = 15;

        private ApiClient apiClient;
        private UserSession userSession;

        private string currentJwtToken = string.Empty;
        private long currentJwtExp;
        private string bridgeSessionId = string.Empty;
        private string currentRefreshSecret = string.Empty;

        private bool isRefreshInFlight;
        private readonly List<Action<string, string>> pendingCallbacks = new List<Action<string, string>>();

        public string CurrentToken => currentJwtToken;
        public long CurrentExp => currentJwtExp;

        public bool HasAccessToken => !string.IsNullOrWhiteSpace(currentJwtToken);

        public bool HasCredentials =>
            HasAccessToken &&
            !string.IsNullOrWhiteSpace(currentRefreshSecret) &&
            !string.IsNullOrWhiteSpace(bridgeSessionId);

        public static string Fingerprint(string token)
        {
            if (string.IsNullOrEmpty(token)) return "null/empty";
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(token));
                return BitConverter.ToString(bytes).Replace("-", "").Substring(0, 8).ToLowerInvariant();
            }
        }

        public string DiagnosticSummary()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long remaining = currentJwtExp > 0 ? (currentJwtExp - now) : 0;
            return $"id={this.GetInstanceID()}, hasJwt={!string.IsNullOrEmpty(currentJwtToken)}, jwtFp={Fingerprint(currentJwtToken)}, hasSecret={!string.IsNullOrEmpty(currentRefreshSecret)}, hasBridgeId={!string.IsNullOrEmpty(bridgeSessionId)}, exp={currentJwtExp}, now={now}, remaining={remaining}s, HasCredentials={HasCredentials}";
        }

        public void Configure(ApiClient client, UserSession session)
        {
            apiClient = client;
            userSession = session;
        }

        public void Initialize(string jwt, string bridgeId, string refreshSecret)
        {
            currentJwtToken = jwt ?? string.Empty;
            bridgeSessionId = bridgeId ?? string.Empty;
            currentRefreshSecret = refreshSecret ?? string.Empty;
            ApplyParsedExpiration(currentJwtToken, "Initialized token lifecycle. Refresh secret stored in-memory.");
            Debug.Log($"[AuthDebug] AuthTokenProvider.Initialize: {DiagnosticSummary()}");
        }

        /// <summary>
        /// Stores a still-valid access JWT without inventing refresh credentials.
        /// Used when UserSession is restored from PlayerPrefs after Play Mode / scene restart.
        /// Does not overwrite an existing refresh secret.
        /// </summary>
        public void HydrateAccessToken(string jwt)
        {
            if (string.IsNullOrWhiteSpace(jwt))
            {
                return;
            }

            if (!JwtClaimsParser.TryReadTiming(jwt, out _, out long exp) || JwtClaimsParser.IsExpiredUtc(exp))
            {
                Debug.LogWarning("[AuthTokenProvider] Refusing to hydrate an unreadable or expired access token.");
                return;
            }

            currentJwtToken = jwt;
            ApplyParsedExpiration(currentJwtToken, "Hydrated access token from restored session (no refresh secret).");
            Debug.Log($"[AuthDebug] AuthTokenProvider.HydrateAccessToken: {DiagnosticSummary()}");
        }

        public void Clear()
        {
            Debug.Log($"[AuthDebug] AuthTokenProvider.Clear: {DiagnosticSummary()}\nStack: {Environment.StackTrace}");
            currentJwtToken = string.Empty;
            currentJwtExp = 0;
            bridgeSessionId = string.Empty;
            currentRefreshSecret = string.Empty;
            isRefreshInFlight = false;
            pendingCallbacks.Clear();
            Debug.Log("[AuthTokenProvider] In-memory credentials cleared.");
        }

        public bool IsTokenExpiring(int bufferSeconds = TokenExpiryBufferSeconds)
        {
            if (string.IsNullOrWhiteSpace(currentJwtToken) || currentJwtExp <= 0)
            {
                return true;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return now >= (currentJwtExp - bufferSeconds);
        }

        /// <summary>
        /// Retrieves a guaranteed valid token. If current token is within 15 seconds of expiry or expired,
        /// performs proactive single-flight refresh. Coalesces concurrent callers.
        /// </summary>
        public IEnumerator GetValidTokenRoutine(Action<string> onTokenReady, Action<string> onError)
        {
            Debug.Log($"[AuthDebug] AuthTokenProvider.GetValidTokenRoutine called: {DiagnosticSummary()}");
            if (!HasCredentials)
            {
                // Restored gameplay sessions keep the access JWT but not the in-memory refresh secret.
                // Use the JWT until its actual exp claim; do not invent a 60s local timeout.
                if (HasAccessToken && !JwtClaimsParser.IsExpiredUtc(currentJwtExp))
                {
                    onTokenReady?.Invoke(currentJwtToken);
                    yield break;
                }

                if (HasAccessToken)
                {
                    onError?.Invoke("Session token has expired. Please sign in again.");
                    yield break;
                }

                onError?.Invoke("No authentication credentials available.");
                yield break;
            }

            if (!IsTokenExpiring(TokenExpiryBufferSeconds))
            {
                onTokenReady?.Invoke(currentJwtToken);
                yield break;
            }

            if (isRefreshInFlight)
            {
                yield return WaitForInFlightRefresh(onTokenReady, onError);
                yield break;
            }

            yield return ExecuteRefreshRoutine(onTokenReady, onError);
        }

        /// <summary>
        /// Forces an immediate token refresh (e.g. upon receiving HTTP 401 from server filter).
        /// If a refresh is already in flight, awaits its completion instead of launching a duplicate.
        /// </summary>
        public IEnumerator ForceRefreshRoutine(Action<string> onTokenReady, Action<string> onError)
        {
            Debug.Log($"[AuthDebug] AuthTokenProvider.ForceRefreshRoutine called: {DiagnosticSummary()}");
            if (!HasCredentials)
            {
                onError?.Invoke("Cannot refresh: no refresh credentials in memory. Re-login required.");
                yield break;
            }

            if (isRefreshInFlight)
            {
                yield return WaitForInFlightRefresh(onTokenReady, onError);
                yield break;
            }

            yield return ExecuteRefreshRoutine(onTokenReady, onError);
        }

        private IEnumerator WaitForInFlightRefresh(Action<string> onTokenReady, Action<string> onError)
        {
            bool completed = false;
            string resultingToken = null;
            string resultingError = null;

            pendingCallbacks.Add((token, err) =>
            {
                resultingToken = token;
                resultingError = err;
                completed = true;
            });

            while (!completed)
            {
                yield return null;
            }

            if (!string.IsNullOrWhiteSpace(resultingToken))
            {
                onTokenReady?.Invoke(resultingToken);
            }
            else
            {
                onError?.Invoke(resultingError ?? "Token refresh failed");
            }
        }

        private IEnumerator ExecuteRefreshRoutine(Action<string> onTokenReady, Action<string> onError)
        {
            isRefreshInFlight = true;
            EnsureDependencies();

            if (apiClient == null)
            {
                isRefreshInFlight = false;
                string err = "ApiClient is not available for token refresh";
                onError?.Invoke(err);
                NotifyPendingFailure(err);
                yield break;
            }

            Debug.Log("[AuthTokenProvider] Refreshing token via dev bridge with in-memory refreshSecret...");
            Debug.Log($"[AuthDebug] AuthTokenProvider.ExecuteRefreshRoutine calling bridge refresh with bridgeSessionId={bridgeSessionId}, secretPresent={!string.IsNullOrEmpty(currentRefreshSecret)}");

            string freshToken = null;
            string rotatedSecret = null;
            long expiresIn = 0;
            string failureError = null;
            long failureCode = 0;

            yield return apiClient.RefreshDevAuthBridge(
                bridgeSessionId,
                currentRefreshSecret,
                onSuccess: response =>
                {
                    freshToken = response.token;
                    rotatedSecret = response.refreshSecret;
                    expiresIn = response.expiresInSeconds;
                },
                onError: (err, code) =>
                {
                    failureError = err;
                    failureCode = code;
                }
            );

            if (!string.IsNullOrWhiteSpace(freshToken) && !string.IsNullOrWhiteSpace(rotatedSecret))
            {
                currentJwtToken = freshToken;
                currentRefreshSecret = rotatedSecret; // Credential rotated! Old secret is invalidated.

                ApplyParsedExpiration(currentJwtToken, "Token refreshed and secret rotated successfully.");
                if (currentJwtExp <= 0 && expiresIn > 0)
                {
                    currentJwtExp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + expiresIn;
                    Debug.Log($"[AuthTokenProvider] Refreshed token exp claim missing; using expiresInSeconds metadata as fallback ({expiresIn}s).");
                }

                userSession?.ApplyToken(currentJwtToken);
                Debug.Log($"[AuthTokenProvider] Token refreshed. New remaining={currentJwtExp - DateTimeOffset.UtcNow.ToUnixTimeSeconds()}s.");
                Debug.Log($"[AuthDebug] AuthTokenProvider.ExecuteRefreshRoutine success: {DiagnosticSummary()}");

                isRefreshInFlight = false;
                onTokenReady?.Invoke(currentJwtToken);
                NotifyPendingSuccess(currentJwtToken);
            }
            else
            {
                isRefreshInFlight = false;
                string finalError = failureError ?? "Token refresh failed";
                Debug.LogWarning($"[AuthTokenProvider] Token refresh rejected (HTTP {failureCode}): {finalError}");
                Debug.Log($"[AuthDebug] AuthTokenProvider.ExecuteRefreshRoutine failed (HTTP {failureCode}): {finalError}, {DiagnosticSummary()}");

                if (failureCode == 401)
                {
                    Clear();
                    userSession?.Clear();
                }

                onError?.Invoke(finalError);
                NotifyPendingFailure(finalError);
            }
        }

        private void NotifyPendingSuccess(string token)
        {
            var callbacks = new List<Action<string, string>>(pendingCallbacks);
            pendingCallbacks.Clear();
            foreach (var cb in callbacks)
            {
                try
                {
                    cb?.Invoke(token, null);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[AuthTokenProvider] Error in pending callback: {ex.Message}");
                }
            }
        }

        private void NotifyPendingFailure(string errorMessage)
        {
            var callbacks = new List<Action<string, string>>(pendingCallbacks);
            pendingCallbacks.Clear();
            foreach (var cb in callbacks)
            {
                try
                {
                    cb?.Invoke(null, errorMessage);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[AuthTokenProvider] Error in pending callback: {ex.Message}");
                }
            }
        }

        private void ApplyParsedExpiration(string jwt, string logMessage)
        {
            if (JwtClaimsParser.TryReadTiming(jwt, out long iat, out long exp))
            {
                currentJwtExp = exp;
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                long duration = (iat > 0 && exp > iat) ? (exp - iat) : 0;
                long remaining = exp - now;
                Debug.Log($"[AuthTokenProvider] {logMessage} iat={iat} exp={exp} now={now} duration={duration}s remaining={remaining}s.");
            }
            else
            {
                currentJwtExp = 0;
                Debug.LogWarning("[AuthTokenProvider] Token without parseable iat/exp claims.");
            }
        }

        private void EnsureDependencies()
        {
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
    }
}
