package com.uiusimulator.player.service;

import com.uiusimulator.player.dto.PlayerStatsDeltaRequest;
import com.uiusimulator.player.dto.PlayerStatsResponse;
import com.uiusimulator.player.entity.Player;
import com.uiusimulator.player.entity.PlayerStats;
import com.uiusimulator.player.exception.PlayerStatsNotFoundException;
import com.uiusimulator.player.repository.PlayerStatsRepository;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
public class PlayerStatsService {

    private static final Logger log = LoggerFactory.getLogger(PlayerStatsService.class);

    private final PlayerService playerService;
    private final PlayerStatsRepository playerStatsRepository;

    public PlayerStatsService(PlayerService playerService, PlayerStatsRepository playerStatsRepository) {
        this.playerService = playerService;
        this.playerStatsRepository = playerStatsRepository;
    }

    /**
     * Applies a persistent stat delta to the authenticated player's record.
     * Concurrency-safe: resolves player, acquires a pessimistic row lock (SELECT ... FOR UPDATE)
     * directly on the mutable player_stats row inside active transaction, applies clamped
     * delta directly to that locked entity, persists, and commits.
     */
    @Transactional
    public PlayerStatsResponse applyStatDelta(Jwt jwt, PlayerStatsDeltaRequest request) {
        String clerkUserId = jwt.getSubject();
        if (clerkUserId == null || clerkUserId.isBlank()) {
            throw new IllegalArgumentException("JWT subject (Clerk user id) is missing");
        }

        return applyStatDelta(clerkUserId, request, jwt);
    }

    @Transactional
    public PlayerStatsResponse applyStatDelta(String clerkUserId, PlayerStatsDeltaRequest request, Jwt jwt) {
        if (clerkUserId == null || clerkUserId.isBlank()) {
            throw new IllegalArgumentException("Clerk user id is required");
        }
        if (request.auraDelta() == null || request.academicReputationDelta() == null) {
            throw new IllegalArgumentException("Stat deltas must not be null");
        }

        // 1. Resolve or provision player identity
        Player player;
        if (jwt != null) {
            player = playerService.getOrProvisionPlayer(jwt);
        } else {
            player = playerService.getOrProvisionPlayer(clerkUserId, null, null);
        }

        // 2. Lock mutable player_stats row with PESSIMISTIC_WRITE
        PlayerStats lockedStats = playerStatsRepository.findByPlayerIdWithLock(player.getId())
                .orElseThrow(() -> {
                    log.error("Data integrity error: player_stats missing for playerId={}, clerkUserId={}",
                            player.getId(), clerkUserId);
                    return new PlayerStatsNotFoundException(player.getId());
                });

        // 3. Apply delta directly to THAT locked entity with bounds clamping [0, 100]
        int prevAura = lockedStats.getAura();
        int prevReputation = lockedStats.getAcademicReputation();
        lockedStats.modifyStats(request.auraDelta(), request.academicReputationDelta());

        // 4. Save and flush within active transaction
        PlayerStats saved = playerStatsRepository.saveAndFlush(lockedStats);
        log.info(
                "Stat delta applied for clerkUserId={}: Aura {} -> {} (delta {}), Reputation {} -> {} (delta {})",
                clerkUserId,
                prevAura,
                saved.getAura(),
                request.auraDelta(),
                prevReputation,
                saved.getAcademicReputation(),
                request.academicReputationDelta()
        );

        return PlayerStatsResponse.from(saved);
    }
}
