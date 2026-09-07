package com.uiusimulator.advisor.config;

import org.springframework.boot.context.properties.ConfigurationProperties;

@ConfigurationProperties(prefix = "uiu.gemini")
public record GeminiProperties(
        String apiKey,
        String model,
        Double temperature,
        Integer maxOutputTokens,
        Integer timeoutSeconds
) {
    public GeminiProperties {
        if (model == null || model.isBlank()) {
            model = "gemini-3.1-flash-lite";
        }
        if (temperature == null) {
            temperature = 0.2;
        }
        if (maxOutputTokens == null) {
            maxOutputTokens = 350;
        }
        if (timeoutSeconds == null) {
            timeoutSeconds = 15;
        }
    }

    public boolean hasApiKey() {
        return apiKey != null && !apiKey.isBlank();
    }
}
