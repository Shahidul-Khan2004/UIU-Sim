using System;
using System.Collections;
using UIU.Simulator.Gameplay.Assessment;
using UIU.Simulator.Gameplay.UI;
using UnityEngine;
namespace UIU.Simulator.Gameplay.Player
{
    public sealed partial class PlayerProgressSync : IAssessmentProgressSync
    {
        public static PlayerProgressSync Active => StatsHUD.Instance != null
            ? StatsHUD.Instance.GetComponent<PlayerProgressSync>() : FindFirstObjectByType<PlayerProgressSync>();

        public void RequestReportCard(Action<ReportCard> success, Action<string> failure)
        {
            EnsureDependencies();
            if (apiClient == null || userSession == null || !userSession.HasToken || isMutationInFlight)
            { failure?.Invoke("Progress is unavailable or another update is in progress."); return; }
            StartCoroutine(HydrateReportCardRoutine(success, failure));
        }
        private IEnumerator HydrateReportCardRoutine(Action<ReportCard> success = null, Action<string> failure = null)
        {
            EnsureDependencies();
            if (apiClient == null || userSession == null || !userSession.HasToken) yield break;
            // Shared request gate avoids a late read overwriting newer course state.
            isMutationInFlight = true;
            yield return apiClient.Get("api/players/me/report-card", userSession.JwtToken, json =>
            {
                isMutationInFlight = false;
                try
                {
                    ReportCard card = JsonUtility.FromJson<ReportCard>(json);
                    if (card == null) throw new InvalidOperationException("Empty report card.");
                    dailyActivityState?.ApplyReportCard(card);
                    success?.Invoke(card);
                }
                catch (Exception) { failure?.Invoke("Could not read the report card."); }
            }, (error, code) =>
            {
                isMutationInFlight = false;
                // Missing save before admission is expected.
                if (code != 404) failure?.Invoke(error);
                else failure?.Invoke("Complete admission to view your Report Card.");
            });
        }
        public void RequestAssessmentStart(string course, string type, Action<AssessmentResult> success, Action<string> failure)
        { RequestAssessment("api/players/me/assessments/" + Uri.EscapeDataString(course) + "/" + Uri.EscapeDataString(type) + "/start", "{}", success, failure); }
        public void RequestAssessmentAnswer(AssessmentResult attempt, int answer, Action<AssessmentResult> success, Action<string> failure)
        {
            var body = new AssessmentAnswer { questionIndex = attempt.questionIndex, answerIndex = answer };
            RequestAssessment("api/players/me/assessments/" + attempt.attemptId + "/answer", JsonUtility.ToJson(body), success, failure);
        }
        public void RequestAssessmentCheat(AssessmentResult attempt, Action<AssessmentResult> success, Action<string> failure)
        { RequestAssessment("api/players/me/assessments/" + attempt.attemptId + "/cheat", "{}", success, failure); }
        private void RequestAssessment(string path, string body, Action<AssessmentResult> success, Action<string> failure)
        {
            EnsureDependencies();
            if (!isHydrated || isMutationInFlight || apiClient == null || userSession == null || !userSession.HasToken)
            { failure?.Invoke("Progress is unavailable or another update is in progress."); return; }
            StartCoroutine(AssessmentRoutine(path, body, success, failure));
        }
        private IEnumerator AssessmentRoutine(string path, string body, Action<AssessmentResult> success, Action<string> failure)
        {
            isMutationInFlight = true;
            yield return apiClient.Post(path, body, userSession.JwtToken, json =>
            {
                isMutationInFlight = false;
                try
                {
                    var result = JsonUtility.FromJson<AssessmentResult>(json);
                    if (result == null || result.reportCard == null) throw new InvalidOperationException("Invalid assessment response.");
                    playerStats.ApplyServerState(result.aura, result.academicReputation, StatUpdateSource.GameplayMutation);
                    dailyActivityState?.ApplyReportCard(result.reportCard);
                    success?.Invoke(result);
                }
                catch (Exception) { failure?.Invoke("Could not read the result. Retry to recover the saved attempt."); }
            }, (error, code) => { isMutationInFlight = false; failure?.Invoke(error); });
        }
    }
}
