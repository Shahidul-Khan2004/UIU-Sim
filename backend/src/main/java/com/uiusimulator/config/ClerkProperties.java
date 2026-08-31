package com.uiusimulator.config;

import java.util.Arrays;
import java.util.List;
import org.springframework.boot.context.properties.ConfigurationProperties;

@ConfigurationProperties(prefix = "uiu.clerk")
public record ClerkProperties(
        String publishableKey,
        String issuer,
        String jwksUrl,
        String authorizedParties
) {
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
