package com.uiusimulator.player.controller;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.when;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.jwt;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.patch;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.uiusimulator.auth.security.ClerkJwtAuthenticationFilter;
import com.uiusimulator.common.exception.GlobalExceptionHandler;
import com.uiusimulator.config.ClerkProperties;
import com.uiusimulator.config.SecurityConfig;
import com.uiusimulator.player.dto.PlayerResponse;
import com.uiusimulator.player.dto.PlayerStatsDeltaRequest;
import com.uiusimulator.player.dto.PlayerStatsResponse;
import com.uiusimulator.player.service.PlayerService;
import com.uiusimulator.player.service.PlayerStatsService;
import java.time.Instant;
import java.util.UUID;
import org.hamcrest.Matchers;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.context.properties.EnableConfigurationProperties;
import org.springframework.boot.test.autoconfigure.web.servlet.WebMvcTest;
import org.springframework.context.annotation.Import;
import org.springframework.http.MediaType;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.security.oauth2.jwt.JwtDecoder;
import org.springframework.test.context.ActiveProfiles;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;

@WebMvcTest(PlayerController.class)
@ActiveProfiles("test")
@EnableConfigurationProperties(ClerkProperties.class)
@Import({SecurityConfig.class, ClerkJwtAuthenticationFilter.class, GlobalExceptionHandler.class})
class PlayerControllerTest {

    @Autowired
    private MockMvc mockMvc;

    @Autowired
    private ObjectMapper objectMapper;

    @MockitoBean
    private JwtDecoder jwtDecoder;

    @MockitoBean
    private PlayerService playerService;

    @MockitoBean
    private PlayerStatsService playerStatsService;

    @Test
    void me_authenticated_returnsPlayerResponseWithStats() throws Exception {
        Instant now = Instant.parse("2026-01-01T00:00:00Z");
        PlayerResponse player = new PlayerResponse(
                UUID.randomUUID(),
                "user_me",
                "me@uiu.edu",
                "campus_hero",
                now,
                now,
                65,
                75
        );
        when(playerService.getOrProvisionPlayerResponse(any(Jwt.class))).thenReturn(player);

        mockMvc.perform(get("/api/players/me")
                        .with(jwt().jwt(j -> j.subject("user_me"))))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.clerkUserId").value("user_me"))
                .andExpect(jsonPath("$.aura").value(65))
                .andExpect(jsonPath("$.academicReputation").value(75));
    }

    @Test
    void me_unauthenticated_returns401() throws Exception {
        mockMvc.perform(get("/api/players/me"))
                .andExpect(status().isUnauthorized());
    }

    @Test
    void mutateStats_validRequest_returnsConfirmedStats() throws Exception {
        when(playerStatsService.applyStatDelta(any(Jwt.class), any(PlayerStatsDeltaRequest.class)))
                .thenReturn(new PlayerStatsResponse(55, 50));

        PlayerStatsDeltaRequest request = new PlayerStatsDeltaRequest(5, 0);

        mockMvc.perform(patch("/api/players/me/stats")
                        .with(jwt().jwt(j -> j.subject("user_me")))
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(objectMapper.writeValueAsString(request)))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.aura").value(55))
                .andExpect(jsonPath("$.academicReputation").value(50));
    }

    @Test
    void mutateStats_missingFields_returns400ValidationFailed() throws Exception {
        mockMvc.perform(patch("/api/players/me/stats")
                        .with(jwt().jwt(j -> j.subject("user_me")))
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"auraDelta\": null, \"academicReputationDelta\": 5}"))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.success").value(false))
                .andExpect(jsonPath("$.message").value(Matchers.containsString("auraDelta must not be null")));
    }

    @Test
    void mutateStats_unauthenticated_returns401() throws Exception {
        PlayerStatsDeltaRequest request = new PlayerStatsDeltaRequest(5, 0);

        mockMvc.perform(patch("/api/players/me/stats")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(objectMapper.writeValueAsString(request)))
                .andExpect(status().isUnauthorized());
    }
}
