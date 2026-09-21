package com.uiusimulator.player.dto;

import com.uiusimulator.player.entity.PlayerRole;
import jakarta.validation.constraints.NotNull;
import java.util.UUID;

public record PlayerSaveCreateRequest(
        @NotNull(message = "role must not be null")
        PlayerRole role,

        @NotNull(message = "departmentId must not be null")
        UUID departmentId,

        String universityId
) {
}
