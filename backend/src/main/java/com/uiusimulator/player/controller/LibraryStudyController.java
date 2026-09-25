package com.uiusimulator.player.controller;

import com.uiusimulator.player.dto.LibraryStudyCompleteRequest;
import com.uiusimulator.player.dto.LibraryStudyResponse;
import com.uiusimulator.player.service.LibraryStudyService;
import jakarta.validation.Valid;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

/**
 * Optional once-per-day Library Study. The client submits a normalized score.
 * Academic Reputation is calculated and applied only on the server.
 */
@RestController
@RequestMapping("/api/players/me/activities/library-study")
public class LibraryStudyController {

    private final LibraryStudyService libraryStudyService;

    public LibraryStudyController(LibraryStudyService libraryStudyService) {
        this.libraryStudyService = libraryStudyService;
    }

    @PostMapping("/start")
    public ResponseEntity<LibraryStudyResponse> start(@AuthenticationPrincipal Jwt jwt) {
        return ResponseEntity.ok(libraryStudyService.start(jwt));
    }

    @PostMapping("/complete")
    public ResponseEntity<LibraryStudyResponse> complete(
            @AuthenticationPrincipal Jwt jwt,
            @Valid @RequestBody LibraryStudyCompleteRequest request
    ) {
        return ResponseEntity.ok(libraryStudyService.complete(jwt, request));
    }
}
