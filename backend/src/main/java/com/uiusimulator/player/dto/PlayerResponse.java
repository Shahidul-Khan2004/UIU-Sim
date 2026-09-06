package com.uiusimulator.player.dto;

import com.uiusimulator.player.entity.Player;
import com.uiusimulator.player.entity.PlayerStats;
import java.time.Instant;
import java.util.UUID;

public record PlayerResponse(
        UUID id,
        String clerkUserId,
        String email,
        String username,
        Instant createdAt,
        Instant lastLogin,
        int aura,
        int academicReputation
) {
    public static PlayerResponse of(Player player, PlayerStats stats) {
        return new PlayerResponse(
                player.getId(),
                player.getClerkUserId(),
                player.getEmail(),
                player.getUsername(),
                player.getCreatedAt(),
                player.getLastLogin(),
                stats.getAura(),
                stats.getAcademicReputation()
        );
    }
}
