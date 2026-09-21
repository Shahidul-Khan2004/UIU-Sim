package com.uiusimulator.player.dto;

import com.fasterxml.jackson.annotation.JsonInclude;

@JsonInclude(JsonInclude.Include.NON_NULL)
public record PlayerSaveStatusResponse(
        boolean hasSave,
        PlayerSaveResponse save
) {
    public static PlayerSaveStatusResponse none() {
        return new PlayerSaveStatusResponse(false, null);
    }

    public static PlayerSaveStatusResponse of(PlayerSaveResponse save) {
        return new PlayerSaveStatusResponse(true, save);
    }
}
