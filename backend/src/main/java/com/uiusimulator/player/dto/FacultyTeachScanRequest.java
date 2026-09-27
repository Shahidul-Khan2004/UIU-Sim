package com.uiusimulator.player.dto;

import jakarta.validation.constraints.NotBlank;

public record FacultyTeachScanRequest(
        @NotBlank(message = "Course is required.")
        String courseId
) {
}
