package com.uiusimulator.player.dto;

import com.uiusimulator.player.entity.FacultyCoffeeOption;
import jakarta.validation.constraints.NotNull;

public record FacultyCoffeeRequest(
        @NotNull(message = "Coffee option is required.")
        FacultyCoffeeOption option
) {
}
