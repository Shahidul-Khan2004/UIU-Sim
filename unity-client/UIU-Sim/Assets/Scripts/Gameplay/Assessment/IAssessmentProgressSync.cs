using System;
namespace UIU.Simulator.Gameplay.Assessment
{
    public interface IAssessmentProgressSync
    {
        void RequestReportCard(Action<ReportCard> success, Action<string> failure);
        void RequestAssessmentStart(string course, string type, Action<AssessmentResult> success, Action<string> failure);
        void RequestAssessmentAnswer(AssessmentResult attempt, int answer, Action<AssessmentResult> success, Action<string> failure);
        void RequestAssessmentCheat(AssessmentResult attempt, Action<AssessmentResult> success, Action<string> failure);
    }
}
