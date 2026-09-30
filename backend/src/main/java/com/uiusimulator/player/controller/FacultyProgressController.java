package com.uiusimulator.player.controller;

import com.uiusimulator.player.dto.FacultyCoffeeRequest;
import com.uiusimulator.player.dto.FacultyProgressResponse;
import com.uiusimulator.player.dto.FacultyTeachScanRequest;
import com.uiusimulator.player.service.FacultyProgressService;
import com.uiusimulator.player.service.FacultyTeachService;
import jakarta.validation.Valid;
import org.springframework.http.MediaType;
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
public class FacultyProgressController {

    private final FacultyProgressService facultyProgressService;
    private final FacultyTeachService facultyTeachService;

    public FacultyProgressController(
            FacultyProgressService facultyProgressService,
            FacultyTeachService facultyTeachService
    ) {
        this.facultyProgressService = facultyProgressService;
        this.facultyTeachService = facultyTeachService;
    }

    @GetMapping("/me/faculty-progress")
    public ResponseEntity<FacultyProgressResponse> getFacultyProgress(@AuthenticationPrincipal Jwt jwt) {
        return ResponseEntity.ok(facultyProgressService.getProgress(jwt));
    }

    @PostMapping("/me/faculty-progress/computer")
    public ResponseEntity<FacultyProgressResponse> useFacultyComputer(@AuthenticationPrincipal Jwt jwt) {
        return ResponseEntity.ok(facultyProgressService.useComputer(jwt));
    }

    @PostMapping(
            value = "/me/faculty-progress/coffee",
            consumes = MediaType.APPLICATION_JSON_VALUE
    )
    public ResponseEntity<FacultyProgressResponse> claimFacultyCoffee(
            @AuthenticationPrincipal Jwt jwt,
            @Valid @RequestBody FacultyCoffeeRequest request
    ) {
        return ResponseEntity.ok(facultyProgressService.claimCoffee(jwt, request.option()));
    }

    @PostMapping(
            value = "/me/faculty-teach/scan",
            consumes = MediaType.APPLICATION_JSON_VALUE
    )
    public ResponseEntity<FacultyProgressResponse> scanFacultyClassroom(
            @AuthenticationPrincipal Jwt jwt,
            @Valid @RequestBody FacultyTeachScanRequest request
    ) {
        return ResponseEntity.ok(facultyTeachService.scanClassroom(jwt, request.courseId()));
    }

    @PostMapping("/me/faculty-teach/complete")
    public ResponseEntity<FacultyProgressResponse> completeFacultyLecture(@AuthenticationPrincipal Jwt jwt) {
        return ResponseEntity.ok(facultyTeachService.completeLecture(jwt));
    }

    @PostMapping("/me/faculty-teach/leave")
    public ResponseEntity<FacultyProgressResponse> leaveFacultyLecture(@AuthenticationPrincipal Jwt jwt) {
        return ResponseEntity.ok(facultyTeachService.leaveLecture(jwt));
    }
}
