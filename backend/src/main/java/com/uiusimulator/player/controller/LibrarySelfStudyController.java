package com.uiusimulator.player.controller;

import com.uiusimulator.player.dto.LibrarySelfStudyMilestoneRequest;
import com.uiusimulator.player.dto.LibrarySelfStudySessionResponse;
import com.uiusimulator.player.service.LibrarySelfStudyService;
import jakarta.validation.Valid;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

/**
 * Optional once-per-day Library Self Study. Academic Reputation is calculated
 * and applied only on the server from active study time.
 */
@RestController
@RequestMapping("/api/players/me/activities/library-self-study")
public class LibrarySelfStudyController {

    private final LibrarySelfStudyService librarySelfStudyService;

    public LibrarySelfStudyController(LibrarySelfStudyService librarySelfStudyService) {
        this.librarySelfStudyService = librarySelfStudyService;
    }

    @PostMapping("/start")
    public ResponseEntity<LibrarySelfStudySessionResponse> start(@AuthenticationPrincipal Jwt jwt) {
        return ResponseEntity.ok(librarySelfStudyService.start(jwt));
    }

    @PostMapping("/pause")
    public ResponseEntity<LibrarySelfStudySessionResponse> pause(@AuthenticationPrincipal Jwt jwt) {
        return ResponseEntity.ok(librarySelfStudyService.pause(jwt));
    }

    @PostMapping("/abandon")
    public ResponseEntity<LibrarySelfStudySessionResponse> abandon(@AuthenticationPrincipal Jwt jwt) {
        return ResponseEntity.ok(librarySelfStudyService.abandon(jwt));
    }

    @PostMapping("/resume")
    public ResponseEntity<LibrarySelfStudySessionResponse> resume(@AuthenticationPrincipal Jwt jwt) {
        return ResponseEntity.ok(librarySelfStudyService.resume(jwt));
    }

    @PostMapping("/milestone")
    public ResponseEntity<LibrarySelfStudySessionResponse> milestone(
            @AuthenticationPrincipal Jwt jwt,
            @Valid @RequestBody LibrarySelfStudyMilestoneRequest request
    ) {
        return ResponseEntity.ok(librarySelfStudyService.claimMilestone(jwt, request));
    }
}
