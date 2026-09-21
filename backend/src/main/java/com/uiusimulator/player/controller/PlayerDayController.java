package com.uiusimulator.player.controller;

import com.uiusimulator.player.dto.DayAdvanceRequest;
import com.uiusimulator.player.dto.DayAdvanceResponse;
import com.uiusimulator.player.dto.DayFinalizeResponse;
import com.uiusimulator.player.service.PlayerDayService;
import jakarta.validation.Valid;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/api/players")
public class PlayerDayController {

    private final PlayerDayService playerDayService;

    public PlayerDayController(PlayerDayService playerDayService) {
        this.playerDayService = playerDayService;
    }

    @PostMapping("/me/day/finalize")
    public ResponseEntity<DayFinalizeResponse> finalizeDay(@AuthenticationPrincipal Jwt jwt) {
        return ResponseEntity.ok(playerDayService.finalizeCurrentDay(jwt));
    }

    @PostMapping("/me/day/advance")
    public ResponseEntity<DayAdvanceResponse> advanceDay(
            @AuthenticationPrincipal Jwt jwt,
            @Valid @RequestBody DayAdvanceRequest request
    ) {
        return ResponseEntity.ok(playerDayService.advanceDay(jwt, request));
    }
}
