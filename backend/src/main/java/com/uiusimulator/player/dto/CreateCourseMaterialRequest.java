package com.uiusimulator.player.dto;

import jakarta.validation.constraints.NotBlank;

public record CreateCourseMaterialRequest(
        @NotBlank(message = "Please provide required information.")
        String title,

        @NotBlank(message = "Please provide required information.")
        String url
) {
}
