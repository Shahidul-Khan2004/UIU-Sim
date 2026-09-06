package com.uiusimulator.player.dto;

import com.uiusimulator.player.entity.Player;
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
    public static PlayerResponse from(Player player) {
        return new PlayerResponse(
                player.getId(),
                player.getClerkUserId(),
                player.getEmail(),
                player.getUsername(),
                player.getCreatedAt(),
                player.getLastLogin(),
                player.getAura(),
                player.getAcademicReputation()
        );
    }
}
