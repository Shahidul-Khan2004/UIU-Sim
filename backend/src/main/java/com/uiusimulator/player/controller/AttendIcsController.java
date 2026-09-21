package com.uiusimulator.player.controller;

import com.uiusimulator.player.dto.AttendIcsMilestoneRequest;
import com.uiusimulator.player.dto.AttendIcsSessionResponse;
import com.uiusimulator.player.service.AttendIcsService;
import jakarta.validation.Valid;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/api/players/me/activities/attend-ics")
public class AttendIcsController {

    private final AttendIcsService attendIcsService;

    public AttendIcsController(AttendIcsService attendIcsService) {
        this.attendIcsService = attendIcsService;
    }

    @PostMapping("/start")
    public ResponseEntity<AttendIcsSessionResponse> start(@AuthenticationPrincipal Jwt jwt) {
        return ResponseEntity.ok(attendIcsService.startLecture(jwt));
    }

    @PostMapping("/pause")
    public ResponseEntity<AttendIcsSessionResponse> pause(@AuthenticationPrincipal Jwt jwt) {
        return ResponseEntity.ok(attendIcsService.pauseLecture(jwt));
    }

    @PostMapping("/resume")
    public ResponseEntity<AttendIcsSessionResponse> resume(@AuthenticationPrincipal Jwt jwt) {
        return ResponseEntity.ok(attendIcsService.resumeLecture(jwt));
    }

    @PostMapping("/milestone")
    public ResponseEntity<AttendIcsSessionResponse> milestone(
            @AuthenticationPrincipal Jwt jwt,
            @Valid @RequestBody AttendIcsMilestoneRequest request
    ) {
        return ResponseEntity.ok(attendIcsService.claimMilestone(jwt, request));
    }

    @PostMapping("/leave-early")
    public ResponseEntity<AttendIcsSessionResponse> leaveEarly(@AuthenticationPrincipal Jwt jwt) {
        return ResponseEntity.ok(attendIcsService.leaveEarly(jwt));
    }

    @PostMapping("/proxy")
    public ResponseEntity<AttendIcsSessionResponse> proxy(@AuthenticationPrincipal Jwt jwt) {
        return ResponseEntity.ok(attendIcsService.punchProxy(jwt));
    }
}
