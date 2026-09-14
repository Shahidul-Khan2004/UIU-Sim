package com.uiusimulator.player.dto;

public record InitialIdTutorialConsumeResponse(boolean consumed) {
    public static InitialIdTutorialConsumeResponse of(boolean consumed) {
        return new InitialIdTutorialConsumeResponse(consumed);
    }
}
