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
import com.uiusimulator.player.dto.DayAdvanceRequest;
import com.uiusimulator.player.dto.DayAdvanceResponse;
import com.uiusimulator.player.dto.DayFinalizeResponse;
import com.uiusimulator.player.dto.DaySummaryActivityResponse;
import com.uiusimulator.player.service.PlayerDayService;
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

@WebMvcTest(PlayerDayController.class)
@ActiveProfiles("test")
@EnableConfigurationProperties(ClerkProperties.class)
@Import({SecurityConfig.class, ClerkJwtAuthenticationFilter.class, GlobalExceptionHandler.class})
class PlayerDayControllerTest {

    @Autowired
    private MockMvc mockMvc;

    @MockitoBean
    private PlayerDayService playerDayService;

    @MockitoBean
    private JwtDecoder jwtDecoder;

    @Test
    void finalizeDay_returnsSummary() throws Exception {
        when(playerDayService.finalizeCurrentDay(any())).thenReturn(
                DayFinalizeResponse.of(
                        1,
                        1,
                        List.of(
                                new DaySummaryActivityResponse("GET_ID_CARD", "COMPLETED", "COMPLETED", 0, 0),
                                new DaySummaryActivityResponse("BREAKFAST", "MISSED", "SKIP_BREAKFAST", -5, 0)
                        ),
                        45,
                        50
                )
        );

        mockMvc.perform(post("/api/players/me/day/finalize").with(jwt()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.semester").value(1))
                .andExpect(jsonPath("$.day").value(1))
                .andExpect(jsonPath("$.totalAuraDelta").value(-5))
                .andExpect(jsonPath("$.activities[1].activityId").value("BREAKFAST"))
                .andExpect(jsonPath("$.aura").value(45));
    }

    @Test
    void advanceDay_returnsNewDay() throws Exception {
        when(playerDayService.advanceDay(any(), eq(new DayAdvanceRequest(1, 1)))).thenReturn(
                new DayAdvanceResponse(1, 2, false, true, 55, 50)
        );

        mockMvc.perform(post("/api/players/me/day/advance")
                        .with(jwt())
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"expectedSemester\":1,\"expectedDay\":1}"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.semester").value(1))
                .andExpect(jsonPath("$.currentDay").value(2))
                .andExpect(jsonPath("$.alreadyAdvanced").value(false))
                .andExpect(jsonPath("$.idCardIssued").value(true));
    }
}
