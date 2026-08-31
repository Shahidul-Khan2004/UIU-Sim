package com.uiusimulator.auth.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

import com.uiusimulator.auth.dto.AuthLoginResponse;
import com.uiusimulator.player.dto.PlayerResponse;
import com.uiusimulator.player.service.PlayerService;
import java.time.Instant;
import java.util.UUID;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;
import org.springframework.security.oauth2.jwt.Jwt;

@ExtendWith(MockitoExtension.class)
class AuthServiceTest {

    @Mock
    private PlayerService playerService;

    private AuthService authService;

    @BeforeEach
    void setUp() {
        authService = new AuthService(playerService);
    }

    @Test
    void login_validClerkUser_returnsPlayer() {
        Jwt jwt = jwtWith("user_abc", "player@uiu.edu", "campus-explorer");
        PlayerResponse player = samplePlayer("user_abc", "player@uiu.edu", "campus-explorer");
        when(playerService.findOrCreateFromClerk("user_abc", "player@uiu.edu", "campus-explorer"))
                .thenReturn(player);

        AuthLoginResponse response = authService.login(jwt);

        assertThat(response.success()).isTrue();
        assertThat(response.player().clerkUserId()).isEqualTo("user_abc");
        verify(playerService).findOrCreateFromClerk("user_abc", "player@uiu.edu", "campus-explorer");
    }

    @Test
    void login_missingSubject_throws() {
        Jwt jwt = Jwt.withTokenValue("token")
                .header("alg", "none")
                .claim("email", "x@y.z")
                .build();

        assertThatThrownBy(() -> authService.login(jwt))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessageContaining("subject");
    }

    @Test
    void login_delegatesNewPlayerCreation() {
        Jwt jwt = jwtWith("user_new", "new@uiu.edu", "newbie");
        PlayerResponse created = samplePlayer("user_new", "new@uiu.edu", "newbie");
        when(playerService.findOrCreateFromClerk(eq("user_new"), eq("new@uiu.edu"), eq("newbie")))
                .thenReturn(created);

        AuthLoginResponse response = authService.login(jwt);

        assertThat(response.player().id()).isEqualTo(created.id());
        verify(playerService).findOrCreateFromClerk("user_new", "new@uiu.edu", "newbie");
    }

    @Test
    void login_existingPlayer_usesReturnedProfile() {
        Jwt jwt = jwtWith("user_old", "old@uiu.edu", "veteran");
        PlayerResponse existing = samplePlayer("user_old", "old@uiu.edu", "veteran");
        when(playerService.findOrCreateFromClerk("user_old", "old@uiu.edu", "veteran"))
                .thenReturn(existing);

        AuthLoginResponse response = authService.login(jwt);

        assertThat(response.player()).isEqualTo(existing);
    }

    private static Jwt jwtWith(String subject, String email, String username) {
        return Jwt.withTokenValue("token")
                .header("alg", "none")
                .subject(subject)
                .claim("email", email)
                .claim("username", username)
                .build();
    }

    private static PlayerResponse samplePlayer(String clerkUserId, String email, String username) {
        Instant now = Instant.parse("2026-01-01T00:00:00Z");
        return new PlayerResponse(UUID.randomUUID(), clerkUserId, email, username, now, now);
    }
}
