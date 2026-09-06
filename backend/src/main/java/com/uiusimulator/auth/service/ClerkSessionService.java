package com.uiusimulator.auth.service;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.uiusimulator.auth.exception.AuthenticationFailedException;
import com.uiusimulator.config.ClerkProperties;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.time.Duration;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;

/**
 * Service communicating directly with Clerk's official Backend API using privileged CLERK_SECRET_KEY.
 * Never logs raw secrets or full JWT tokens.
 */
@Service
public class ClerkSessionService {

    private static final Logger log = LoggerFactory.getLogger(ClerkSessionService.class);
    private static final String CLERK_API_BASE_URL = "https://api.clerk.com/v1";

    private final ClerkProperties clerkProperties;
    private final HttpClient httpClient;
    private final ObjectMapper objectMapper;

    @org.springframework.beans.factory.annotation.Autowired
    public ClerkSessionService(ClerkProperties clerkProperties, ObjectMapper objectMapper) {
        this(clerkProperties, objectMapper, HttpClient.newBuilder()
                .connectTimeout(Duration.ofSeconds(5))
                .build());
    }

    public ClerkSessionService(ClerkProperties clerkProperties, ObjectMapper objectMapper, HttpClient httpClient) {
        this.clerkProperties = clerkProperties;
        this.objectMapper = objectMapper;
        this.httpClient = httpClient;
    }

    /**
     * Queries Clerk's Backend API to determine if the given session is active.
     * Returns false if the session is revoked, ended, abandoned, expired, or non-existent.
     */
    public boolean isSessionActive(String clerkSessionId) {
        if (clerkSessionId == null || clerkSessionId.isBlank()) {
            return false;
        }
        String secretKey = clerkProperties.secretKey();
        if (secretKey == null || secretKey.isBlank()) {
            log.error("Clerk secret key is missing in configuration");
            return false;
        }

        try {
            HttpRequest request = HttpRequest.newBuilder()
                    .uri(URI.create(CLERK_API_BASE_URL + "/sessions/" + clerkSessionId))
                    .header("Authorization", "Bearer " + secretKey)
                    .header("User-Agent", "UIU-Sim-Backend/1.0")
                    .timeout(Duration.ofSeconds(5))
                    .GET()
                    .build();

            HttpResponse<String> response = httpClient.send(request, HttpResponse.BodyHandlers.ofString());
            if (response.statusCode() != 200) {
                log.warn("Clerk session check returned HTTP {} for sessionId={}",
                        response.statusCode(), maskId(clerkSessionId));
                return false;
            }

            JsonNode root = objectMapper.readTree(response.body());
            String status = root.path("status").asText("");
            boolean active = "active".equalsIgnoreCase(status);
            if (!active) {
                log.info("Clerk session is not active (status={}) for sessionId={}",
                        status, maskId(clerkSessionId));
            }
            return active;
        } catch (InterruptedException ex) {
            Thread.currentThread().interrupt();
            log.error("Clerk session check interrupted for sessionId={}", maskId(clerkSessionId));
            return false;
        } catch (Exception ex) {
            log.error("Clerk session check failed for sessionId={}: {}",
                    maskId(clerkSessionId), ex.getMessage());
            return false;
        }
    }

    /**
     * Mints a fresh session token from Clerk Backend API for an active session.
     * The token is signed with Clerk's official RS256 key.
     */
    public String createSessionToken(String clerkSessionId) {
        if (clerkSessionId == null || clerkSessionId.isBlank()) {
            throw new AuthenticationFailedException("Clerk session ID is missing");
        }
        String secretKey = clerkProperties.secretKey();
        if (secretKey == null || secretKey.isBlank()) {
            throw new AuthenticationFailedException("Clerk secret key is not configured");
        }

        try {
            String requestBody = "{\"expires_in_seconds\":60}";
            HttpRequest request = HttpRequest.newBuilder()
                    .uri(URI.create(CLERK_API_BASE_URL + "/sessions/" + clerkSessionId + "/tokens"))
                    .header("Authorization", "Bearer " + secretKey)
                    .header("Content-Type", "application/json")
                    .header("User-Agent", "UIU-Sim-Backend/1.0")
                    .timeout(Duration.ofSeconds(5))
                    .POST(HttpRequest.BodyPublishers.ofString(requestBody))
                    .build();

            HttpResponse<String> response = httpClient.send(request, HttpResponse.BodyHandlers.ofString());
            if (response.statusCode() != 200) {
                log.warn("Clerk session token creation failed with HTTP {} for sessionId={}",
                        response.statusCode(), maskId(clerkSessionId));
                throw new AuthenticationFailedException("Failed to mint Clerk session token");
            }

            JsonNode root = objectMapper.readTree(response.body());
            String jwt = root.path("jwt").asText("");
            if (jwt.isBlank()) {
                throw new AuthenticationFailedException("Clerk returned empty session token");
            }

            log.info("Successfully minted fresh Clerk session token for sessionId={}", maskId(clerkSessionId));
            return jwt;
        } catch (AuthenticationFailedException ex) {
            throw ex;
        } catch (InterruptedException ex) {
            Thread.currentThread().interrupt();
            throw new AuthenticationFailedException("Token creation was interrupted");
        } catch (Exception ex) {
            log.error("Clerk token creation failed for sessionId={}: {}", maskId(clerkSessionId), ex.getMessage());
            throw new AuthenticationFailedException("Authentication service communication error");
        }
    }

    private static String maskId(String id) {
        if (id == null || id.length() <= 8) {
            return "***";
        }
        return id.substring(0, 8) + "...";
    }
}
