package com.uiusimulator.advisor.dto;

import jakarta.validation.Valid;
import jakarta.validation.constraints.Size;
import java.util.List;

public record AdvisorChatRequest(
        @Size(max = 500, message = "Message must not exceed 500 characters")
        String message,

        @Valid
        @Size(max = 8, message = "History must not exceed 8 messages")
        List<AdvisorHistoryMessage> history,

        Boolean initialGreeting
) {
    public AdvisorChatRequest {
        if (history == null) {
            history = List.of();
        }
        if (initialGreeting == null) {
            initialGreeting = Boolean.FALSE;
        }
    }

    public boolean isInitialGreeting() {
        return Boolean.TRUE.equals(initialGreeting);
    }
}
