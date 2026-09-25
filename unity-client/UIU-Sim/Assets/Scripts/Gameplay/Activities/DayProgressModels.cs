using System;

namespace UIU.Simulator.Gameplay.Activities
{
    /// <summary>Server-authoritative daily summary payload used by DailySummaryUI.</summary>
    public readonly struct DayFinalizeResult
    {
        public readonly int Semester;
        public readonly int Day;
        public readonly DaySummaryActivity[] Activities;
        public readonly int TotalAuraDelta;
        public readonly int TotalAcademicReputationDelta;
        public readonly int Aura;
        public readonly int AcademicReputation;

        public DayFinalizeResult(
            int semester,
            int day,
            DaySummaryActivity[] activities,
            int totalAuraDelta,
            int totalAcademicReputationDelta,
            int aura,
            int academicReputation)
        {
            Semester = semester;
            Day = day;
            Activities = activities ?? Array.Empty<DaySummaryActivity>();
            TotalAuraDelta = totalAuraDelta;
            TotalAcademicReputationDelta = totalAcademicReputationDelta;
            Aura = aura;
            AcademicReputation = academicReputation;
        }
    }

    public readonly struct DaySummaryActivity
    {
        public readonly string ActivityId;
        public readonly ActivityStatus Status;
        public readonly string Outcome;
        public readonly int AuraDelta;
        public readonly int AcademicReputationDelta;
        public readonly int MarksObtained, MaxMarks;
        public readonly string AssessmentName;
        public readonly bool CourseDropped;

        public DaySummaryActivity(
            string activityId,
            ActivityStatus status,
            string outcome,
            int auraDelta,
            int academicReputationDelta, int marksObtained = 0, int maxMarks = 0, string assessmentName = null, bool courseDropped = false)
        {
            ActivityId = activityId ?? string.Empty;
            Status = status;
            Outcome = outcome ?? string.Empty;
            AuraDelta = auraDelta;
            AcademicReputationDelta = academicReputationDelta;
            MarksObtained = marksObtained; MaxMarks = maxMarks; AssessmentName = assessmentName; CourseDropped = courseDropped;
        }
    }

    public readonly struct DayAdvanceResult
    {
        public readonly int Semester;
        public readonly int CurrentDay;
        public readonly bool AlreadyAdvanced;
        public readonly bool IdCardIssued;
        public readonly int Aura;
        public readonly int AcademicReputation;

        public DayAdvanceResult(
            int semester,
            int currentDay,
            bool alreadyAdvanced,
            bool idCardIssued,
            int aura,
            int academicReputation)
        {
            Semester = semester;
            CurrentDay = currentDay;
            AlreadyAdvanced = alreadyAdvanced;
            IdCardIssued = idCardIssued;
            Aura = aura;
            AcademicReputation = academicReputation;
        }
    }
}
