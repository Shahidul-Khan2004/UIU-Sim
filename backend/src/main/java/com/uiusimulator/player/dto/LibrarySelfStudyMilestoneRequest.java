package com.uiusimulator.player.dto;

import jakarta.validation.constraints.NotNull;

public record LibrarySelfStudyMilestoneRequest(
        @NotNull(message = "milestoneSeconds must not be null")
        Integer milestoneSeconds
) {
}
