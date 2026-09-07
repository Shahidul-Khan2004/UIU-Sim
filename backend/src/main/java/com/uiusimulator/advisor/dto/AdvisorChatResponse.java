package com.uiusimulator.advisor.dto;

public record AdvisorChatResponse(
        boolean success,
        boolean inScope,
        String reply
) {
    public static AdvisorChatResponse inScope(String reply) {
        return new AdvisorChatResponse(true, true, reply);
    }

    public static AdvisorChatResponse outOfScope(String canonicalRefusal) {
        return new AdvisorChatResponse(true, false, canonicalRefusal);
    }
}
