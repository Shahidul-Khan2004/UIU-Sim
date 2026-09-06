package com.uiusimulator.player.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.uiusimulator.player.dto.PlayerStatsDeltaRequest;
import com.uiusimulator.player.dto.PlayerStatsResponse;
import com.uiusimulator.player.entity.Player;
import com.uiusimulator.player.repository.PlayerRepository;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.Callable;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.orm.jpa.DataJpaTest;
import org.springframework.context.annotation.Import;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.test.context.ActiveProfiles;
import org.springframework.transaction.PlatformTransactionManager;
import org.springframework.transaction.support.TransactionTemplate;

@DataJpaTest
@ActiveProfiles("test")
@Import({PlayerService.class, PlayerStatsService.class})
class PlayerStatsServiceTest {

    @Autowired
    private PlayerStatsService playerStatsService;

    @Autowired
    private PlayerService playerService;

    @Autowired
    private PlayerRepository playerRepository;

    @Autowired
    private PlatformTransactionManager transactionManager;

    @Test
    void applyStatDelta_positiveDelta_updatesAuraAndReputation() {
        Jwt jwt = jwtWith("user_stat_pos");
        PlayerStatsDeltaRequest request = new PlayerStatsDeltaRequest(5, 10);

        PlayerStatsResponse response = playerStatsService.applyStatDelta(jwt, request);

        assertThat(response.aura()).isEqualTo(55);
        assertThat(response.academicReputation()).isEqualTo(60);

        Player saved = playerRepository.findByClerkUserId("user_stat_pos").orElseThrow();
        assertThat(saved.getAura()).isEqualTo(55);
        assertThat(saved.getAcademicReputation()).isEqualTo(60);
    }

    @Test
    void applyStatDelta_negativeDelta_decreasesStats() {
        Jwt jwt = jwtWith("user_stat_neg");
        PlayerStatsDeltaRequest request = new PlayerStatsDeltaRequest(-10, -5);

        PlayerStatsResponse response = playerStatsService.applyStatDelta(jwt, request);

        assertThat(response.aura()).isEqualTo(40);
        assertThat(response.academicReputation()).isEqualTo(45);

        Player saved = playerRepository.findByClerkUserId("user_stat_neg").orElseThrow();
        assertThat(saved.getAura()).isEqualTo(40);
        assertThat(saved.getAcademicReputation()).isEqualTo(45);
    }

    @Test
    void applyStatDelta_clampsAbove100() {
        Jwt jwt = jwtWith("user_stat_clamp_high");
        PlayerStatsDeltaRequest request = new PlayerStatsDeltaRequest(90, 70);

        PlayerStatsResponse response = playerStatsService.applyStatDelta(jwt, request);

        assertThat(response.aura()).isEqualTo(100);
        assertThat(response.academicReputation()).isEqualTo(100);

        Player saved = playerRepository.findByClerkUserId("user_stat_clamp_high").orElseThrow();
        assertThat(saved.getAura()).isEqualTo(100);
        assertThat(saved.getAcademicReputation()).isEqualTo(100);
    }

    @Test
    void applyStatDelta_clampsBelowZero() {
        Jwt jwt = jwtWith("user_stat_clamp_low");
        PlayerStatsDeltaRequest request = new PlayerStatsDeltaRequest(-80, -90);

        PlayerStatsResponse response = playerStatsService.applyStatDelta(jwt, request);

        assertThat(response.aura()).isEqualTo(0);
        assertThat(response.academicReputation()).isEqualTo(0);

        Player saved = playerRepository.findByClerkUserId("user_stat_clamp_low").orElseThrow();
        assertThat(saved.getAura()).isEqualTo(0);
        assertThat(saved.getAcademicReputation()).isEqualTo(0);
    }

    @Test
    void applyStatDelta_provisionsMissingPlayerFirst() {
        Jwt jwt = jwtWith("user_lazy_mutation");
        assertThat(playerRepository.findByClerkUserId("user_lazy_mutation")).isEmpty();

        PlayerStatsDeltaRequest request = new PlayerStatsDeltaRequest(5, 0);
        PlayerStatsResponse response = playerStatsService.applyStatDelta(jwt, request);

        assertThat(response.aura()).isEqualTo(55);
        assertThat(response.academicReputation()).isEqualTo(50);
        assertThat(playerRepository.findByClerkUserId("user_lazy_mutation")).isPresent();
    }

    @Test
    @org.springframework.transaction.annotation.Transactional(propagation = org.springframework.transaction.annotation.Propagation.NOT_SUPPORTED)
    void applyStatDelta_concurrentUpdates_produceExactSum() throws Exception {
        String clerkUserId = "user_concurrent";
        TransactionTemplate txTemplate = new TransactionTemplate(transactionManager);
        txTemplate.executeWithoutResult(status -> {
            Player p = Player.createNew(clerkUserId, "concur@uiu.edu", "concur_user");
            playerRepository.saveAndFlush(p);
        });

        try {
            int threadCount = 10;
            ExecutorService executor = Executors.newFixedThreadPool(threadCount);
            List<Callable<PlayerStatsResponse>> tasks = new ArrayList<>();

            for (int i = 0; i < threadCount; i++) {
                tasks.add(() -> {
                    Jwt jwt = jwtWith(clerkUserId);
                    PlayerStatsDeltaRequest req = new PlayerStatsDeltaRequest(2, 1);
                    return playerStatsService.applyStatDelta(jwt, req);
                });
            }

            List<Future<PlayerStatsResponse>> futures = executor.invokeAll(tasks);
            for (Future<PlayerStatsResponse> f : futures) {
                f.get();
            }
            executor.shutdown();

            Player finalPlayer = playerRepository.findByClerkUserId(clerkUserId).orElseThrow();
            // Initial: 50 + 10 * 2 = 70 Aura
            // Initial: 50 + 10 * 1 = 60 Reputation
            assertThat(finalPlayer.getAura()).isEqualTo(70);
            assertThat(finalPlayer.getAcademicReputation()).isEqualTo(60);
        } finally {
            txTemplate.executeWithoutResult(status ->
                    playerRepository.findByClerkUserId(clerkUserId).ifPresent(playerRepository::delete)
            );
        }
    }

    @Test
    void applyStatDelta_nullDeltas_throwException() {
        Jwt jwt = jwtWith("user_bad_delta");
        PlayerStatsDeltaRequest req = new PlayerStatsDeltaRequest(null, 5);

        assertThatThrownBy(() -> playerStatsService.applyStatDelta(jwt, req))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessageContaining("Stat deltas must not be null");
    }

    private static Jwt jwtWith(String subject) {
        return Jwt.withTokenValue("token")
                .header("alg", "none")
                .subject(subject)
                .build();
    }
}
