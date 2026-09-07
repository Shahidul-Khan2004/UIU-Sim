using System;
using System.Collections.Generic;
using UIU.Simulator.Authentication;
using UIU.Simulator.Networking;
using UnityEngine;

namespace UIU.Simulator.Gameplay.Advisor
{
    /// <summary>
    /// Communicates with the backend POST /api/advisor/chat endpoint.
    /// Interacts strictly through ApiClient, knowing nothing of Clerk token internals, JWTs, or secrets.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AdvisorClient : MonoBehaviour
    {
        [SerializeField] private ApiClient apiClient;

        [Serializable]
        public class AdvisorHistoryMessageDto
        {
            public string role;
            public string content;

            public AdvisorHistoryMessageDto() { }

            public AdvisorHistoryMessageDto(string role, string content)
            {
                this.role = role;
                this.content = content;
            }
        }

        [Serializable]
        public class AdvisorChatRequestDto
        {
            public string message;
            public List<AdvisorHistoryMessageDto> history = new List<AdvisorHistoryMessageDto>();
            public bool initialGreeting;

            public AdvisorChatRequestDto() { }

            public AdvisorChatRequestDto(string message, List<AdvisorHistoryMessageDto> history, bool initialGreeting)
            {
                this.message = message;
                this.history = history != null ? new List<AdvisorHistoryMessageDto>(history) : new List<AdvisorHistoryMessageDto>();
                this.initialGreeting = initialGreeting;
            }
        }

        [Serializable]
        public class AdvisorChatResponseDto
        {
            public bool success;
            public bool inScope;
            public string reply;
        }

        private void Awake()
        {
            EnsureApiClient();
        }

        public void Configure(ApiClient client)
        {
            apiClient = client;
        }

        private void EnsureApiClient()
        {
            if (apiClient == null)
            {
                if (AuthHost.Instance != null)
                {
                    apiClient = AuthHost.Instance.ApiClient;
                }
                else if (Application.isPlaying)
                {
                    AuthHost host = AuthHost.EnsureExists();
                    if (host != null)
                    {
                        apiClient = host.ApiClient;
                    }
                }
            }
        }

        public void SendChat(
            AdvisorChatRequestDto request,
            Action<AdvisorChatResponseDto> onSuccess,
            Action<string, long> onError)
        {
            EnsureApiClient();

            if (apiClient == null)
            {
                onError?.Invoke("Backend API client is not available.", 0);
                return;
            }

            string jsonBody = JsonUtility.ToJson(request);
            StartCoroutine(apiClient.Post(
                "api/advisor/chat",
                jsonBody,
                onSuccess: json =>
                {
                    try
                    {
                        AdvisorChatResponseDto responseDto = JsonUtility.FromJson<AdvisorChatResponseDto>(json);
                        if (responseDto != null && responseDto.success)
                        {
                            onSuccess?.Invoke(responseDto);
                        }
                        else
                        {
                            onError?.Invoke("Received invalid advisor response.", 200);
                        }
                    }
                    catch (Exception ex)
                    {
                        onError?.Invoke($"Failed to parse advisor response: {ex.Message}", 200);
                    }
                },
                onError: (err, statusCode) =>
                {
                    onError?.Invoke(err, statusCode);
                }
            ));
        }
    }
}
