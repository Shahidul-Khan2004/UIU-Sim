package com.uiusimulator.player.dto;

import com.uiusimulator.player.entity.PlayerRole;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import java.util.UUID;

public record PlayerSaveCreateRequest(
        @NotNull(message = "role must not be null")
        PlayerRole role,

        @NotBlank(message = "playerName must not be blank")
        String playerName,

        @NotNull(message = "departmentId must not be null")
        UUID departmentId,

        @NotBlank(message = "universityId must not be blank")
        String universityId
) {
}
