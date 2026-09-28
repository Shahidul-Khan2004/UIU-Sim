using System;
using System.Collections;
using UIU.Simulator.Gameplay.Activities;
using UIU.Simulator.Gameplay.UI;
using UIU.Simulator.Networking;
using UnityEngine;

namespace UIU.Simulator.Gameplay.Player
{
    public sealed partial class PlayerProgressSync
    {
        public void RequestLibrarySelfStudyStart(Action<LibrarySelfStudySessionResult> onSuccess, Action onFailure)
        {
            StartLibrarySelfStudyMutation(LibrarySelfStudyStartPath, "{}", onSuccess, onFailure);
        }

        public void RequestLibrarySelfStudyPause(Action<LibrarySelfStudySessionResult> onSuccess, Action onFailure)
        {
            StartLibrarySelfStudyMutation(LibrarySelfStudyPausePath, "{}", onSuccess, onFailure, requireMutationSlot: false);
        }

        public void RequestLibrarySelfStudyAbandon(Action<LibrarySelfStudySessionResult> onSuccess, Action onFailure)
        {
            StartLibrarySelfStudyMutation(LibrarySelfStudyAbandonPath, "{}", onSuccess, onFailure, requireMutationSlot: false);
        }

        public void RequestLibrarySelfStudyResume(Action<LibrarySelfStudySessionResult> onSuccess, Action onFailure)
        {
            StartLibrarySelfStudyMutation(LibrarySelfStudyResumePath, "{}", onSuccess, onFailure, requireMutationSlot: false);
        }

        public void RequestLibrarySelfStudyMilestone(
            int milestoneSeconds,
            Action<LibrarySelfStudySessionResult> onSuccess,
            Action onFailure)
        {
            string body = JsonUtility.ToJson(new ApiClient.AttendIcsMilestoneRequestDto(milestoneSeconds));
            StartLibrarySelfStudyMutation(LibrarySelfStudyMilestonePath, body, onSuccess, onFailure);
        }

        public bool ApplyLibrarySelfStudyResponseForTesting(string json)
        {
            EnsureDependencies();
            return TryApplyLibrarySelfStudyJson(json, out _);
        }

        private void StartLibrarySelfStudyMutation(
            string path,
            string jsonBody,
            Action<LibrarySelfStudySessionResult> onSuccess,
            Action onFailure,
            bool requireMutationSlot = true)
        {
            if (!isHydrated)
            {
                SystemNotificationUI.Show("Player progress is still loading.");
                onFailure?.Invoke();
                return;
            }

            if (requireMutationSlot && isMutationInFlight)
            {
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

            StartCoroutine(LibrarySelfStudyMutationRoutine(path, jsonBody, onSuccess, onFailure, requireMutationSlot));
        }

        private IEnumerator LibrarySelfStudyMutationRoutine(
            string path,
            string jsonBody,
            Action<LibrarySelfStudySessionResult> onSuccess,
            Action onFailure,
            bool requireMutationSlot)
        {
            if (requireMutationSlot)
            {
                isMutationInFlight = true;
            }

            yield return apiClient.Post(
                path,
                string.IsNullOrEmpty(jsonBody) ? "{}" : jsonBody,
                userSession.JwtToken,
                onSuccess: json =>
                {
                    if (requireMutationSlot)
                    {
                        isMutationInFlight = false;
                    }

                    if (!TryApplyLibrarySelfStudyJson(json, out LibrarySelfStudySessionResult result))
                    {
                        Debug.LogError("[PlayerProgressSync] Failed to parse library self-study response. Local state unchanged.");
                        onFailure?.Invoke();
                        return;
                    }

                    onSuccess?.Invoke(result);
                },
                onError: (error, code) =>
                {
                    if (requireMutationSlot)
                    {
                        isMutationInFlight = false;
                    }

                    string userMessage = ClassifyErrorMessage(code, error);
                    Debug.LogWarning($"[PlayerProgressSync] Library self-study failed (HTTP {code}): {error}. Local state unchanged.");
                    SystemNotificationUI.Show(userMessage);
                    OnSyncFailed?.Invoke(userMessage);
                    onFailure?.Invoke();
                }
            );
        }

        private bool TryApplyLibrarySelfStudyJson(string json, out LibrarySelfStudySessionResult result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            ApiClient.LibrarySelfStudySessionResponseDto response;
            try
            {
                response = JsonUtility.FromJson<ApiClient.LibrarySelfStudySessionResponseDto>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayerProgressSync] Failed to parse library self-study response: {ex.Message}");
                return false;
            }

            if (response == null || string.IsNullOrWhiteSpace(response.activityId))
            {
                return false;
            }

            ActivityRecord record = new ActivityRecord(
                response.activityId,
                ParseActivityStatus(response.status),
                response.outcome,
                response.auraDelta,
                response.reputationDelta,
                response.dayNumber,
                response.milestoneSeconds);

            dailyActivityState?.ApplyServerActivity(record);
            if (playerStats != null)
            {
                StatUpdateSource source = response.alreadyApplied || response.appliedReputationDelta == 0
                    ? StatUpdateSource.InitialHydration
                    : StatUpdateSource.GameplayMutation;
                playerStats.ApplyServerState(response.aura, response.academicReputation, source);
            }

            result = new LibrarySelfStudySessionResult(
                record,
                response.appliedReputationDelta,
                response.alreadyApplied,
                response.alreadyCompleted,
                response.sessionActive,
                response.activeElapsedMs,
                response.aura,
                response.academicReputation);
            return true;
        }
    }
}
