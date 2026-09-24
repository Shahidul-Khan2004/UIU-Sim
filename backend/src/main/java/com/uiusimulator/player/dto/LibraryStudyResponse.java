package com.uiusimulator.player.dto;

import com.uiusimulator.player.entity.PlayerDayActivity;
import com.uiusimulator.player.entity.PlayerStats;

public record LibraryStudyResponse(
        String activityId,
        String status,
        String outcome,
        Integer score,
        int auraDelta,
        int reputationDelta,
        int appliedReputationDelta,
        boolean alreadyStarted,
        boolean alreadyCompleted,
        int dayNumber,
        int aura,
        int academicReputation
) {
    public static LibraryStudyResponse of(
            PlayerDayActivity activity,
            PlayerStats stats,
            int appliedReputationDelta,
            boolean alreadyStarted,
            boolean alreadyCompleted
    ) {
        return new LibraryStudyResponse(
                activity.getActivityId(),
                activity.getStatus().name(),
                activity.getOutcome(),
                activity.getNormalizedScore(),
                activity.getAuraDelta(),
                activity.getReputationDelta(),
                appliedReputationDelta,
                alreadyStarted,
                alreadyCompleted,
                activity.getDayNumber(),
                stats.getAura(),
                stats.getAcademicReputation()
        );
    }
}
