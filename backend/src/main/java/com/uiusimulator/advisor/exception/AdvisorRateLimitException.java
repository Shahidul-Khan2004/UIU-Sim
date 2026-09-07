package com.uiusimulator.advisor.exception;

public class AdvisorRateLimitException extends RuntimeException {
    public AdvisorRateLimitException(String message) {
        super(message);
    }

    public AdvisorRateLimitException(String message, Throwable cause) {
        super(message, cause);
    }
}
