package com.uiusimulator.advisor.service;

import com.uiusimulator.advisor.model.AdvisorStudentContext;
import com.uiusimulator.advisor.model.StudentStandingBand;
import com.uiusimulator.player.entity.Player;
import com.uiusimulator.player.entity.PlayerStats;
import com.uiusimulator.player.exception.PlayerStatsNotFoundException;
import com.uiusimulator.player.repository.PlayerStatsRepository;
import com.uiusimulator.player.service.PlayerService;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;
import org.springframework.security.oauth2.jwt.Jwt;

import java.time.Instant;
import java.util.Map;
import java.util.Optional;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

@ExtendWith(MockitoExtension.class)
class AdvisorStudentContextResolverTest {

    @Mock
    private PlayerService playerService;

    @Mock
    private PlayerStatsRepository playerStatsRepository;

    private AdvisorStudentContextResolver resolver;

    private Player testPlayer;
    private PlayerStats testStats;
    private Jwt testJwt;

    @BeforeEach
    void setUp() {
        resolver = new AdvisorStudentContextResolver(playerService, playerStatsRepository);

        testPlayer = Player.createNew("user_clerk_123", "student@uiu.ac.bd", "test_student");
        testStats = new PlayerStats(testPlayer, 25, 85); // Aura=25 (LOW), Reputation=85 (HIGH)

        testJwt = new Jwt(
                "mock-token",
                Instant.now(),
                Instant.now().plusSeconds(3600),
                Map.of("alg", "none"),
                Map.of("sub", "user_clerk_123", "email", "student@uiu.ac.bd")
        );
    }

    @Test
    void resolveStudentContext_resolvesPlayerAndStats_constructsImmutableContext() {
        when(playerService.getOrProvisionPlayer(testJwt)).thenReturn(testPlayer);
        when(playerStatsRepository.findByPlayerId(testPlayer.getId())).thenReturn(Optional.of(testStats));

        AdvisorStudentContext context = resolver.resolveStudentContext(testJwt);

        assertNotNull(context);
        assertEquals(25, context.aura());
        assertEquals(85, context.academicReputation());
        assertEquals(StudentStandingBand.LOW, context.auraBand());
        assertEquals(StudentStandingBand.HIGH, context.academicBand());

        // Verify read-only database access
        verify(playerService).getOrProvisionPlayer(testJwt);
        verify(playerStatsRepository).findByPlayerId(testPlayer.getId());
        verify(playerStatsRepository, never()).save(any());
    }

    @Test
    void resolveStudentContext_missingStats_throwsPlayerStatsNotFoundException() {
        when(playerService.getOrProvisionPlayer(testJwt)).thenReturn(testPlayer);
        when(playerStatsRepository.findByPlayerId(testPlayer.getId())).thenReturn(Optional.empty());

        assertThrows(PlayerStatsNotFoundException.class, () -> resolver.resolveStudentContext(testJwt));
    }

    @Test
    void resolveStudentContext_nullOrEmptyJwtSubject_throwsIllegalArgumentException() {
        Jwt invalidJwt = new Jwt(
                "mock-token",
                Instant.now(),
                Instant.now().plusSeconds(3600),
                Map.of("alg", "none"),
                Map.of("sub", "")
        );

        assertThrows(IllegalArgumentException.class, () -> resolver.resolveStudentContext(invalidJwt));
        assertThrows(IllegalArgumentException.class, () -> resolver.resolveStudentContext(null));
    }
}
