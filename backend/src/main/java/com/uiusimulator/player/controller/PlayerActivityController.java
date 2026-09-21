package com.uiusimulator.player.controller;

import com.uiusimulator.player.dto.ActivityListResponse;
import com.uiusimulator.player.dto.ActivityResolveRequest;
import com.uiusimulator.player.dto.ActivityResolveResponse;
import com.uiusimulator.player.service.PlayerActivityService;
import jakarta.validation.Valid;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/api/players")
public class PlayerActivityController {

    private final PlayerActivityService playerActivityService;

    public PlayerActivityController(PlayerActivityService playerActivityService) {
        this.playerActivityService = playerActivityService;
    }

    @GetMapping("/me/activities")
    public ResponseEntity<ActivityListResponse> listActivities(@AuthenticationPrincipal Jwt jwt) {
        return ResponseEntity.ok(playerActivityService.listCurrentDayActivities(jwt));
    }

    @PostMapping("/me/activities/resolve")
    public ResponseEntity<ActivityResolveResponse> resolveActivity(
            @AuthenticationPrincipal Jwt jwt,
            @Valid @RequestBody ActivityResolveRequest request
    ) {
        return ResponseEntity.ok(playerActivityService.resolveActivity(jwt, request));
    }
}
