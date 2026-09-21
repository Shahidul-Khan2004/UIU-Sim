package com.uiusimulator.player.dto;

import jakarta.validation.constraints.NotBlank;

public record ActivityResolveRequest(
        @NotBlank(message = "activityId must not be blank")
        String activityId,

        @NotBlank(message = "outcome must not be blank")
        String outcome
) {
}
