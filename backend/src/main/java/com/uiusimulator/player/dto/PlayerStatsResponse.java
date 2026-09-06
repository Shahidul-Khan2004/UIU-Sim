package com.uiusimulator.player.dto;

import com.uiusimulator.player.entity.PlayerStats;

public record PlayerStatsResponse(
        int aura,
        int academicReputation
) {
    public static PlayerStatsResponse from(PlayerStats stats) {
        return new PlayerStatsResponse(stats.getAura(), stats.getAcademicReputation());
    }
}
