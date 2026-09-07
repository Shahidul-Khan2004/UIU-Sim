package com.uiusimulator.advisor.service;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.google.genai.Client;
import com.google.genai.errors.ApiException;
import com.google.genai.types.Content;
import com.google.genai.types.GenerateContentConfig;
import com.google.genai.types.GenerateContentResponse;
import com.google.genai.types.HttpOptions;
import com.google.genai.types.HttpRetryOptions;
import com.google.genai.types.Part;
import com.google.genai.types.Schema;
import com.google.genai.types.Type;
import com.uiusimulator.advisor.config.GeminiProperties;
import com.uiusimulator.advisor.dto.AdvisorHistoryMessage;
import com.uiusimulator.advisor.dto.GeminiStructuredOutput;
import com.uiusimulator.advisor.exception.AdvisorRateLimitException;
import com.uiusimulator.advisor.exception.AdvisorUnavailableException;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.stereotype.Component;

import java.util.ArrayList;
import java.util.List;
import java.util.Map;

@Component
public class GeminiAdvisorClient {

    private static final Logger log = LoggerFactory.getLogger(GeminiAdvisorClient.class);

    private final GeminiProperties properties;
    private final ObjectMapper objectMapper;
    private final Client genAiClient;

    @Autowired
    public GeminiAdvisorClient(GeminiProperties properties, ObjectMapper objectMapper) {
        this(properties.hasApiKey() ? Client.builder().apiKey(properties.apiKey()).build() : null, properties, objectMapper);
    }

    // Constructor for testing with mock or injected Client
    public GeminiAdvisorClient(Client client, GeminiProperties properties, ObjectMapper objectMapper) {
        this.genAiClient = client;
        this.properties = properties;
        this.objectMapper = objectMapper;
    }

    public GeminiStructuredOutput generateAdvisorResponse(
            String systemInstruction,
            List<AdvisorHistoryMessage> history,
            String currentMessage,
            boolean isInitialGreeting
    ) {
        if (genAiClient == null || !properties.hasApiKey()) {
            log.warn("Gemini API key is not configured");
            throw new AdvisorUnavailableException("Advisor is not configured right now.");
        }

        long startTime = System.currentTimeMillis();
        String modelName = properties.model();

        try {
            Schema responseSchema = Schema.builder()
                    .type(Type.Known.OBJECT)
                    .properties(Map.of(
                            "inScope", Schema.builder().type(Type.Known.BOOLEAN).build(),
                            "reply", Schema.builder().type(Type.Known.STRING).build()
                    ))
                    .required(List.of("inScope", "reply"))
                    .build();

            HttpOptions httpOptions = HttpOptions.builder()
                    .timeout(properties.timeoutSeconds() * 1000)
                    .retryOptions(HttpRetryOptions.builder().attempts(1).build())
                    .build();

            GenerateContentConfig config = GenerateContentConfig.builder()
                    .responseMimeType("application/json")
                    .responseSchema(responseSchema)
                    .systemInstruction(Content.fromParts(Part.fromText(systemInstruction)))
                    .temperature(properties.temperature().floatValue())
                    .maxOutputTokens(properties.maxOutputTokens())
                    .httpOptions(httpOptions)
                    .build();

            List<Content> contents = new ArrayList<>();

            if (history != null) {
                for (AdvisorHistoryMessage msg : history) {
                    if (msg != null && msg.content() != null && !msg.content().isBlank()) {
                        String role = "advisor".equalsIgnoreCase(msg.role()) ? "model" : "user";
                        contents.add(Content.builder()
                                .role(role)
                                .parts(Part.fromText(msg.content()))
                                .build());
                    }
                }
            }

            if (isInitialGreeting) {
                contents.add(Content.builder()
                        .role("user")
                        .parts(Part.fromText("Give this student a short academic-advisor check-in based on their current simulated state."))
                        .build());
            } else if (currentMessage != null && !currentMessage.isBlank()) {
                contents.add(Content.builder()
                        .role("user")
                        .parts(Part.fromText(currentMessage))
                        .build());
            }

            GenerateContentResponse response = genAiClient.models.generateContent(modelName, contents, config);
            long durationMs = System.currentTimeMillis() - startTime;

            String responseText = response != null ? response.text() : null;
            if (responseText == null || responseText.isBlank()) {
                log.warn("Empty response received from Gemini model={} durationMs={}", modelName, durationMs);
                throw new AdvisorUnavailableException("Empty response received from advisor model.");
            }

            GeminiStructuredOutput output = objectMapper.readValue(responseText, GeminiStructuredOutput.class);
            log.info("Advisor response generated: model={} inScope={} durationMs={}",
                    modelName, output.inScope(), durationMs);
            return output;

        } catch (ApiException apiEx) {
            long durationMs = System.currentTimeMillis() - startTime;
            log.warn("Gemini API error: code={} status={} durationMs={}",
                    apiEx.code(), apiEx.status(), durationMs);

            if (apiEx.code() == 429 || "RESOURCE_EXHAUSTED".equalsIgnoreCase(apiEx.status())) {
                throw new AdvisorRateLimitException("The advisor is busy right now. Please try again shortly.", apiEx);
            }
            throw new AdvisorUnavailableException("Advisor is unavailable right now. Please try again.", apiEx);

        } catch (AdvisorRateLimitException | AdvisorUnavailableException ex) {
            throw ex;
        } catch (Exception ex) {
            long durationMs = System.currentTimeMillis() - startTime;
            log.error("Failed to generate advisor response: model={} durationMs={} error={}",
                    modelName, durationMs, ex.getClass().getSimpleName());

            String msg = ex.getMessage() != null ? ex.getMessage().toLowerCase() : "";
            if (msg.contains("429") || msg.contains("quota") || msg.contains("resource_exhausted") || msg.contains("rate limit")) {
                throw new AdvisorRateLimitException("The advisor is busy right now. Please try again shortly.", ex);
            }
            throw new AdvisorUnavailableException("Advisor is unavailable right now. Please try again.", ex);
        }
    }
}
