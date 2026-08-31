package com.uiusimulator.auth.controller;

import com.uiusimulator.auth.dto.AuthLoginResponse;
import com.uiusimulator.auth.exception.AuthenticationFailedException;
import com.uiusimulator.auth.service.AuthService;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/api/auth")
public class AuthController {

    private static final Logger log = LoggerFactory.getLogger(AuthController.class);

    private final AuthService authService;

    public AuthController(AuthService authService) {
        this.authService = authService;
    }

    @PostMapping("/login")
    public ResponseEntity<AuthLoginResponse> login(@AuthenticationPrincipal Jwt jwt) {
        if (jwt == null) {
            throw new AuthenticationFailedException("Missing authentication token");
        }
        AuthLoginResponse response = authService.login(jwt);
        log.info("Authentication completed for clerkUserId={}", jwt.getSubject());
        return ResponseEntity.ok(response);
    }
}
