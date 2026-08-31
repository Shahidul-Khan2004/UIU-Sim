package com.uiusimulator.auth.controller;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

import com.uiusimulator.auth.dto.AuthLoginResponse;
import com.uiusimulator.auth.exception.AuthenticationFailedException;
import com.uiusimulator.auth.service.AuthService;
import com.uiusimulator.player.dto.PlayerResponse;
import java.time.Instant;
import java.util.UUID;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.security.oauth2.jwt.Jwt;

@ExtendWith(MockitoExtension.class)
class AuthControllerTest {

    @Mock
    private AuthService authService;

    private AuthController authController;

    @BeforeEach
    void setUp() {
        authController = new AuthController(authService);
    }

    @Test
    void login_successfulAuthentication_returnsPlayer() {
        Instant now = Instant.parse("2026-01-01T00:00:00Z");
        PlayerResponse player = new PlayerResponse(
                UUID.fromString("11111111-1111-1111-1111-111111111111"),
                "user_abc",
                "player@uiu.edu",
                "campus-explorer",
                now,
                now
        );
        when(authService.login(any(Jwt.class))).thenReturn(AuthLoginResponse.of(player));

        Jwt jwt = Jwt.withTokenValue("token")
                .header("alg", "none")
                .subject("user_abc")
                .claim("email", "player@uiu.edu")
                .build();

        ResponseEntity<AuthLoginResponse> response = authController.login(jwt);

        assertThat(response.getStatusCode()).isEqualTo(HttpStatus.OK);
        assertThat(response.getBody()).isNotNull();
        assertThat(response.getBody().success()).isTrue();
        assertThat(response.getBody().player().clerkUserId()).isEqualTo("user_abc");
        verify(authService).login(jwt);
    }

    @Test
    void login_unauthorizedRequest_throws() {
        assertThatThrownBy(() -> authController.login(null))
                .isInstanceOf(AuthenticationFailedException.class)
                .hasMessageContaining("Missing authentication");
    }
}
