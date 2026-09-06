package com.uiusimulator.player.service;

import com.uiusimulator.player.dto.PlayerStatsDeltaRequest;
import com.uiusimulator.player.dto.PlayerStatsResponse;
import com.uiusimulator.player.entity.Player;
import com.uiusimulator.player.repository.PlayerRepository;
import java.util.Optional;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
public class PlayerStatsService {

    private static final Logger log = LoggerFactory.getLogger(PlayerStatsService.class);

    private final PlayerService playerService;
    private final PlayerRepository playerRepository;

    public PlayerStatsService(PlayerService playerService, PlayerRepository playerRepository) {
        this.playerService = playerService;
        this.playerRepository = playerRepository;
    }

    /**
     * Applies a persistent stat delta to the authenticated player's record.
     * Concurrency-safe: acquires a pessimistic row lock (SELECT ... FOR UPDATE)
     * inside the active transaction, lazily provisions if absent, applies clamped
     * delta directly to the locked entity, persists, and commits.
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

        // 1. Acquire pessimistic write lock directly on the canonical row inside active transaction
        Optional<Player> lockedOpt = playerRepository.findByClerkUserIdWithLock(clerkUserId);
        Player lockedPlayer;

        if (lockedOpt.isPresent()) {
            lockedPlayer = lockedOpt.get();
        } else {
            // Player not yet provisioned: provision via centralized PlayerService
            if (jwt != null) {
                playerService.getOrProvisionPlayer(jwt);
            } else {
                playerService.getOrProvisionPlayer(clerkUserId, null, null);
            }

            // Acquire lock on newly provisioned row
            lockedPlayer = playerRepository.findByClerkUserIdWithLock(clerkUserId)
                    .orElseThrow(() -> new IllegalStateException("Player must exist after provisioning: " + clerkUserId));
        }

        // 2. Apply delta directly to THAT locked entity with bounds clamping [0, 100]
        int prevAura = lockedPlayer.getAura();
        int prevReputation = lockedPlayer.getAcademicReputation();
        lockedPlayer.modifyStats(request.auraDelta(), request.academicReputationDelta());

        // 3. Save and flush within active transaction
        Player saved = playerRepository.saveAndFlush(lockedPlayer);
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
