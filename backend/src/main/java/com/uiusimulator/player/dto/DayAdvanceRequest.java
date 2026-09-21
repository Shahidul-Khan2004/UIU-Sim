package com.uiusimulator.player.dto;

import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotNull;

public record DayAdvanceRequest(
        @NotNull(message = "expectedSemester must not be null")
        @Min(value = 1, message = "expectedSemester must be at least 1")
        Integer expectedSemester,

        @NotNull(message = "expectedDay must not be null")
        @Min(value = 1, message = "expectedDay must be at least 1")
        Integer expectedDay
) {
}
