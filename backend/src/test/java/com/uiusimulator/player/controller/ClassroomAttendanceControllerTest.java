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

@WebMvcTest(ClassroomAttendanceController.class)
@ActiveProfiles("test")
@EnableConfigurationProperties(ClerkProperties.class)
@Import({SecurityConfig.class, ClerkJwtAuthenticationFilter.class, GlobalExceptionHandler.class})
class ClassroomAttendanceControllerTest {

    @Autowired
    private MockMvc mockMvc;

    @MockitoBean
    private AttendIcsService attendIcsService;

    @MockitoBean
    private JwtDecoder jwtDecoder;

    @Test
    void englishMilestone_usesActivityPath() throws Exception {
        when(attendIcsService.claimMilestone(any(), eq("ATTEND_ENGLISH"), eq(new AttendIcsMilestoneRequest(30))))
                .thenReturn(session("ATTEND_ENGLISH", "IN_PROGRESS", "ATTENDING", 30, 0, 2, 52));

        mockMvc.perform(post("/api/players/me/activities/classroom/ATTEND_ENGLISH/milestone")
                        .with(jwt())
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"milestoneSeconds\":30}"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.activityId").value("ATTEND_ENGLISH"))
                .andExpect(jsonPath("$.milestoneSeconds").value(30))
                .andExpect(jsonPath("$.reputationDelta").value(2));
    }

    @Test
    void dmProxy_usesActivityPath() throws Exception {
        when(attendIcsService.punchProxy(any(), eq("ATTEND_DM")))
                .thenReturn(session("ATTEND_DM", "COMPLETED", "PROXY", 0, 3, 0, 50));

        mockMvc.perform(post("/api/players/me/activities/classroom/ATTEND_DM/proxy").with(jwt()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.activityId").value("ATTEND_DM"))
                .andExpect(jsonPath("$.outcome").value("PROXY"))
                .andExpect(jsonPath("$.auraDelta").value(3));
    }

    private static AttendIcsSessionResponse session(
            String activityId,
            String status,
            String outcome,
            int milestoneSeconds,
            int auraDelta,
            int reputationDelta,
            int academicReputation
    ) {
        return new AttendIcsSessionResponse(
                activityId,
                status,
                outcome,
                milestoneSeconds,
                auraDelta,
                reputationDelta,
                auraDelta,
                reputationDelta,
                auraDelta,
                reputationDelta,
                false,
                "IN_PROGRESS".equals(status),
                milestoneSeconds * 1000L,
                1,
                50 + auraDelta,
                academicReputation
        );
    }
}
