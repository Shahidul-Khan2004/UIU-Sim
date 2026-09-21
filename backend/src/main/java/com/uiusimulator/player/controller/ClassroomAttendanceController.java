package com.uiusimulator.player.controller;

import com.uiusimulator.player.dto.AttendIcsMilestoneRequest;
import com.uiusimulator.player.dto.AttendIcsSessionResponse;
import com.uiusimulator.player.service.AttendIcsService;
import jakarta.validation.Valid;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

/**
 * Shared lecture session API. {@code activityId} must be a catalogued classroom activity
 * (ATTEND_ICS, ATTEND_ENGLISH, ATTEND_DM). Rewards are server-defined.
 * The existing {@code /attend-ics} routes remain for Introduction to Computer Science.
 */
@RestController
@RequestMapping("/api/players/me/activities/classroom/{activityId}")
public class ClassroomAttendanceController {

    private final AttendIcsService attendIcsService;

    public ClassroomAttendanceController(AttendIcsService attendIcsService) {
        this.attendIcsService = attendIcsService;
    }

    @PostMapping("/start")
    public ResponseEntity<AttendIcsSessionResponse> start(
            @AuthenticationPrincipal Jwt jwt,
            @PathVariable String activityId
    ) {
        return ResponseEntity.ok(attendIcsService.startLecture(jwt, activityId));
    }

    @PostMapping("/pause")
    public ResponseEntity<AttendIcsSessionResponse> pause(
            @AuthenticationPrincipal Jwt jwt,
            @PathVariable String activityId
    ) {
        return ResponseEntity.ok(attendIcsService.pauseLecture(jwt, activityId));
    }

    @PostMapping("/resume")
    public ResponseEntity<AttendIcsSessionResponse> resume(
            @AuthenticationPrincipal Jwt jwt,
            @PathVariable String activityId
    ) {
        return ResponseEntity.ok(attendIcsService.resumeLecture(jwt, activityId));
    }

    @PostMapping("/milestone")
    public ResponseEntity<AttendIcsSessionResponse> milestone(
            @AuthenticationPrincipal Jwt jwt,
            @PathVariable String activityId,
            @Valid @RequestBody AttendIcsMilestoneRequest request
    ) {
        return ResponseEntity.ok(attendIcsService.claimMilestone(jwt, activityId, request));
    }

    @PostMapping("/leave-early")
    public ResponseEntity<AttendIcsSessionResponse> leaveEarly(
            @AuthenticationPrincipal Jwt jwt,
            @PathVariable String activityId
    ) {
        return ResponseEntity.ok(attendIcsService.leaveEarly(jwt, activityId));
    }

    @PostMapping("/proxy")
    public ResponseEntity<AttendIcsSessionResponse> proxy(
            @AuthenticationPrincipal Jwt jwt,
            @PathVariable String activityId
    ) {
        return ResponseEntity.ok(attendIcsService.punchProxy(jwt, activityId));
    }
}
