package com.uiusimulator.player.dto;

import com.uiusimulator.player.entity.PlayerDayActivity;
import com.uiusimulator.player.entity.PlayerStats;

public record ActivityResolveResponse(
        String activityId,
        String status,
        String outcome,
        int auraDelta,
        int reputationDelta,
        int dayNumber,
        boolean alreadyResolved,
        int aura,
        int academicReputation
) {
    public static ActivityResolveResponse of(
            PlayerDayActivity activity,
            PlayerStats stats,
            boolean alreadyResolved
    ) {
        return new ActivityResolveResponse(
                activity.getActivityId(),
                activity.getStatus().name(),
                activity.getOutcome(),
                activity.getAuraDelta(),
                activity.getReputationDelta(),
                activity.getDayNumber(),
                alreadyResolved,
                stats.getAura(),
                stats.getAcademicReputation()
        );
    }
}
