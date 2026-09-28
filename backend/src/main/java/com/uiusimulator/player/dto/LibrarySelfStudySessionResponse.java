package com.uiusimulator.player.dto;

import com.uiusimulator.player.entity.ActivityStatus;
import com.uiusimulator.player.entity.PlayerDayActivity;
import com.uiusimulator.player.entity.PlayerStats;
import java.time.Instant;

public record LibrarySelfStudySessionResponse(
        String activityId,
        String status,
        String outcome,
        int milestoneSeconds,
        int auraDelta,
        int reputationDelta,
        int requestedReputationDelta,
        int appliedReputationDelta,
        boolean alreadyApplied,
        boolean alreadyCompleted,
        boolean sessionActive,
        long activeElapsedMs,
        int dayNumber,
        int aura,
        int academicReputation
) {
    public static LibrarySelfStudySessionResponse of(
            PlayerDayActivity activity,
            PlayerStats stats,
            Instant now,
            int requestedReputationDelta,
            int appliedReputationDelta,
            boolean alreadyApplied
    ) {
        return of(
                activity,
                stats,
                now,
                requestedReputationDelta,
                appliedReputationDelta,
                alreadyApplied,
                activity.getStatus() == ActivityStatus.COMPLETED
        );
    }

    public static LibrarySelfStudySessionResponse of(
            PlayerDayActivity activity,
            PlayerStats stats,
            Instant now,
            int requestedReputationDelta,
            int appliedReputationDelta,
            boolean alreadyApplied,
            boolean alreadyCompleted
    ) {
        return new LibrarySelfStudySessionResponse(
                activity.getActivityId(),
                activity.getStatus().name(),
                activity.getOutcome(),
                activity.getMilestoneSeconds(),
                activity.getAuraDelta(),
                activity.getReputationDelta(),
                requestedReputationDelta,
                appliedReputationDelta,
                alreadyApplied,
                alreadyCompleted,
                activity.isSessionActive(),
                activity.computeActiveElapsedMs(now),
                activity.getDayNumber(),
                stats.getAura(),
                stats.getAcademicReputation()
        );
    }
}
