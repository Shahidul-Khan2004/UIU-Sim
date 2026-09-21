package com.uiusimulator.player.controller;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.when;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.jwt;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.uiusimulator.auth.security.ClerkJwtAuthenticationFilter;
import com.uiusimulator.common.exception.GlobalExceptionHandler;
import com.uiusimulator.config.ClerkProperties;
import com.uiusimulator.config.SecurityConfig;
import com.uiusimulator.player.dto.ActivityListResponse;
import com.uiusimulator.player.dto.ActivityResolveRequest;
import com.uiusimulator.player.dto.ActivityResolveResponse;
import com.uiusimulator.player.dto.ActivityStateResponse;
import com.uiusimulator.player.exception.PlayerSaveNotFoundException;
import com.uiusimulator.player.service.PlayerActivityService;
import java.util.List;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.context.properties.EnableConfigurationProperties;
import org.springframework.boot.test.autoconfigure.web.servlet.WebMvcTest;
import org.springframework.context.annotation.Import;
import org.springframework.http.MediaType;
import org.springframework.security.oauth2.jwt.JwtDecoder;
import org.springframework.test.context.ActiveProfiles;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;

@WebMvcTest(PlayerActivityController.class)
@ActiveProfiles("test")
@EnableConfigurationProperties(ClerkProperties.class)
@Import({SecurityConfig.class, ClerkJwtAuthenticationFilter.class, GlobalExceptionHandler.class})
class PlayerActivityControllerTest {

    @Autowired
    private MockMvc mockMvc;

    @Autowired
    private ObjectMapper objectMapper;

    @MockitoBean
    private JwtDecoder jwtDecoder;

    @MockitoBean
    private PlayerActivityService playerActivityService;

    @Test
    void listActivities_authenticated_returnsPayload() throws Exception {
        when(playerActivityService.listCurrentDayActivities(any())).thenReturn(
                ActivityListResponse.of(
                        1,
                        List.of(new ActivityStateResponse("BREAKFAST", "COMPLETED", "RICE", 5, 0, 1))
                )
        );

        mockMvc.perform(get("/api/players/me/activities")
                        .with(jwt().jwt(j -> j.subject("user_act"))))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.dayNumber").value(1))
                .andExpect(jsonPath("$.activities[0].activityId").value("BREAKFAST"))
                .andExpect(jsonPath("$.activities[0].status").value("COMPLETED"));
    }

    @Test
    void resolveActivity_authenticated_returnsCanonicalStats() throws Exception {
        when(playerActivityService.resolveActivity(any(), any(ActivityResolveRequest.class))).thenReturn(
                new ActivityResolveResponse(
                        "BREAKFAST",
                        "COMPLETED",
                        "RICE",
                        5,
                        0,
                        1,
                        false,
                        55,
                        50
                )
        );

        mockMvc.perform(post("/api/players/me/activities/resolve")
                        .with(jwt().jwt(j -> j.subject("user_act")))
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(objectMapper.writeValueAsString(new ActivityResolveRequest("BREAKFAST", "RICE"))))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.activityId").value("BREAKFAST"))
                .andExpect(jsonPath("$.aura").value(55))
                .andExpect(jsonPath("$.alreadyResolved").value(false));
    }

    @Test
    void resolveActivity_withoutSave_returns404() throws Exception {
        when(playerActivityService.resolveActivity(any(), any(ActivityResolveRequest.class)))
                .thenThrow(new PlayerSaveNotFoundException());

        mockMvc.perform(post("/api/players/me/activities/resolve")
                        .with(jwt().jwt(j -> j.subject("user_act")))
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(objectMapper.writeValueAsString(new ActivityResolveRequest("BREAKFAST", "RICE"))))
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.success").value(false));
    }
}
