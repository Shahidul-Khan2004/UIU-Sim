package com.uiusimulator.player.dto;

import jakarta.validation.constraints.Max;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotNull;

public record LibraryStudyCompleteRequest(
        @NotNull(message = "score must not be null")
        @Min(value = 0, message = "score must be between 0 and 100")
        @Max(value = 100, message = "score must be between 0 and 100")
        Integer score
) {
}
