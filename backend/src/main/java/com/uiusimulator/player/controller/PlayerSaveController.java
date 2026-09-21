package com.uiusimulator.player.controller;

import com.uiusimulator.player.dto.PlayerSaveCreateRequest;
import com.uiusimulator.player.dto.PlayerSaveStatusResponse;
import com.uiusimulator.player.service.PlayerSaveService;
import jakarta.validation.Valid;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/api/players")
public class PlayerSaveController {

    private final PlayerSaveService playerSaveService;

    public PlayerSaveController(PlayerSaveService playerSaveService) {
        this.playerSaveService = playerSaveService;
    }

    @GetMapping("/me/save")
    public ResponseEntity<PlayerSaveStatusResponse> getSave(@AuthenticationPrincipal Jwt jwt) {
        return ResponseEntity.ok(playerSaveService.getSaveStatus(jwt));
    }

    @PostMapping("/me/save")
    public ResponseEntity<PlayerSaveStatusResponse> createSave(
            @AuthenticationPrincipal Jwt jwt,
            @Valid @RequestBody PlayerSaveCreateRequest request
    ) {
        return ResponseEntity.status(HttpStatus.CREATED)
                .body(playerSaveService.createSave(jwt, request));
    }

    @DeleteMapping("/me/save")
    public ResponseEntity<PlayerSaveStatusResponse> deleteSave(@AuthenticationPrincipal Jwt jwt) {
        return ResponseEntity.ok(playerSaveService.deleteSave(jwt));
    }
}
