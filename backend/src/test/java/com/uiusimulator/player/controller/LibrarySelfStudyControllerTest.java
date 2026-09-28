package com.uiusimulator.player.controller;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.when;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.jwt;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.uiusimulator.auth.security.ClerkJwtAuthenticationFilter;
import com.uiusimulator.common.exception.GlobalExceptionHandler;
import com.uiusimulator.config.ClerkProperties;
import com.uiusimulator.config.SecurityConfig;
import com.uiusimulator.player.dto.LibrarySelfStudyMilestoneRequest;
import com.uiusimulator.player.dto.LibrarySelfStudySessionResponse;
import com.uiusimulator.player.service.LibrarySelfStudyService;
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

@WebMvcTest(LibrarySelfStudyController.class)
@ActiveProfiles("test")
@EnableConfigurationProperties(ClerkProperties.class)
@Import({SecurityConfig.class, ClerkJwtAuthenticationFilter.class, GlobalExceptionHandler.class})
class LibrarySelfStudyControllerTest {

    @Autowired
    private MockMvc mockMvc;

    @Autowired
    private ObjectMapper objectMapper;

    @MockitoBean
    private JwtDecoder jwtDecoder;

    @MockitoBean
    private LibrarySelfStudyService librarySelfStudyService;

    @Test
    void start_authenticated_returnsAttempt() throws Exception {
        when(librarySelfStudyService.start(any())).thenReturn(started());

        mockMvc.perform(post("/api/players/me/activities/library-self-study/start")
                        .with(jwt().jwt(j -> j.subject("user_self_study"))))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.activityId").value("LIBRARY_SELF_STUDY"))
                .andExpect(jsonPath("$.status").value("IN_PROGRESS"))
                .andExpect(jsonPath("$.alreadyCompleted").value(false))
                .andExpect(jsonPath("$.academicReputation").value(50));
    }

    @Test
    void milestone_authenticated_returnsServerReward() throws Exception {
        when(librarySelfStudyService.claimMilestone(any(), any(LibrarySelfStudyMilestoneRequest.class)))
                .thenReturn(new LibrarySelfStudySessionResponse(
                        "LIBRARY_SELF_STUDY",
                        "IN_PROGRESS",
                        "STARTED",
                        30,
                        0,
                        1,
                        1,
                        1,
                        false,
                        false,
                        true,
                        30000L,
                        1,
                        50,
                        51
                ));

        mockMvc.perform(post("/api/players/me/activities/library-self-study/milestone")
                        .with(jwt().jwt(j -> j.subject("user_self_study")))
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(objectMapper.writeValueAsString(new LibrarySelfStudyMilestoneRequest(30))))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.milestoneSeconds").value(30))
                .andExpect(jsonPath("$.appliedReputationDelta").value(1))
                .andExpect(jsonPath("$.auraDelta").value(0))
                .andExpect(jsonPath("$.academicReputation").value(51));
    }

    @Test
    void milestone_rejectsMissingSeconds() throws Exception {
        mockMvc.perform(post("/api/players/me/activities/library-self-study/milestone")
                        .with(jwt().jwt(j -> j.subject("user_self_study")))
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{}"))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.success").value(false));
    }

    @Test
    void start_withoutToken_isUnauthorized() throws Exception {
        mockMvc.perform(post("/api/players/me/activities/library-self-study/start"))
                .andExpect(status().isUnauthorized());
    }

    @Test
    void abandon_authenticated_returnsCompleted() throws Exception {
        when(librarySelfStudyService.abandon(any())).thenReturn(new LibrarySelfStudySessionResponse(
                "LIBRARY_SELF_STUDY",
                "COMPLETED",
                "ABANDONED",
                30,
                0,
                1,
                0,
                0,
                false,
                true,
                false,
                30000L,
                1,
                50,
                51
        ));

        mockMvc.perform(post("/api/players/me/activities/library-self-study/abandon")
                        .with(jwt().jwt(j -> j.subject("user_self_study"))))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.status").value("COMPLETED"))
                .andExpect(jsonPath("$.outcome").value("ABANDONED"))
                .andExpect(jsonPath("$.alreadyCompleted").value(true))
                .andExpect(jsonPath("$.appliedReputationDelta").value(0));
    }

    private static LibrarySelfStudySessionResponse started() {
        return new LibrarySelfStudySessionResponse(
                "LIBRARY_SELF_STUDY",
                "IN_PROGRESS",
                "STARTED",
                0,
                0,
                0,
                0,
                0,
                false,
                false,
                true,
                0L,
                1,
                50,
                50
        );
    }
}
