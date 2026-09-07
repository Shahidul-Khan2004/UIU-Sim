package com.uiusimulator.advisor.service;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.google.genai.Client;
import com.google.genai.Models;
import com.google.genai.errors.ApiException;
import com.google.genai.types.GenerateContentConfig;
import com.google.genai.types.GenerateContentResponse;
import com.uiusimulator.advisor.config.GeminiProperties;
import com.uiusimulator.advisor.dto.GeminiStructuredOutput;
import com.uiusimulator.advisor.exception.AdvisorRateLimitException;
import com.uiusimulator.advisor.exception.AdvisorUnavailableException;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.lang.reflect.Field;
import java.util.List;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyList;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

@ExtendWith(MockitoExtension.class)
class GeminiAdvisorClientTest {

    @Mock
    private Client mockClient;

    @Mock
    private Models mockModels;

    private ObjectMapper objectMapper;
    private GeminiProperties properties;

    @BeforeEach
    void setUp() throws Exception {
        objectMapper = new ObjectMapper();
        properties = new GeminiProperties("dummy-key", "gemini-3.1-flash-lite", 0.2, 350, 15);

        // Set the public final models field on mockClient
        Field modelsField = Client.class.getField("models");
        modelsField.setAccessible(true);
        modelsField.set(mockClient, mockModels);
    }

    @Test
    void generateAdvisorResponse_missingApiKey_throwsAdvisorUnavailableException() {
        GeminiProperties emptyProps = new GeminiProperties("", "gemini-3.1-flash-lite", 0.2, 350, 15);
        GeminiAdvisorClient client = new GeminiAdvisorClient(emptyProps, objectMapper);

        assertThrows(AdvisorUnavailableException.class, () ->
                client.generateAdvisorResponse("System prompt", List.of(), "Hello", false));
    }

    @Test
    void generateAdvisorResponse_validStructuredOutput_parsesSuccessfully() {
        GenerateContentResponse mockResponse = mock(GenerateContentResponse.class);
        when(mockResponse.text()).thenReturn("{\"inScope\": true, \"reply\": \"Great progress!\"}");
        when(mockModels.generateContent(anyString(), anyList(), any(GenerateContentConfig.class)))
                .thenReturn(mockResponse);

        GeminiAdvisorClient client = new GeminiAdvisorClient(mockClient, properties, objectMapper);
        GeminiStructuredOutput output = client.generateAdvisorResponse("System prompt", List.of(), "Hello", false);

        assertTrue(output.inScope());
        assertEquals("Great progress!", output.reply());
    }

    @Test
    void generateAdvisorResponse_emptyResponse_throwsAdvisorUnavailableException() {
        GenerateContentResponse mockResponse = mock(GenerateContentResponse.class);
        when(mockResponse.text()).thenReturn("   ");
        when(mockModels.generateContent(anyString(), anyList(), any(GenerateContentConfig.class)))
                .thenReturn(mockResponse);

        GeminiAdvisorClient client = new GeminiAdvisorClient(mockClient, properties, objectMapper);
        assertThrows(AdvisorUnavailableException.class, () ->
                client.generateAdvisorResponse("System prompt", List.of(), "Hello", false));
    }

    @Test
    void generateAdvisorResponse_malformedJson_throwsAdvisorUnavailableException() {
        GenerateContentResponse mockResponse = mock(GenerateContentResponse.class);
        when(mockResponse.text()).thenReturn("Not valid JSON at all");
        when(mockModels.generateContent(anyString(), anyList(), any(GenerateContentConfig.class)))
                .thenReturn(mockResponse);

        GeminiAdvisorClient client = new GeminiAdvisorClient(mockClient, properties, objectMapper);
        assertThrows(AdvisorUnavailableException.class, () ->
                client.generateAdvisorResponse("System prompt", List.of(), "Hello", false));
    }

    @Test
    void generateAdvisorResponse_rateLimit429_throwsAdvisorRateLimitException() {
        ApiException apiException = new ApiException(429, "Too many requests", "RESOURCE_EXHAUSTED");
        when(mockModels.generateContent(anyString(), anyList(), any(GenerateContentConfig.class)))
                .thenThrow(apiException);

        GeminiAdvisorClient client = new GeminiAdvisorClient(mockClient, properties, objectMapper);
        assertThrows(AdvisorRateLimitException.class, () ->
                client.generateAdvisorResponse("System prompt", List.of(), "Hello", false));
    }

    @Test
    void generateAdvisorResponse_provider5xx_throwsAdvisorUnavailableException() {
        ApiException apiException = new ApiException(503, "Internal model unavailable", "UNAVAILABLE");
        when(mockModels.generateContent(anyString(), anyList(), any(GenerateContentConfig.class)))
                .thenThrow(apiException);

        GeminiAdvisorClient client = new GeminiAdvisorClient(mockClient, properties, objectMapper);
        assertThrows(AdvisorUnavailableException.class, () ->
                client.generateAdvisorResponse("System prompt", List.of(), "Hello", false));
    }
}
