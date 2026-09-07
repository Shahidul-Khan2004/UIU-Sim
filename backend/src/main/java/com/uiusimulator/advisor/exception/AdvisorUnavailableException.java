package com.uiusimulator.advisor.exception;

public class AdvisorUnavailableException extends RuntimeException {
    public AdvisorUnavailableException(String message) {
        super(message);
    }

    public AdvisorUnavailableException(String message, Throwable cause) {
        super(message, cause);
    }
}
