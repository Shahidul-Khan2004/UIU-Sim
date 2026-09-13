package com.uiusimulator.config;

import java.util.Arrays;
import java.util.List;
import org.springframework.boot.context.properties.ConfigurationProperties;

@ConfigurationProperties(prefix = "uiu.clerk")
public record ClerkProperties(
        String publishableKey,
        String secretKey,
        String issuer,
        String jwksUrl,
        String authorizedParties,
        Integer gameTokenTtlSeconds
) {
    private static final int DEFAULT_GAME_TOKEN_TTL_SECONDS = 60;

    /**
     * Returns the effective game-token TTL in seconds.
     * Falls back to 60 s (Clerk's standard lifetime) when unconfigured or non-positive.
     */
    public int effectiveGameTokenTtlSeconds() {
        return (gameTokenTtlSeconds != null && gameTokenTtlSeconds > 0)
                ? gameTokenTtlSeconds
                : DEFAULT_GAME_TOKEN_TTL_SECONDS;
    }

    public List<String> authorizedPartyList() {
        if (authorizedParties == null || authorizedParties.isBlank()) {
            return List.of();
        }
        return Arrays.stream(authorizedParties.split(","))
                .map(String::trim)
                .filter(s -> !s.isEmpty())
                .toList();
    }
}
