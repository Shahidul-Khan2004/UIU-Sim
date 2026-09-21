package com.uiusimulator.player.dto;

import com.uiusimulator.player.entity.PlayerDayActivity;
import com.uiusimulator.player.entity.PlayerStats;
import java.time.Instant;

public record AttendIcsSessionResponse(
        String activityId,
        String status,
        String outcome,
        int milestoneSeconds,
        int auraDelta,
        int reputationDelta,
        int requestedAuraDelta,
        int requestedReputationDelta,
        int appliedAuraDelta,
        int appliedReputationDelta,
        boolean alreadyApplied,
        boolean sessionActive,
        long activeElapsedMs,
        int dayNumber,
        int aura,
        int academicReputation
) {
    public static AttendIcsSessionResponse of(
            PlayerDayActivity activity,
            PlayerStats stats,
            Instant now,
            int requestedAuraDelta,
            int requestedReputationDelta,
            int appliedAuraDelta,
            int appliedReputationDelta,
            boolean alreadyApplied
    ) {
        return new AttendIcsSessionResponse(
                activity.getActivityId(),
                activity.getStatus().name(),
                activity.getOutcome(),
                activity.getMilestoneSeconds(),
                activity.getAuraDelta(),
                activity.getReputationDelta(),
                requestedAuraDelta,
                requestedReputationDelta,
                appliedAuraDelta,
                appliedReputationDelta,
                alreadyApplied,
                activity.isSessionActive(),
                activity.computeActiveElapsedMs(now),
                activity.getDayNumber(),
                stats.getAura(),
                stats.getAcademicReputation()
        );
    }
}
