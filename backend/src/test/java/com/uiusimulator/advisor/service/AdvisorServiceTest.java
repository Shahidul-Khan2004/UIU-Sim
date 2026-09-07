package com.uiusimulator.advisor.service;

import com.uiusimulator.advisor.dto.AdvisorChatRequest;
import com.uiusimulator.advisor.dto.AdvisorChatResponse;
import com.uiusimulator.advisor.dto.AdvisorHistoryMessage;
import com.uiusimulator.advisor.dto.GeminiStructuredOutput;
import com.uiusimulator.advisor.model.AdvisorStudentContext;
import com.uiusimulator.advisor.model.StudentStandingBand;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;
import org.springframework.security.oauth2.jwt.Jwt;

import java.time.Instant;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.ArgumentMatchers.*;
import static org.mockito.Mockito.*;

@ExtendWith(MockitoExtension.class)
class AdvisorServiceTest {

    @Mock
    private AdvisorStudentContextResolver contextResolver;

    @Mock
    private GeminiAdvisorClient geminiAdvisorClient;

    private AdvisorService advisorService;
    private AdvisorStudentContext testContext;
    private Jwt testJwt;

    @BeforeEach
    void setUp() {
        advisorService = new AdvisorService(contextResolver, geminiAdvisorClient);

        testContext = new AdvisorStudentContext(35, 75, StudentStandingBand.LOW, StudentStandingBand.HIGH);

        testJwt = new Jwt(
                "mock-token",
                Instant.now(),
                Instant.now().plusSeconds(3600),
                Map.of("alg", "none"),
                Map.of("sub", "user_clerk_123", "email", "student@uiu.ac.bd")
        );
    }

    @Test
    void chat_inScopeResponse_returnsAdvisorAdvice() {
        when(contextResolver.resolveStudentContext(testJwt)).thenReturn(testContext);
        when(geminiAdvisorClient.generateAdvisorResponse(anyString(), anyList(), anyString(), anyBoolean()))
                .thenReturn(new GeminiStructuredOutput(true, "Focus on building good study habits."));

        AdvisorChatRequest request = new AdvisorChatRequest("How can I improve?", List.of(), false);
        AdvisorChatResponse response = advisorService.chat(testJwt, request);

        assertTrue(response.success());
        assertTrue(response.inScope());
        assertEquals("Focus on building good study habits.", response.reply());

        verify(contextResolver).resolveStudentContext(testJwt);
    }

    @Test
    void chat_outOfScopeResponse_enforcesCanonicalRefusalAndIgnoresModelReply() {
        when(contextResolver.resolveStudentContext(testJwt)).thenReturn(testContext);
        when(geminiAdvisorClient.generateAdvisorResponse(anyString(), anyList(), anyString(), anyBoolean()))
                .thenReturn(new GeminiStructuredOutput(false, "Creative refusal that should be ignored by server"));

        AdvisorChatRequest request = new AdvisorChatRequest("Write my python homework", List.of(), false);
        AdvisorChatResponse response = advisorService.chat(testJwt, request);

        assertTrue(response.success());
        assertFalse(response.inScope());
        assertEquals(AdvisorService.CANONICAL_OUT_OF_SCOPE_REFUSAL, response.reply());
    }

    @Test
    void chat_initialGreeting_worksWithoutUserMessage() {
        when(contextResolver.resolveStudentContext(testJwt)).thenReturn(testContext);
        when(geminiAdvisorClient.generateAdvisorResponse(anyString(), anyList(), isNull(), eq(true)))
                .thenReturn(new GeminiStructuredOutput(true, "Welcome back! Your academic standing is strong."));

        AdvisorChatRequest request = new AdvisorChatRequest(null, List.of(), true);
        AdvisorChatResponse response = advisorService.chat(testJwt, request);

        assertTrue(response.success());
        assertTrue(response.inScope());
        assertEquals("Welcome back! Your academic standing is strong.", response.reply());
    }

    @Test
    void chat_normalMessageBlank_throwsIllegalArgumentException() {
        AdvisorChatRequest request = new AdvisorChatRequest("   ", List.of(), false);
        assertThrows(IllegalArgumentException.class, () -> advisorService.chat(testJwt, request));
    }

    @Test
    void chat_normalMessageExceeds500Chars_throwsIllegalArgumentException() {
        String longMessage = "a".repeat(501);
        AdvisorChatRequest request = new AdvisorChatRequest(longMessage, List.of(), false);
        assertThrows(IllegalArgumentException.class, () -> advisorService.chat(testJwt, request));
    }

    @Test
    void chat_authoritativeStatsPassedToSystemPrompt_andClientCannotOverride() {
        when(contextResolver.resolveStudentContext(testJwt)).thenReturn(testContext);
        when(geminiAdvisorClient.generateAdvisorResponse(anyString(), anyList(), anyString(), anyBoolean()))
                .thenReturn(new GeminiStructuredOutput(true, "Advice"));

        AdvisorChatRequest request = new AdvisorChatRequest("Study tips", List.of(), false);
        advisorService.chat(testJwt, request);

        ArgumentCaptor<String> promptCaptor = ArgumentCaptor.forClass(String.class);
        verify(geminiAdvisorClient).generateAdvisorResponse(promptCaptor.capture(), anyList(), eq("Study tips"), eq(false));

        String prompt = promptCaptor.getValue();
        assertTrue(prompt.contains("Aura: 35/100"));
        assertTrue(prompt.contains("Aura band: LOW"));
        assertTrue(prompt.contains("Academic Reputation: 75/100"));
        assertTrue(prompt.contains("Academic band: HIGH"));
    }

    @Test
    void chat_historyTruncatedTo8OldestFirst() {
        when(contextResolver.resolveStudentContext(testJwt)).thenReturn(testContext);
        when(geminiAdvisorClient.generateAdvisorResponse(anyString(), anyList(), anyString(), anyBoolean()))
                .thenReturn(new GeminiStructuredOutput(true, "Advice"));

        List<AdvisorHistoryMessage> history = new ArrayList<>();
        for (int i = 1; i <= 10; i++) {
            history.add(new AdvisorHistoryMessage(i % 2 == 0 ? "advisor" : "user", "Message " + i));
        }

        AdvisorChatRequest request = new AdvisorChatRequest("Follow up question", history, false);
        advisorService.chat(testJwt, request);

        @SuppressWarnings("unchecked")
        ArgumentCaptor<List<AdvisorHistoryMessage>> historyCaptor = ArgumentCaptor.forClass(List.class);
        verify(geminiAdvisorClient).generateAdvisorResponse(anyString(), historyCaptor.capture(), anyString(), eq(false));

        List<AdvisorHistoryMessage> passedHistory = historyCaptor.getValue();
        assertEquals(8, passedHistory.size());
        assertEquals("Message 3", passedHistory.get(0).content());
        assertEquals("Message 10", passedHistory.get(7).content());
    }

    @Test
    void chat_geminiCalledOutsideTransaction_contextResolvedFirst() {
        when(contextResolver.resolveStudentContext(testJwt)).thenReturn(testContext);
        when(geminiAdvisorClient.generateAdvisorResponse(anyString(), anyList(), anyString(), anyBoolean()))
                .thenReturn(new GeminiStructuredOutput(true, "Advice"));

        AdvisorChatRequest request = new AdvisorChatRequest("Advice", List.of(), false);
        advisorService.chat(testJwt, request);

        // Verify contextResolver is called first and resolves context before Gemini call
        var inOrder = inOrder(contextResolver, geminiAdvisorClient);
        inOrder.verify(contextResolver).resolveStudentContext(testJwt);
        inOrder.verify(geminiAdvisorClient).generateAdvisorResponse(anyString(), anyList(), anyString(), anyBoolean());
    }
}
