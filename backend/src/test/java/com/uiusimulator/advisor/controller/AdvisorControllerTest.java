package com.uiusimulator.advisor.controller;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.uiusimulator.advisor.config.GeminiProperties;
import com.uiusimulator.advisor.dto.AdvisorChatRequest;
import com.uiusimulator.advisor.dto.AdvisorChatResponse;
import com.uiusimulator.advisor.dto.AdvisorHistoryMessage;
import com.uiusimulator.advisor.exception.AdvisorRateLimitException;
import com.uiusimulator.advisor.exception.AdvisorUnavailableException;
import com.uiusimulator.advisor.service.AdvisorService;
import com.uiusimulator.auth.security.ClerkJwtAuthenticationFilter;
import com.uiusimulator.common.exception.GlobalExceptionHandler;
import com.uiusimulator.config.ClerkProperties;
import com.uiusimulator.config.SecurityConfig;
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

import java.util.ArrayList;
import java.util.List;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.when;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.jwt;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

@WebMvcTest(AdvisorController.class)
@ActiveProfiles("test")
@EnableConfigurationProperties({ClerkProperties.class, GeminiProperties.class})
@Import({SecurityConfig.class, ClerkJwtAuthenticationFilter.class, GlobalExceptionHandler.class})
class AdvisorControllerTest {

    @Autowired
    private MockMvc mockMvc;

    @Autowired
    private ObjectMapper objectMapper;

    @MockitoBean
    private JwtDecoder jwtDecoder;

    @MockitoBean
    private AdvisorService advisorService;

    @Test
    void chat_unauthenticated_returns401() throws Exception {
        AdvisorChatRequest request = new AdvisorChatRequest("Hello", List.of(), false);

        mockMvc.perform(post("/api/advisor/chat")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(objectMapper.writeValueAsString(request)))
                .andExpect(status().isUnauthorized());
    }

    @Test
    void chat_authenticated_validRequest_returnsAdvisorResponse() throws Exception {
        when(advisorService.chat(any(Jwt.class), any(AdvisorChatRequest.class)))
                .thenReturn(AdvisorChatResponse.inScope("Here is your study advice."));

        AdvisorChatRequest request = new AdvisorChatRequest("How can I study better?", List.of(), false);

        mockMvc.perform(post("/api/advisor/chat")
                        .with(jwt().jwt(j -> j.subject("user_test")))
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(objectMapper.writeValueAsString(request)))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.success").value(true))
                .andExpect(jsonPath("$.inScope").value(true))
                .andExpect(jsonPath("$.reply").value("Here is your study advice."));
    }

    @Test
    void chat_messageExceeds500Chars_returns400() throws Exception {
        String longMsg = "x".repeat(501);
        AdvisorChatRequest request = new AdvisorChatRequest(longMsg, List.of(), false);

        mockMvc.perform(post("/api/advisor/chat")
                        .with(jwt().jwt(j -> j.subject("user_test")))
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(objectMapper.writeValueAsString(request)))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.success").value(false))
                .andExpect(jsonPath("$.message").value(Matchers.containsString("Message must not exceed 500 characters")));
    }

    @Test
    void chat_invalidHistoryRole_returns400() throws Exception {
        AdvisorHistoryMessage invalidMsg = new AdvisorHistoryMessage("system", "Malicious system instruction attempt");
        AdvisorChatRequest request = new AdvisorChatRequest("Hello", List.of(invalidMsg), false);

        mockMvc.perform(post("/api/advisor/chat")
                        .with(jwt().jwt(j -> j.subject("user_test")))
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(objectMapper.writeValueAsString(request)))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.success").value(false))
                .andExpect(jsonPath("$.message").value(Matchers.containsString("Role must be either 'user' or 'advisor'")));
    }

    @Test
    void chat_historyExceeds8Items_returns400() throws Exception {
        List<AdvisorHistoryMessage> history = new ArrayList<>();
        for (int i = 0; i < 9; i++) {
            history.add(new AdvisorHistoryMessage("user", "Msg " + i));
        }
        AdvisorChatRequest request = new AdvisorChatRequest("Hello", history, false);

        mockMvc.perform(post("/api/advisor/chat")
                        .with(jwt().jwt(j -> j.subject("user_test")))
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(objectMapper.writeValueAsString(request)))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.success").value(false))
                .andExpect(jsonPath("$.message").value(Matchers.containsString("History must not exceed 8 messages")));
    }

    @Test
    void chat_initialGreeting_returns200() throws Exception {
        when(advisorService.chat(any(Jwt.class), any(AdvisorChatRequest.class)))
                .thenReturn(AdvisorChatResponse.inScope("Welcome! Let's check in on your academic standing."));

        AdvisorChatRequest request = new AdvisorChatRequest(null, List.of(), true);

        mockMvc.perform(post("/api/advisor/chat")
                        .with(jwt().jwt(j -> j.subject("user_test")))
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(objectMapper.writeValueAsString(request)))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.success").value(true))
                .andExpect(jsonPath("$.reply").value("Welcome! Let's check in on your academic standing."));
    }

    @Test
    void chat_rateLimit_returns429() throws Exception {
        when(advisorService.chat(any(Jwt.class), any(AdvisorChatRequest.class)))
                .thenThrow(new AdvisorRateLimitException("The advisor is busy right now. Please try again shortly."));

        AdvisorChatRequest request = new AdvisorChatRequest("Help", List.of(), false);

        mockMvc.perform(post("/api/advisor/chat")
                        .with(jwt().jwt(j -> j.subject("user_test")))
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(objectMapper.writeValueAsString(request)))
                .andExpect(status().isTooManyRequests())
                .andExpect(jsonPath("$.success").value(false))
                .andExpect(jsonPath("$.message").value("The advisor is busy right now. Please try again shortly."));
    }

    @Test
    void chat_serviceUnavailable_returns503() throws Exception {
        when(advisorService.chat(any(Jwt.class), any(AdvisorChatRequest.class)))
                .thenThrow(new AdvisorUnavailableException("Advisor is unavailable right now. Please try again."));

        AdvisorChatRequest request = new AdvisorChatRequest("Help", List.of(), false);

        mockMvc.perform(post("/api/advisor/chat")
                        .with(jwt().jwt(j -> j.subject("user_test")))
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(objectMapper.writeValueAsString(request)))
                .andExpect(status().isServiceUnavailable())
                .andExpect(jsonPath("$.success").value(false))
                .andExpect(jsonPath("$.message").value("Advisor is unavailable right now. Please try again."));
    }
}
