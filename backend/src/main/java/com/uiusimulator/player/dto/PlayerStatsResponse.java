package com.uiusimulator.player.dto;

import com.uiusimulator.player.entity.Player;

public record PlayerStatsResponse(
        int aura,
        int academicReputation
) {
    public static PlayerStatsResponse from(Player player) {
        return new PlayerStatsResponse(player.getAura(), player.getAcademicReputation());
    }
}
