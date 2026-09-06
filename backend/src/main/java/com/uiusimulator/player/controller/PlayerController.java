package com.uiusimulator.player.controller;

import com.uiusimulator.player.dto.PlayerResponse;
import com.uiusimulator.player.dto.PlayerStatsDeltaRequest;
import com.uiusimulator.player.dto.PlayerStatsResponse;
import com.uiusimulator.player.service.PlayerService;
import com.uiusimulator.player.service.PlayerStatsService;
import jakarta.validation.Valid;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PatchMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/api/players")
public class PlayerController {

    private final PlayerService playerService;
    private final PlayerStatsService playerStatsService;

    public PlayerController(PlayerService playerService, PlayerStatsService playerStatsService) {
        this.playerService = playerService;
        this.playerStatsService = playerStatsService;
    }

    @GetMapping("/me")
    public ResponseEntity<PlayerResponse> me(@AuthenticationPrincipal Jwt jwt) {
        return ResponseEntity.ok(playerService.getOrProvisionPlayerResponse(jwt));
    }

    @PatchMapping("/me/stats")
    public ResponseEntity<PlayerStatsResponse> mutateStats(
            @AuthenticationPrincipal Jwt jwt,
            @Valid @RequestBody PlayerStatsDeltaRequest request
    ) {
        return ResponseEntity.ok(playerStatsService.applyStatDelta(jwt, request));
    }
}
