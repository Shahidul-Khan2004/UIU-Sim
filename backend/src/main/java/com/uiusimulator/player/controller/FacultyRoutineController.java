package com.uiusimulator.player.controller;

import com.uiusimulator.player.dto.FacultyRoutineResponse;
import com.uiusimulator.player.service.FacultyRoutineService;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/api/players")
public class FacultyRoutineController {

    private final FacultyRoutineService facultyRoutineService;

    public FacultyRoutineController(FacultyRoutineService facultyRoutineService) {
        this.facultyRoutineService = facultyRoutineService;
    }

    @GetMapping("/me/faculty-routine")
    public ResponseEntity<FacultyRoutineResponse> getFacultyRoutine(@AuthenticationPrincipal Jwt jwt) {
        return ResponseEntity.ok(facultyRoutineService.getFacultyRoutine(jwt));
    }
}
