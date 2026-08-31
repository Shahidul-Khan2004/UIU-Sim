package com.uiusimulator.player.controller;

import com.uiusimulator.player.dto.PlayerResponse;
import com.uiusimulator.player.service.PlayerService;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/api/players")
public class PlayerController {

    private final PlayerService playerService;

    public PlayerController(PlayerService playerService) {
        this.playerService = playerService;
    }

    @GetMapping("/me")
    public ResponseEntity<PlayerResponse> me(@AuthenticationPrincipal Jwt jwt) {
        return ResponseEntity.ok(playerService.getByClerkUserId(jwt.getSubject()));
    }
}
