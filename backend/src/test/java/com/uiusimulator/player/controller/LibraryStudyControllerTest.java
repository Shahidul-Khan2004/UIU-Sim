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
import com.uiusimulator.player.dto.LibraryStudyCompleteRequest;
import com.uiusimulator.player.dto.LibraryStudyResponse;
import com.uiusimulator.player.service.LibraryStudyService;
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

@WebMvcTest(LibraryStudyController.class)
@ActiveProfiles("test")
@EnableConfigurationProperties(ClerkProperties.class)
@Import({SecurityConfig.class, ClerkJwtAuthenticationFilter.class, GlobalExceptionHandler.class})
class LibraryStudyControllerTest {

    @Autowired
    private MockMvc mockMvc;

    @Autowired
    private ObjectMapper objectMapper;

    @MockitoBean
    private JwtDecoder jwtDecoder;

    @MockitoBean
    private LibraryStudyService libraryStudyService;

    @Test
    void start_authenticated_returnsAttempt() throws Exception {
        when(libraryStudyService.start(any())).thenReturn(started());

        mockMvc.perform(post("/api/players/me/activities/library-study/start")
                        .with(jwt().jwt(j -> j.subject("user_lib"))))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.activityId").value("LIBRARY_STUDY"))
                .andExpect(jsonPath("$.status").value("IN_PROGRESS"))
                .andExpect(jsonPath("$.alreadyCompleted").value(false))
                .andExpect(jsonPath("$.academicReputation").value(50));
    }

    @Test
    void complete_authenticated_returnsServerReward() throws Exception {
        when(libraryStudyService.complete(any(), any(LibraryStudyCompleteRequest.class))).thenReturn(
                new LibraryStudyResponse(
                        "LIBRARY_STUDY",
                        "COMPLETED",
                        "COMPLETED",
                        73,
                        0,
                        3,
                        3,
                        true,
                        false,
                        1,
                        50,
                        53
                )
        );

        mockMvc.perform(post("/api/players/me/activities/library-study/complete")
                        .with(jwt().jwt(j -> j.subject("user_lib")))
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(objectMapper.writeValueAsString(new LibraryStudyCompleteRequest(73))))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.score").value(73))
                .andExpect(jsonPath("$.reputationDelta").value(3))
                .andExpect(jsonPath("$.appliedReputationDelta").value(3))
                .andExpect(jsonPath("$.auraDelta").value(0))
                .andExpect(jsonPath("$.academicReputation").value(53));
    }

    @Test
    void complete_rejectsScoreOutsideRange() throws Exception {
        mockMvc.perform(post("/api/players/me/activities/library-study/complete")
                        .with(jwt().jwt(j -> j.subject("user_lib")))
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"score\":120}"))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.success").value(false));
    }

    @Test
    void start_withoutToken_isUnauthorized() throws Exception {
        mockMvc.perform(post("/api/players/me/activities/library-study/start"))
                .andExpect(status().isUnauthorized());
    }

    private static LibraryStudyResponse started() {
        return new LibraryStudyResponse(
                "LIBRARY_STUDY",
                "IN_PROGRESS",
                "STARTED",
                null,
                0,
                0,
                0,
                false,
                false,
                1,
                50,
                50
        );
    }
}
