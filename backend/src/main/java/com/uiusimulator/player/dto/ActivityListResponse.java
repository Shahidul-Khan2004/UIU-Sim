package com.uiusimulator.player.dto;

import java.util.List;

public record ActivityListResponse(
        int dayNumber,
        List<ActivityStateResponse> activities
) {
    public static ActivityListResponse of(int dayNumber, List<ActivityStateResponse> activities) {
        return new ActivityListResponse(dayNumber, activities);
    }

    public static ActivityListResponse empty() {
        return new ActivityListResponse(0, List.of());
    }
}
