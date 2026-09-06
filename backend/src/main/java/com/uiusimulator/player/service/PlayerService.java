package com.uiusimulator.player.service;

import com.uiusimulator.player.dto.PlayerResponse;
import com.uiusimulator.player.entity.Player;
import com.uiusimulator.player.entity.PlayerStats;
import com.uiusimulator.player.exception.PlayerStatsNotFoundException;
import com.uiusimulator.player.repository.PlayerRepository;
import com.uiusimulator.player.repository.PlayerStatsRepository;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.dao.DataIntegrityViolationException;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
public class PlayerService {

    private static final Logger log = LoggerFactory.getLogger(PlayerService.class);

    private final PlayerRepository playerRepository;
    private final PlayerStatsRepository playerStatsRepository;

    public PlayerService(PlayerRepository playerRepository, PlayerStatsRepository playerStatsRepository) {
        this.playerRepository = playerRepository;
        this.playerStatsRepository = playerStatsRepository;
    }

    /**
     * Centralized player provisioning: returns existing player without rewriting last_login,
     * or atomically provisions a new player with default stats (Aura=50, Reputation=50) and first-contact last_login.
     */
    @Transactional
    public Player getOrProvisionPlayer(Jwt jwt) {
        String clerkUserId = jwt.getSubject();
        if (clerkUserId == null || clerkUserId.isBlank()) {
            throw new IllegalArgumentException("JWT subject (Clerk user id) is missing");
        }

        String email = extractEmail(jwt);
        String username = extractUsername(jwt);
        return getOrProvisionPlayer(clerkUserId, email, username);
    }

    @Transactional
    public Player getOrProvisionPlayer(String clerkUserId, String email, String username) {
        if (clerkUserId == null || clerkUserId.isBlank()) {
            throw new IllegalArgumentException("Clerk user id is required");
        }

        return playerRepository.findByClerkUserId(clerkUserId)
                .orElseGet(() -> {
                    try {
                        return provisionNewPlayer(clerkUserId, email, username);
                    } catch (DataIntegrityViolationException ex) {
                        log.warn("Concurrent provisioning detected for clerkUserId={}, falling back to existing player", clerkUserId);
                        return playerRepository.findByClerkUserId(clerkUserId)
                                .orElseThrow(() -> ex);
                    }
                });
    }

    private Player provisionNewPlayer(String clerkUserId, String email, String username) {
        Player created = Player.createNew(clerkUserId, email, username);
        Player saved = playerRepository.save(created);
        PlayerStats stats = PlayerStats.createDefault(saved);
        playerStatsRepository.save(stats);
        log.info("Player profile and stats atomically provisioned for clerkUserId={}", clerkUserId);
        return saved;
    }

    @Transactional
    public PlayerResponse getOrProvisionPlayerResponse(Jwt jwt) {
        // Normal profile reads do NOT rewrite last_login
        Player player = getOrProvisionPlayer(jwt);
        PlayerStats stats = playerStatsRepository.findByPlayerId(player.getId())
                .orElseThrow(() -> new PlayerStatsNotFoundException(player.getId()));
        return PlayerResponse.of(player, stats);
    }

    @Transactional(readOnly = true)
    public PlayerResponse getByClerkUserId(String clerkUserId) {
        Player player = playerRepository.findByClerkUserId(clerkUserId)
                .orElseThrow(() -> new IllegalArgumentException("Player not found for clerk user"));
        PlayerStats stats = playerStatsRepository.findByPlayerId(player.getId())
                .orElseThrow(() -> new PlayerStatsNotFoundException(player.getId()));
        return PlayerResponse.of(player, stats);
    }

    /**
     * Explicit login flow: marks last_login and updates profile if claims are present.
     */
    @Transactional
    public PlayerResponse login(Jwt jwt) {
        String clerkUserId = jwt.getSubject();
        if (clerkUserId == null || clerkUserId.isBlank()) {
            throw new IllegalArgumentException("JWT subject (Clerk user id) is missing");
        }

        String email = extractEmail(jwt);
        String username = extractUsername(jwt);

        Player player = playerRepository.findByClerkUserId(clerkUserId)
                .map(existing -> {
                    existing.updateProfile(email, username);
                    existing.markLogin();
                    log.info("Existing player login recorded for clerkUserId={}", clerkUserId);
                    return existing;
                })
                .orElseGet(() -> {
                    Player created = provisionNewPlayer(clerkUserId, email, username);
                    log.info("New player created during login for clerkUserId={}", clerkUserId);
                    return created;
                });

        PlayerStats stats = playerStatsRepository.findByPlayerId(player.getId())
                .orElseThrow(() -> new PlayerStatsNotFoundException(player.getId()));

        return PlayerResponse.of(player, stats);
    }

    @Transactional
    public PlayerResponse findOrCreateFromClerk(String clerkUserId, String email, String username) {
        Player player = playerRepository.findByClerkUserId(clerkUserId)
                .map(existing -> {
                    existing.updateProfile(email, username);
                    existing.markLogin();
                    log.info("Existing player login updated for clerkUserId={}", clerkUserId);
                    return existing;
                })
                .orElseGet(() -> {
                    Player created = provisionNewPlayer(clerkUserId, email, username);
                    log.info("Player profile created for clerkUserId={}", clerkUserId);
                    return created;
                });

        PlayerStats stats = playerStatsRepository.findByPlayerId(player.getId())
                .orElseThrow(() -> new PlayerStatsNotFoundException(player.getId()));

        return PlayerResponse.of(player, stats);
    }

    public static String extractEmail(Jwt jwt) {
        if (jwt == null) {
            return null;
        }
        return firstNonBlank(
                jwt.getClaimAsString("email"),
                claimAsString(jwt, "primary_email_address")
        );
    }

    public static String extractUsername(Jwt jwt) {
        if (jwt == null) {
            return null;
        }
        return firstNonBlank(
                jwt.getClaimAsString("username"),
                jwt.getClaimAsString("preferred_username")
        );
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
