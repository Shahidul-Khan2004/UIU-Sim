using System;
using System.Collections;
using System.Text;
using UIU.Simulator.Authentication;
using UnityEngine;
using UnityEngine.Networking;

namespace UIU.Simulator.Networking
{
    /// <summary>
    /// HTTP client for the Spring Boot API. Attaches Bearer JWT automatically for authenticated calls.
    /// Interacts with AuthTokenProvider to ensure proactive token validity and single-flight 401 retry.
    /// </summary>
    public sealed class ApiClient : MonoBehaviour
    {
        [SerializeField] private string backendBaseUrl = "http://localhost:8080";
        [SerializeField] private AuthTokenProvider authTokenProvider;

        public string BackendBaseUrl
        {
            get => backendBaseUrl.TrimEnd('/');
            set => backendBaseUrl = value;
        }

        public void ConfigureAuth(AuthTokenProvider provider)
        {
            authTokenProvider = provider;
        }

        private void Awake()
        {
            if (authTokenProvider == null)
            {
                authTokenProvider = GetComponent<AuthTokenProvider>();
            }
        }

        [Serializable]
        public class PlayerDto
        {
            public string id;
            public string clerkUserId;
            public string email;
            public string username;
            public string createdAt;
            public string lastLogin;
            public int aura;
            public int academicReputation;
        }

        [Serializable]
        public class PlayerStatsResponseDto
        {
            public int aura;
            public int academicReputation;
        }

        [Serializable]
        public class PlayerStatsDeltaRequestDto
        {
            public int auraDelta;
            public int academicReputationDelta;

            public PlayerStatsDeltaRequestDto(int auraDelta, int academicReputationDelta)
            {
                this.auraDelta = auraDelta;
                this.academicReputationDelta = academicReputationDelta;
            }
        }

        [Serializable]
        public class AuthLoginResponseDto
        {
            public bool success;
            public PlayerDto player;
        }

        [Serializable]
        public class ApiErrorDto
        {
            public bool success;
            public string message;
            public string timestamp;
            public string path;
        }

        [Serializable]
        public class DevBridgePollDto
        {
            public bool success;
            public bool ready;
            public string token;
            public string refreshSecret;
            public string bridgeSessionId;
        }

        [Serializable]
        public class DevBridgeRefreshRequestDto
        {
            public string bridgeSessionId;
            public string refreshSecret;

            public DevBridgeRefreshRequestDto(string bridgeSessionId, string refreshSecret)
            {
                this.bridgeSessionId = bridgeSessionId;
                this.refreshSecret = refreshSecret;
            }
        }

        [Serializable]
        public class DevBridgeRefreshResponseDto
        {
            public bool success;
            public string token;
            public string refreshSecret;
            public long expiresInSeconds;
        }

        public IEnumerator PollDevAuthBridge(
            string sessionId,
            float timeoutSeconds,
            float intervalSeconds,
            Action<string, string, string> onHandshake,
            Action<string> onError)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                onError?.Invoke("Missing auth session id");
                yield break;
            }

            float elapsed = 0f;
            string url = $"{BackendBaseUrl}/auth/dev/bridge/{Uri.EscapeDataString(sessionId)}";

            while (elapsed < timeoutSeconds)
            {
                using (UnityWebRequest request = UnityWebRequest.Get(url))
                {
                    request.SetRequestHeader("Accept", "application/json");
                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        DevBridgePollDto dto = null;
                        try
                        {
                            dto = JsonUtility.FromJson<DevBridgePollDto>(request.downloadHandler.text);
                        }
                        catch (Exception ex)
                        {
                            onError?.Invoke($"Bad bridge response: {ex.Message}");
                            yield break;
                        }

                        if (dto != null && dto.ready && !string.IsNullOrWhiteSpace(dto.token))
                        {
                            onHandshake?.Invoke(dto.token, dto.refreshSecret, dto.bridgeSessionId ?? sessionId);
                            yield break;
                        }
                    }
                    else if (request.responseCode >= 500)
                    {
                        onError?.Invoke(ExtractErrorMessage(request));
                        yield break;
                    }
                }

                yield return new WaitForSecondsRealtime(intervalSeconds);
                elapsed += intervalSeconds;
            }

            onError?.Invoke("Timed out waiting for browser sign-in");
        }

        public IEnumerator PollDevAuthBridge(
            string sessionId,
            float timeoutSeconds,
            float intervalSeconds,
            Action<string> onToken,
            Action<string> onError)
        {
            return PollDevAuthBridge(sessionId, timeoutSeconds, intervalSeconds, (tok, _, _) => onToken?.Invoke(tok), onError);
        }

        public IEnumerator RefreshDevAuthBridge(
            string bridgeSessionId,
            string refreshSecret,
            Action<DevBridgeRefreshResponseDto> onSuccess,
            Action<string, long> onError)
        {
            string url = $"{BackendBaseUrl}/auth/dev/bridge/refresh";
            string json = JsonUtility.ToJson(new DevBridgeRefreshRequestDto(bridgeSessionId, refreshSecret));
            Debug.Log($"[AuthDebug] ApiClient.RefreshDevAuthBridge: sending POST to {url} for bridgeSessionId={bridgeSessionId}, secretPresent={!string.IsNullOrEmpty(refreshSecret)}");

            using UnityWebRequest request = new UnityWebRequest(url, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");

            yield return request.SendWebRequest();
            Debug.Log($"[AuthDebug] ApiClient.RefreshDevAuthBridge response: code={request.responseCode}, result={request.result}");

            if (request.result != UnityWebRequest.Result.Success)
            {
                long code = request.responseCode;
                string message = ExtractErrorMessage(request);
                onError?.Invoke(message, code);
                yield break;
            }

            DevBridgeRefreshResponseDto response;
            try
            {
                response = JsonUtility.FromJson<DevBridgeRefreshResponseDto>(request.downloadHandler.text);
            }
            catch (Exception ex)
            {
                onError?.Invoke($"Failed to parse refresh response: {ex.Message}", request.responseCode);
                yield break;
            }

            if (response == null || !response.success || string.IsNullOrWhiteSpace(response.token))
            {
                onError?.Invoke("Refresh response was invalid", request.responseCode);
                yield break;
            }

            onSuccess?.Invoke(response);
        }

        public IEnumerator LoginWithBearerToken(
            string jwtToken,
            Action<PlayerDto> onSuccess,
            Action<string> onError)
        {
            if (string.IsNullOrWhiteSpace(jwtToken))
            {
                onError?.Invoke("JWT token is empty");
                yield break;
            }

            string url = $"{BackendBaseUrl}/api/auth/login";
            using UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(Array.Empty<byte>());
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", $"Bearer {jwtToken}");
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string message = ExtractErrorMessage(request);
                Debug.LogError($"[ApiClient] Login failed: HTTP {(int)request.responseCode} {message}");
                onError?.Invoke(message);
                yield break;
            }

            string body = request.downloadHandler.text;
            AuthLoginResponseDto response;
            try
            {
                response = JsonUtility.FromJson<AuthLoginResponseDto>(body);
            }
            catch (Exception ex)
            {
                onError?.Invoke($"Failed to parse login response: {ex.Message}");
                yield break;
            }

            if (response == null || !response.success || response.player == null)
            {
                onError?.Invoke("Login response was unsuccessful");
                yield break;
            }

            onSuccess?.Invoke(response.player);
        }

        public IEnumerator Get(
            string relativePath,
            string jwtToken,
            Action<string> onSuccess,
            Action<string> onError)
        {
            yield return Get(relativePath, jwtToken, onSuccess, (err, _) => onError?.Invoke(err));
        }

        public IEnumerator Get(
            string relativePath,
            string jwtToken,
            Action<string> onSuccess,
            Action<string, long> onError)
        {
            string url = $"{BackendBaseUrl}/{relativePath.TrimStart('/')}";
            yield return ExecuteRequestWithAuthRetry(
                token =>
                {
                    UnityWebRequest req = UnityWebRequest.Get(url);
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        req.SetRequestHeader("Authorization", $"Bearer {token}");
                    }
                    req.SetRequestHeader("Accept", "application/json");
                    return req;
                },
                jwtToken,
                $"GET {relativePath}",
                onSuccess,
                onError
            );
        }

        public IEnumerator Patch(
            string relativePath,
            string jsonBody,
            string jwtToken,
            Action<string> onSuccess,
            Action<string, long> onError)
        {
            string url = $"{BackendBaseUrl}/{relativePath.TrimStart('/')}";
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody ?? string.Empty);

            yield return ExecuteRequestWithAuthRetry(
                token =>
                {
                    UnityWebRequest req = new UnityWebRequest(url, "PATCH");
                    req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    req.downloadHandler = new DownloadHandlerBuffer();
                    req.SetRequestHeader("Content-Type", "application/json");
                    req.SetRequestHeader("Accept", "application/json");
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        req.SetRequestHeader("Authorization", $"Bearer {token}");
                    }
                    return req;
                },
                jwtToken,
                $"PATCH {relativePath}",
                onSuccess,
                onError
            );
        }

        public IEnumerator Post(
            string relativePath,
            string jsonBody,
            Action<string> onSuccess,
            Action<string, long> onError)
        {
            return Post(relativePath, jsonBody, null, onSuccess, onError);
        }

        public IEnumerator Post(
            string relativePath,
            string jsonBody,
            string jwtToken,
            Action<string> onSuccess,
            Action<string, long> onError)
        {
            string url = $"{BackendBaseUrl}/{relativePath.TrimStart('/')}";
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody ?? string.Empty);

            yield return ExecuteRequestWithAuthRetry(
                token =>
                {
                    UnityWebRequest req = new UnityWebRequest(url, "POST");
                    req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    req.downloadHandler = new DownloadHandlerBuffer();
                    req.SetRequestHeader("Content-Type", "application/json");
                    req.SetRequestHeader("Accept", "application/json");
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        req.SetRequestHeader("Authorization", $"Bearer {token}");
                    }
                    return req;
                },
                jwtToken,
                $"POST {relativePath}",
                onSuccess,
                onError
            );
        }

        private IEnumerator ExecuteRequestWithAuthRetry(
            Func<string, UnityWebRequest> requestFactory,
            string initialToken,
            string actionDescription,
            Action<string> onSuccess,
            Action<string, long> onError)
        {
            string effectiveToken = initialToken;
            Debug.Log($"[AuthDebug] ApiClient.ExecuteRequestWithAuthRetry start: clientID={this.GetInstanceID()}, action={actionDescription}, initialFp={AuthTokenProvider.Fingerprint(initialToken)}, providerNull={authTokenProvider == null}, providerDetails={(authTokenProvider != null ? authTokenProvider.DiagnosticSummary() : "null")}");

            if (authTokenProvider != null && authTokenProvider.HasCredentials)
            {
                string resolvedToken = null;
                string resolveErr = null;
                yield return authTokenProvider.GetValidTokenRoutine(
                    t => resolvedToken = t,
                    e => resolveErr = e
                );

                if (!string.IsNullOrWhiteSpace(resolvedToken))
                {
                    effectiveToken = resolvedToken;
                }
                else if (string.IsNullOrWhiteSpace(effectiveToken))
                {
                    onError?.Invoke(resolveErr ?? "Authentication expired. Please log in again.", 401);
                    yield break;
                }
            }
            Debug.Log($"[AuthDebug] ApiClient.ExecuteRequestWithAuthRetry token resolved: effectiveFp={AuthTokenProvider.Fingerprint(effectiveToken)}");

            using (UnityWebRequest req = requestFactory(effectiveToken))
            {
                yield return req.SendWebRequest();
                Debug.Log($"[AuthDebug] ApiClient.ExecuteRequestWithAuthRetry response: action={actionDescription}, code={req.responseCode}, result={req.result}");

                if (req.result == UnityWebRequest.Result.Success)
                {
                    onSuccess?.Invoke(req.downloadHandler.text);
                    yield break;
                }

                // Strictly one-time retry on HTTP 401 only
                if (req.responseCode == 401)
                {
                    bool hasProv = authTokenProvider != null;
                    bool hasCreds = hasProv && authTokenProvider.HasCredentials;
                    Debug.Log($"[AuthDebug] ApiClient 401 condition check on {actionDescription}: providerPresent={hasProv}, hasCreds={hasCreds}, providerDetails={(hasProv ? authTokenProvider.DiagnosticSummary() : "null")}");

                    if (hasProv && hasCreds)
                    {
                        Debug.Log($"[ApiClient] HTTP 401 received on {actionDescription}. Refreshing token and retrying once...");
                        string refreshedToken = null;
                        string refreshErr = null;

                        yield return authTokenProvider.ForceRefreshRoutine(
                            t => refreshedToken = t,
                            e => refreshErr = e
                        );

                        if (!string.IsNullOrWhiteSpace(refreshedToken))
                        {
                            Debug.Log($"[AuthDebug] ApiClient retrying {actionDescription} with new token fp={AuthTokenProvider.Fingerprint(refreshedToken)}");
                            using (UnityWebRequest retryReq = requestFactory(refreshedToken))
                            {
                                yield return retryReq.SendWebRequest();
                                Debug.Log($"[AuthDebug] ApiClient retry response for {actionDescription}: code={retryReq.responseCode}, result={retryReq.result}");

                                if (retryReq.result == UnityWebRequest.Result.Success)
                                {
                                    onSuccess?.Invoke(retryReq.downloadHandler.text);
                                    yield break;
                                }

                                onError?.Invoke(ExtractErrorMessage(retryReq), retryReq.responseCode);
                                yield break;
                            }
                        }
                    }
                }

                // Non-401 failure (timeout, network, 5xx): NEVER retried for gameplay mutations
                onError?.Invoke(ExtractErrorMessage(req), req.responseCode);
            }
        }

        private static string ExtractErrorMessage(UnityWebRequest request)
        {
            string body = request.downloadHandler != null ? request.downloadHandler.text : null;
            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    ApiErrorDto error = JsonUtility.FromJson<ApiErrorDto>(body);
                    if (error != null && !string.IsNullOrWhiteSpace(error.message))
                    {
                        return error.message;
                    }
                }
                catch
                {
                    // fall through
                }

                if (body.Length < 200)
                {
                    return body;
                }
            }

            if (!string.IsNullOrWhiteSpace(request.error))
            {
                return request.error;
            }

            return $"HTTP {(int)request.responseCode}";
        }
    }
}
