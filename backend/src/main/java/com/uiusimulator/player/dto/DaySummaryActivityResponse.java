package com.uiusimulator.player.dto;

import com.uiusimulator.player.entity.PlayerDayActivity;

public record DaySummaryActivityResponse(
        String activityId,
        String status,
        String outcome,
        int auraDelta,
        int academicReputationDelta
) {
    public static DaySummaryActivityResponse from(PlayerDayActivity activity) {
        return new DaySummaryActivityResponse(
                activity.getActivityId(),
                activity.getStatus().name(),
                activity.getOutcome(),
                activity.getAuraDelta(),
                activity.getReputationDelta()
        );
    }
}
