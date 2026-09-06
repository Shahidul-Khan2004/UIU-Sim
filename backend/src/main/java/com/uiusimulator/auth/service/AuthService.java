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

        if (jwt == null || jwt.getSubject() == null || jwt.getSubject().isBlank()) {
            throw new IllegalArgumentException("JWT subject (Clerk user id) is missing");
        }

        log.info("Clerk user validated clerkUserId={}", jwt.getSubject());
        PlayerResponse player = playerService.login(jwt);
        return AuthLoginResponse.of(player);
    }
}
