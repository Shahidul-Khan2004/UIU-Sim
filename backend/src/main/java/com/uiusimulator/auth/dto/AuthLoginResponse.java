package com.uiusimulator.auth.dto;

import com.uiusimulator.player.dto.PlayerResponse;

public record AuthLoginResponse(
        boolean success,
        PlayerResponse player
) {
    public static AuthLoginResponse of(PlayerResponse player) {
        return new AuthLoginResponse(true, player);
    }
}
