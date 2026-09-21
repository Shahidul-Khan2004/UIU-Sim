using System;

namespace UIU.Simulator.Gameplay.Activities
{
    /// <summary>Immutable snapshot of a current-day activity result.</summary>
    public readonly struct ActivityRecord
    {
        public string ActivityId { get; }
        public ActivityStatus Status { get; }
        public string Outcome { get; }
        public int AuraDelta { get; }
        public int ReputationDelta { get; }
        public int DayNumber { get; }
        public int MilestoneSeconds { get; }

        public ActivityRecord(
            string activityId,
            ActivityStatus status,
            string outcome,
            int auraDelta,
            int reputationDelta,
            int dayNumber,
            int milestoneSeconds = 0)
        {
            ActivityId = activityId ?? string.Empty;
            Status = status;
            Outcome = outcome ?? string.Empty;
            AuraDelta = auraDelta;
            ReputationDelta = reputationDelta;
            DayNumber = dayNumber;
            MilestoneSeconds = milestoneSeconds;
        }

        public bool IsResolved => Status == ActivityStatus.Completed || Status == ActivityStatus.Missed;
    }

    /// <summary>Result of a successful activity resolve call (new or idempotent).</summary>
    public readonly struct ActivityResolveResult
    {
        public ActivityRecord Record { get; }
        public bool AlreadyResolved { get; }
        public float Aura { get; }
        public float AcademicReputation { get; }

        public ActivityResolveResult(ActivityRecord record, bool alreadyResolved, float aura, float academicReputation)
        {
            Record = record;
            AlreadyResolved = alreadyResolved;
            Aura = aura;
            AcademicReputation = academicReputation;
        }
    }

    /// <summary>Result of an ATTEND_ICS session mutation (start / milestone / leave / proxy).</summary>
    public readonly struct AttendIcsSessionResult
    {
        public readonly ActivityRecord Record;
        public readonly bool AlreadyApplied;
        public readonly bool SessionActive;
        public readonly long ActiveElapsedMs;
        public readonly int RequestedAuraDelta;
        public readonly int RequestedReputationDelta;
        public readonly int AppliedAuraDelta;
        public readonly int AppliedReputationDelta;
        public readonly float Aura;
        public readonly float AcademicReputation;

        public AttendIcsSessionResult(
            ActivityRecord record,
            bool alreadyApplied,
            bool sessionActive,
            long activeElapsedMs,
            int requestedAuraDelta,
            int requestedReputationDelta,
            int appliedAuraDelta,
            int appliedReputationDelta,
            float aura,
            float academicReputation)
        {
            Record = record;
            AlreadyApplied = alreadyApplied;
            SessionActive = sessionActive;
            ActiveElapsedMs = activeElapsedMs;
            RequestedAuraDelta = requestedAuraDelta;
            RequestedReputationDelta = requestedReputationDelta;
            AppliedAuraDelta = appliedAuraDelta;
            AppliedReputationDelta = appliedReputationDelta;
            Aura = aura;
            AcademicReputation = academicReputation;
        }
    }
}
