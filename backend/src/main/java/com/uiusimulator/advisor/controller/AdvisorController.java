package com.uiusimulator.advisor.controller;

import com.uiusimulator.advisor.dto.AdvisorChatRequest;
import com.uiusimulator.advisor.dto.AdvisorChatResponse;
import com.uiusimulator.advisor.service.AdvisorService;
import jakarta.validation.Valid;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/api/advisor")
public class AdvisorController {

    private final AdvisorService advisorService;

    public AdvisorController(AdvisorService advisorService) {
        this.advisorService = advisorService;
    }

    @PostMapping("/chat")
    public ResponseEntity<AdvisorChatResponse> chat(
            @AuthenticationPrincipal Jwt jwt,
            @Valid @RequestBody AdvisorChatRequest request
    ) {
        return ResponseEntity.ok(advisorService.chat(jwt, request));
    }
}
