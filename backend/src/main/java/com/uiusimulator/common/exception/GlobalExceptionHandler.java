package com.uiusimulator.common.exception;

import com.uiusimulator.auth.exception.AuthenticationFailedException;
import com.uiusimulator.common.response.ApiErrorResponse;
import com.uiusimulator.player.exception.PlayerStatsNotFoundException;
import jakarta.servlet.http.HttpServletRequest;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.dao.DataAccessException;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.security.access.AccessDeniedException;
import org.springframework.security.core.AuthenticationException;
import org.springframework.validation.FieldError;
import org.springframework.web.bind.MethodArgumentNotValidException;
import org.springframework.web.bind.annotation.ExceptionHandler;
import org.springframework.web.bind.annotation.RestControllerAdvice;

@RestControllerAdvice
public class GlobalExceptionHandler {

    private static final Logger log = LoggerFactory.getLogger(GlobalExceptionHandler.class);

    @ExceptionHandler(MethodArgumentNotValidException.class)
    public ResponseEntity<ApiErrorResponse> handleValidation(
            MethodArgumentNotValidException ex,
            HttpServletRequest request
    ) {
        String message = ex.getBindingResult().getFieldErrors().stream()
                .findFirst()
                .map(FieldError::getDefaultMessage)
                .orElse("Validation failed");
        return ResponseEntity.badRequest().body(ApiErrorResponse.of(message, request.getRequestURI()));
    }

    @ExceptionHandler({AuthenticationFailedException.class, AuthenticationException.class})
    public ResponseEntity<ApiErrorResponse> handleAuthentication(
            Exception ex,
            HttpServletRequest request
    ) {
        log.error("Authentication failure: {}", ex.getMessage());
        return ResponseEntity.status(HttpStatus.UNAUTHORIZED)
                .body(ApiErrorResponse.of("Authentication failed", request.getRequestURI()));
    }

    @ExceptionHandler(AccessDeniedException.class)
    public ResponseEntity<ApiErrorResponse> handleAccessDenied(
            AccessDeniedException ex,
            HttpServletRequest request
    ) {
        return ResponseEntity.status(HttpStatus.FORBIDDEN)
                .body(ApiErrorResponse.of("Access denied", request.getRequestURI()));
    }

    @ExceptionHandler(IllegalArgumentException.class)
    public ResponseEntity<ApiErrorResponse> handleIllegalArgument(
            IllegalArgumentException ex,
            HttpServletRequest request
    ) {
        return ResponseEntity.badRequest()
                .body(ApiErrorResponse.of(ex.getMessage(), request.getRequestURI()));
    }

    @ExceptionHandler(PlayerStatsNotFoundException.class)
    public ResponseEntity<ApiErrorResponse> handlePlayerStatsNotFound(
            PlayerStatsNotFoundException ex,
            HttpServletRequest request
    ) {
        log.error("Data integrity error: {}", ex.getMessage(), ex);
        return ResponseEntity.status(HttpStatus.INTERNAL_SERVER_ERROR)
                .body(ApiErrorResponse.of("Player state error", request.getRequestURI()));
    }

    @ExceptionHandler(com.uiusimulator.advisor.exception.AdvisorRateLimitException.class)
    public ResponseEntity<ApiErrorResponse> handleAdvisorRateLimit(
            com.uiusimulator.advisor.exception.AdvisorRateLimitException ex,
            HttpServletRequest request
    ) {
        log.warn("Advisor rate limit: {}", ex.getMessage());
        return ResponseEntity.status(HttpStatus.TOO_MANY_REQUESTS)
                .body(ApiErrorResponse.of("The advisor is busy right now. Please try again shortly.", request.getRequestURI()));
    }

    @ExceptionHandler(com.uiusimulator.advisor.exception.AdvisorUnavailableException.class)
    public ResponseEntity<ApiErrorResponse> handleAdvisorUnavailable(
            com.uiusimulator.advisor.exception.AdvisorUnavailableException ex,
            HttpServletRequest request
    ) {
        log.warn("Advisor unavailable: {}", ex.getMessage());
        return ResponseEntity.status(HttpStatus.SERVICE_UNAVAILABLE)
                .body(ApiErrorResponse.of("Advisor is unavailable right now. Please try again.", request.getRequestURI()));
    }

    @ExceptionHandler(DataAccessException.class)
    public ResponseEntity<ApiErrorResponse> handleDataAccess(
            DataAccessException ex,
            HttpServletRequest request
    ) {
        log.error("Database error", ex);
        return ResponseEntity.status(HttpStatus.INTERNAL_SERVER_ERROR)
                .body(ApiErrorResponse.of("Database error", request.getRequestURI()));
    }

    @ExceptionHandler(Exception.class)
    public ResponseEntity<ApiErrorResponse> handleUnexpected(
            Exception ex,
            HttpServletRequest request
    ) {
        log.error("Unexpected error", ex);
        return ResponseEntity.status(HttpStatus.INTERNAL_SERVER_ERROR)
                .body(ApiErrorResponse.of("Unexpected server error", request.getRequestURI()));
    }
}
