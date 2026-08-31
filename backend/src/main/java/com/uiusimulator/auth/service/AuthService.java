package com.uiusimulator.auth.service;

import com.uiusimulator.auth.dto.AuthLoginResponse;
import com.uiusimulator.player.dto.PlayerResponse;
import com.uiusimulator.player.service.PlayerService;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.stereotype.Service;

@Service
public class AuthService {

    private static final Logger log = LoggerFactory.getLogger(AuthService.class);

    private final PlayerService playerService;

    public AuthService(PlayerService playerService) {
        this.playerService = playerService;
    }

    public AuthLoginResponse login(Jwt jwt) {
        log.info("Authentication attempt started");

        String clerkUserId = jwt.getSubject();
        if (clerkUserId == null || clerkUserId.isBlank()) {
            throw new IllegalArgumentException("JWT subject (Clerk user id) is missing");
        }

        String email = firstNonBlank(
                jwt.getClaimAsString("email"),
                claimAsString(jwt, "primary_email_address")
        );
        String username = firstNonBlank(
                jwt.getClaimAsString("username"),
                jwt.getClaimAsString("preferred_username"),
                email,
                clerkUserId
        );

        log.info("Clerk user validated clerkUserId={}", clerkUserId);
        PlayerResponse player = playerService.findOrCreateFromClerk(clerkUserId, email, username);
        return AuthLoginResponse.of(player);
    }

    private static String claimAsString(Jwt jwt, String name) {
        Object value = jwt.getClaims().get(name);
        return value == null ? null : String.valueOf(value);
    }

    private static String firstNonBlank(String... values) {
        if (values == null) {
            return null;
        }
        for (String value : values) {
            if (value != null && !value.isBlank()) {
                return value;
            }
        }
        return null;
    }
}
