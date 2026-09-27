package com.uiusimulator.player.dto;

import java.time.Instant;

public record FacultyCourseProgressItem(
        String courseCode,
        boolean completed,
        Instant completedAt
) {
}
