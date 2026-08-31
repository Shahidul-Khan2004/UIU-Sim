package com.uiusimulator.player.service;

import com.uiusimulator.player.dto.PlayerResponse;
import com.uiusimulator.player.entity.Player;
import com.uiusimulator.player.repository.PlayerRepository;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
public class PlayerService {

    private static final Logger log = LoggerFactory.getLogger(PlayerService.class);

    private final PlayerRepository playerRepository;

    public PlayerService(PlayerRepository playerRepository) {
        this.playerRepository = playerRepository;
    }

    @Transactional
    public PlayerResponse findOrCreateFromClerk(String clerkUserId, String email, String username) {
        return playerRepository.findByClerkUserId(clerkUserId)
                .map(existing -> {
                    existing.updateProfile(email, username);
                    existing.markLogin();
                    log.info("Existing player login updated for clerkUserId={}", clerkUserId);
                    return toResponse(existing);
                })
                .orElseGet(() -> {
                    Player created = Player.createNew(clerkUserId, email, username);
                    playerRepository.save(created);
                    log.info("Player profile created for clerkUserId={}", clerkUserId);
                    return toResponse(created);
                });
    }

    @Transactional(readOnly = true)
    public PlayerResponse getByClerkUserId(String clerkUserId) {
        Player player = playerRepository.findByClerkUserId(clerkUserId)
                .orElseThrow(() -> new IllegalArgumentException("Player not found for clerk user"));
        return toResponse(player);
    }

    private static PlayerResponse toResponse(Player player) {
        return new PlayerResponse(
                player.getId(),
                player.getClerkUserId(),
                player.getEmail(),
                player.getUsername(),
                player.getCreatedAt(),
                player.getLastLogin()
        );
    }
}
