package com.uiusimulator.player.dto;

import com.uiusimulator.player.entity.PlayerDayActivity;

public record ActivityStateResponse(
        String activityId,
        String status,
        String outcome,
        int auraDelta,
        int reputationDelta,
        int dayNumber
) {
    public static ActivityStateResponse from(PlayerDayActivity activity) {
        return new ActivityStateResponse(
                activity.getActivityId(),
                activity.getStatus().name(),
                activity.getOutcome(),
                activity.getAuraDelta(),
                activity.getReputationDelta(),
                activity.getDayNumber()
        );
    }
}
