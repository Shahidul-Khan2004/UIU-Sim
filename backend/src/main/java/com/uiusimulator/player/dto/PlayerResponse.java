package com.uiusimulator.player.dto;

import java.time.Instant;
import java.util.UUID;

public record PlayerResponse(
        UUID id,
        String clerkUserId,
        String email,
        String username,
        Instant createdAt,
        Instant lastLogin
) {
}
