package com.uiusimulator.player.service;

import static org.assertj.core.api.Assertions.assertThat;

import com.uiusimulator.player.dto.PlayerResponse;
import com.uiusimulator.player.entity.Player;
import com.uiusimulator.player.repository.PlayerRepository;
import java.time.Instant;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.orm.jpa.DataJpaTest;
import org.springframework.context.annotation.Import;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.test.context.ActiveProfiles;

@DataJpaTest
@ActiveProfiles("test")
@Import(PlayerService.class)
class PlayerServiceTest {

    @Autowired
    private PlayerService playerService;

    @Autowired
    private PlayerRepository playerRepository;

    @Test
    void getOrProvisionPlayer_createsNewPlayerWithDefaultStats() {
        Jwt jwt = Jwt.withTokenValue("token")
                .header("alg", "none")
                .subject("user_lazy_new")
                .claim("email", "student@uiu.edu")
                .claim("username", "freshman")
                .build();

        Player player = playerService.getOrProvisionPlayer(jwt);

        assertThat(player.getClerkUserId()).isEqualTo("user_lazy_new");
        assertThat(player.getEmail()).isEqualTo("student@uiu.edu");
        assertThat(player.getUsername()).isEqualTo("freshman");
        assertThat(player.getAura()).isEqualTo(50);
        assertThat(player.getAcademicReputation()).isEqualTo(50);
        assertThat(player.getCreatedAt()).isNotNull();
        assertThat(player.getLastLogin()).isEqualTo(player.getCreatedAt());
        assertThat(playerRepository.findByClerkUserId("user_lazy_new")).isPresent();
    }

    @Test
    void getOrProvisionPlayer_missingOptionalClaims_persistsNullsGracefully() {
        Jwt jwt = Jwt.withTokenValue("token")
                .header("alg", "none")
                .subject("user_noclaims")
                .build();

        Player player = playerService.getOrProvisionPlayer(jwt);

        assertThat(player.getClerkUserId()).isEqualTo("user_noclaims");
        assertThat(player.getEmail()).isNull();
        assertThat(player.getUsername()).isNull();
        assertThat(player.getAura()).isEqualTo(50);
        assertThat(player.getAcademicReputation()).isEqualTo(50);
    }

    @Test
    void getOrProvisionPlayer_existingPlayer_doesNotRewriteLastLogin() {
        Instant originalLogin = Instant.parse("2026-01-01T12:00:00Z");
        Player existing = new Player(
                java.util.UUID.randomUUID(),
                "user_persisted",
                "old@uiu.edu",
                "veteran",
                originalLogin,
                originalLogin,
                70,
                80
        );
        playerRepository.saveAndFlush(existing);

        Jwt jwt = Jwt.withTokenValue("token")
                .header("alg", "none")
                .subject("user_persisted")
                .claim("email", "old@uiu.edu")
                .build();

        Player loaded = playerService.getOrProvisionPlayer(jwt);

        assertThat(loaded.getId()).isEqualTo(existing.getId());
        assertThat(loaded.getLastLogin()).isEqualTo(originalLogin);
        assertThat(loaded.getAura()).isEqualTo(70);
        assertThat(loaded.getAcademicReputation()).isEqualTo(80);
    }

    @Test
    void login_existingPlayer_updatesLastLogin() {
        Instant originalLogin = Instant.parse("2026-01-01T12:00:00Z");
        Player existing = new Player(
                java.util.UUID.randomUUID(),
                "user_login_test",
                "login@uiu.edu",
                "login_user",
                originalLogin,
                originalLogin,
                50,
                50
        );
        playerRepository.saveAndFlush(existing);

        Jwt jwt = Jwt.withTokenValue("token")
                .header("alg", "none")
                .subject("user_login_test")
                .claim("email", "login@uiu.edu")
                .build();

        PlayerResponse response = playerService.login(jwt);

        assertThat(response.id()).isEqualTo(existing.getId());
        assertThat(response.lastLogin()).isAfter(originalLogin);
    }

    @Test
    void getOrProvisionPlayerResponse_returnsCompleteProfileWithStats() {
        Jwt jwt = Jwt.withTokenValue("token")
                .header("alg", "none")
                .subject("user_profile_me")
                .claim("email", "me@uiu.edu")
                .build();

        PlayerResponse response = playerService.getOrProvisionPlayerResponse(jwt);

        assertThat(response.clerkUserId()).isEqualTo("user_profile_me");
        assertThat(response.email()).isEqualTo("me@uiu.edu");
        assertThat(response.aura()).isEqualTo(50);
        assertThat(response.academicReputation()).isEqualTo(50);
    }
}
