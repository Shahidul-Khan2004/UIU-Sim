package com.uiusimulator.common.response;

import java.time.Instant;

public record ApiErrorResponse(
        boolean success,
        String message,
        Instant timestamp,
        String path
) {
    public static ApiErrorResponse of(String message, String path) {
        return new ApiErrorResponse(false, message, Instant.now(), path);
    }
}
