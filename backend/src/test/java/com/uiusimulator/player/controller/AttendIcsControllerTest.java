package com.uiusimulator.player.controller;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.when;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.jwt;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.uiusimulator.auth.security.ClerkJwtAuthenticationFilter;
import com.uiusimulator.common.exception.GlobalExceptionHandler;
import com.uiusimulator.config.ClerkProperties;
import com.uiusimulator.config.SecurityConfig;
import com.uiusimulator.player.dto.AttendIcsMilestoneRequest;
import com.uiusimulator.player.dto.AttendIcsSessionResponse;
import com.uiusimulator.player.service.AttendIcsService;
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

@WebMvcTest(AttendIcsController.class)
@ActiveProfiles("test")
@EnableConfigurationProperties(ClerkProperties.class)
@Import({SecurityConfig.class, ClerkJwtAuthenticationFilter.class, GlobalExceptionHandler.class})
class AttendIcsControllerTest {

    @Autowired
    private MockMvc mockMvc;

    @MockitoBean
    private AttendIcsService attendIcsService;

    @MockitoBean
    private JwtDecoder jwtDecoder;

    @Test
    void milestone_returnsSessionPayload() throws Exception {
        when(attendIcsService.claimMilestone(any(), eq(new AttendIcsMilestoneRequest(30))))
                .thenReturn(new AttendIcsSessionResponse(
                        "ATTEND_ICS",
                        "IN_PROGRESS",
                        "ATTENDING",
                        30,
                        0,
                        4,
                        0,
                        4,
                        0,
                        4,
                        false,
                        true,
                        30000L,
                        1,
                        50,
                        54
                ));

        mockMvc.perform(post("/api/players/me/activities/attend-ics/milestone")
                        .with(jwt())
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"milestoneSeconds\":30}"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.activityId").value("ATTEND_ICS"))
                .andExpect(jsonPath("$.milestoneSeconds").value(30))
                .andExpect(jsonPath("$.reputationDelta").value(4))
                .andExpect(jsonPath("$.academicReputation").value(54));
    }

    @Test
    void proxy_returnsAuraReward() throws Exception {
        when(attendIcsService.punchProxy(any())).thenReturn(new AttendIcsSessionResponse(
                "ATTEND_ICS",
                "COMPLETED",
                "PROXY",
                0,
                5,
                0,
                5,
                0,
                5,
                0,
                false,
                false,
                0L,
                1,
                55,
                50
        ));

        mockMvc.perform(post("/api/players/me/activities/attend-ics/proxy").with(jwt()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.outcome").value("PROXY"))
                .andExpect(jsonPath("$.auraDelta").value(5))
                .andExpect(jsonPath("$.reputationDelta").value(0));
    }
}
