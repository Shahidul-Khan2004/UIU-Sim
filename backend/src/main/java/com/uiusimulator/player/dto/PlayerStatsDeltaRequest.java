package com.uiusimulator.player.dto;

import jakarta.validation.constraints.NotNull;

public record PlayerStatsDeltaRequest(
        @NotNull(message = "auraDelta must not be null")
        Integer auraDelta,

        @NotNull(message = "academicReputationDelta must not be null")
        Integer academicReputationDelta
) {
}
