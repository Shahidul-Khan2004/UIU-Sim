package com.uiusimulator.assessment.controller;
import com.uiusimulator.assessment.dto.*;
import com.uiusimulator.assessment.entity.AssessmentType;
import com.uiusimulator.assessment.service.AssessmentService;
import jakarta.validation.Valid;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.web.bind.annotation.*;
import java.util.UUID;
@RestController
@RequestMapping("/api/players/me")
public class AssessmentController {
    private final AssessmentService service;
    public AssessmentController(AssessmentService service) { this.service = service; }
    @GetMapping("/report-card")
    public ReportCardResponse report(@AuthenticationPrincipal Jwt jwt) { return service.reportCard(jwt); }
    @PostMapping("/assessments/{course}/{type}/start")
    public AssessmentResponse start(@AuthenticationPrincipal Jwt jwt, @PathVariable String course, @PathVariable AssessmentType type) {
        return service.start(jwt, course, type);
    }
    @PostMapping("/assessments/{id}/answer")
    public AssessmentResponse answer(@AuthenticationPrincipal Jwt jwt, @PathVariable UUID id, @Valid @RequestBody AssessmentAnswerRequest request) {
        return service.answer(jwt, id, request);
    }
    @PostMapping("/assessments/{id}/cheat")
    public AssessmentResponse cheat(@AuthenticationPrincipal Jwt jwt, @PathVariable UUID id) { return service.resolveCheat(jwt, id); }
}
