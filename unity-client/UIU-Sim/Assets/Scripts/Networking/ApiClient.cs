using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace UIU.Simulator.Networking
{
    /// <summary>
    /// HTTP client for the Spring Boot API. Attaches Bearer JWT automatically for authenticated calls.
    /// </summary>
    public sealed class ApiClient : MonoBehaviour
    {
        [SerializeField] private string backendBaseUrl = "http://localhost:8080";

        public string BackendBaseUrl
        {
            get => backendBaseUrl.TrimEnd('/');
            set => backendBaseUrl = value;
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
        }

        public IEnumerator PollDevAuthBridge(
            string sessionId,
            float timeoutSeconds,
            float intervalSeconds,
            Action<string> onToken,
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
                            onToken?.Invoke(dto.token);
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
            string url = $"{BackendBaseUrl}/{relativePath.TrimStart('/')}";
            using UnityWebRequest request = UnityWebRequest.Get(url);
            if (!string.IsNullOrWhiteSpace(jwtToken))
            {
                request.SetRequestHeader("Authorization", $"Bearer {jwtToken}");
            }

            request.SetRequestHeader("Accept", "application/json");
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(ExtractErrorMessage(request));
                yield break;
            }

            onSuccess?.Invoke(request.downloadHandler.text);
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
