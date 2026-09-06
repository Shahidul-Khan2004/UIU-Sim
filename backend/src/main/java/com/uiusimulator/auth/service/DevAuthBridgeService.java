package com.uiusimulator.auth.service;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.security.SecureRandom;
import java.time.Duration;
import java.time.Instant;
import java.util.Base64;
import java.util.Iterator;
import java.util.Map;
import java.util.Optional;
import java.util.concurrent.ConcurrentHashMap;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;

/**
 * In-memory bridge service for Unity development client.
 * Issues and rotates application-defined bearer refresh credentials (refreshSecret)
 * bound to an active Clerk session, enabling headless session token refreshing without
 * persisting sensitive master credentials on the client.
 */
@Service
public class DevAuthBridgeService {

    private static final Logger log = LoggerFactory.getLogger(DevAuthBridgeService.class);
    private static final Duration INITIAL_TOKEN_TTL = Duration.ofMinutes(5);
    private static final Duration BRIDGE_SESSION_TTL = Duration.ofHours(24);
    private static final int MAX_FAILED_ATTEMPTS = 5;

    private final SecureRandom secureRandom = new SecureRandom();
    private final ConcurrentHashMap<String, BridgeSession> sessions = new ConcurrentHashMap<>();
    private final ClerkSessionService clerkSessionService;
    private final ObjectMapper objectMapper;

    public DevAuthBridgeService(ClerkSessionService clerkSessionService, ObjectMapper objectMapper) {
        this.clerkSessionService = clerkSessionService;
        this.objectMapper = objectMapper;
    }

    public record InitialHandshake(String token, String refreshSecret) {
    }

    public record RefreshResult(String token, String refreshSecret, long expiresInSeconds) {
    }

    /**
     * Called when browser completes login. Generates initial refreshSecret and stores
     * its SHA-256 hash alongside the verified Clerk session ID.
     */
    public void putToken(String bridgeSessionId, String token, String clerkSessionId) {
        purgeExpired();
        if (bridgeSessionId == null || bridgeSessionId.isBlank() || token == null || token.isBlank()) {
            throw new IllegalArgumentException("bridgeSessionId and token are required");
        }

        String effectiveClerkSessionId = clerkSessionId;
        if (effectiveClerkSessionId == null || effectiveClerkSessionId.isBlank()) {
            effectiveClerkSessionId = extractClerkSessionId(token);
        }

        if (effectiveClerkSessionId == null || effectiveClerkSessionId.isBlank()) {
            throw new IllegalArgumentException("Clerk session ID is missing and could not be extracted from token");
        }

        String initialSecret = generateSecureSecret();
        byte[] secretHash = sha256(initialSecret);

        Instant now = Instant.now();
        BridgeSession session = new BridgeSession(
                bridgeSessionId,
                effectiveClerkSessionId,
                secretHash,
                token,
                now.plus(INITIAL_TOKEN_TTL),
                now.plus(BRIDGE_SESSION_TTL),
                0
        );

        sessions.put(bridgeSessionId, session);
        log.info("Bridge session established for bridgeSessionId={}", maskId(bridgeSessionId));
    }

    /**
     * Poll endpoint called by Unity. Consumes the initial JWT and delivers the initial refreshSecret.
     */
    public Optional<InitialHandshake> consumeInitialToken(String bridgeSessionId) {
        purgeExpired();
        if (bridgeSessionId == null || bridgeSessionId.isBlank()) {
            return Optional.empty();
        }

        BridgeSession session = sessions.get(bridgeSessionId);
        if (session == null || session.initialToken() == null) {
            return Optional.empty();
        }

        if (session.initialTokenExpiresAt().isBefore(Instant.now())) {
            sessions.remove(bridgeSessionId);
            return Optional.empty();
        }

        // Generate a fresh secret for the consumer to ensure single-delivery
        String secretForClient = generateSecureSecret();
        byte[] newHash = sha256(secretForClient);

        // Clear initial token so it cannot be consumed again, and bind to client's secret
        BridgeSession updated = new BridgeSession(
                session.bridgeSessionId(),
                session.clerkSessionId(),
                newHash,
                null,
                Instant.EPOCH,
                session.expiresAt(),
                0
        );
        sessions.put(bridgeSessionId, updated);

        log.info("Initial bridge token consumed for bridgeSessionId={}", maskId(bridgeSessionId));
        return Optional.of(new InitialHandshake(session.initialToken(), secretForClient));
    }

    /**
     * Rotates refresh secret and mints a fresh Clerk JWT after validating the presented secret
     * via constant-time hash comparison and confirming the Clerk session is active.
     */
    public Optional<RefreshResult> refreshAndRotate(String bridgeSessionId, String presentedSecret) {
        purgeExpired();
        if (bridgeSessionId == null || bridgeSessionId.isBlank()
                || presentedSecret == null || presentedSecret.isBlank()) {
            return Optional.empty();
        }

        BridgeSession session = sessions.get(bridgeSessionId);
        if (session == null || session.expiresAt().isBefore(Instant.now())) {
            sessions.remove(bridgeSessionId);
            log.warn("Refresh failed: Bridge session not found or expired for bridgeSessionId={}", maskId(bridgeSessionId));
            return Optional.empty();
        }

        if (session.failedAttempts() >= MAX_FAILED_ATTEMPTS) {
            sessions.remove(bridgeSessionId);
            log.warn("Bridge session locked out due to exceeding maximum failed attempts for bridgeSessionId={}",
                    maskId(bridgeSessionId));
            return Optional.empty();
        }

        byte[] presentedHash = sha256(presentedSecret);
        boolean matches = MessageDigest.isEqual(session.refreshSecretHash(), presentedHash);

        if (!matches) {
            int attempts = session.failedAttempts() + 1;
            sessions.put(bridgeSessionId, session.withFailedAttempts(attempts));
            log.warn("Invalid refreshSecret attempt ({}/{}) for bridgeSessionId={}",
                    attempts, MAX_FAILED_ATTEMPTS, maskId(bridgeSessionId));
            return Optional.empty();
        }

        // Verify that the underlying Clerk session is still active
        if (!clerkSessionService.isSessionActive(session.clerkSessionId())) {
            sessions.remove(bridgeSessionId);
            log.warn("Underlying Clerk session is not active for bridgeSessionId={}", maskId(bridgeSessionId));
            return Optional.empty();
        }

        // Mint a fresh Clerk session JWT
        String freshJwt = clerkSessionService.createSessionToken(session.clerkSessionId());

        // Rotate secret: generate Secret B, store SHA-256(B), invalidate Secret A
        String nextSecret = generateSecureSecret();
        byte[] nextSecretHash = sha256(nextSecret);

        BridgeSession rotated = new BridgeSession(
                session.bridgeSessionId(),
                session.clerkSessionId(),
                nextSecretHash,
                null,
                Instant.EPOCH,
                Instant.now().plus(BRIDGE_SESSION_TTL),
                0
        );

        sessions.put(bridgeSessionId, rotated);
        log.info("Token successfully refreshed and secret rotated for bridgeSessionId={}", maskId(bridgeSessionId));

        return Optional.of(new RefreshResult(freshJwt, nextSecret, 60));
    }

    public boolean isPending(String bridgeSessionId) {
        purgeExpired();
        BridgeSession session = sessions.get(bridgeSessionId);
        return session != null
                && session.initialToken() != null
                && session.initialTokenExpiresAt().isAfter(Instant.now());
    }

    private void purgeExpired() {
        Instant now = Instant.now();
        Iterator<Map.Entry<String, BridgeSession>> it = sessions.entrySet().iterator();
        while (it.hasNext()) {
            Map.Entry<String, BridgeSession> entry = it.next();
            if (entry.getValue().expiresAt().isBefore(now)) {
                it.remove();
            }
        }
    }

    private String generateSecureSecret() {
        byte[] bytes = new byte[32];
        secureRandom.nextBytes(bytes);
        return Base64.getUrlEncoder().withoutPadding().encodeToString(bytes);
    }

    private byte[] sha256(String input) {
        try {
            MessageDigest digest = MessageDigest.getInstance("SHA-256");
            return digest.digest(input.getBytes(StandardCharsets.UTF_8));
        } catch (NoSuchAlgorithmException ex) {
            throw new IllegalStateException("SHA-256 not available", ex);
        }
    }

    private String extractClerkSessionId(String token) {
        try {
            String[] parts = token.split("\\.");
            if (parts.length >= 2) {
                byte[] decoded = Base64.getUrlDecoder().decode(parts[1]);
                JsonNode payload = objectMapper.readTree(decoded);
                return payload.path("sid").asText(null);
            }
        } catch (Exception ex) {
            log.warn("Failed to extract sid from JWT token: {}", ex.getMessage());
        }
        return null;
    }

    private static String maskId(String id) {
        if (id == null || id.length() <= 8) {
            return "***";
        }
        return id.substring(0, 8) + "...";
    }

    private record BridgeSession(
            String bridgeSessionId,
            String clerkSessionId,
            byte[] refreshSecretHash,
            String initialToken,
            Instant initialTokenExpiresAt,
            Instant expiresAt,
            int failedAttempts
    ) {
        public BridgeSession withFailedAttempts(int newFailedAttempts) {
            return new BridgeSession(
                    bridgeSessionId,
                    clerkSessionId,
                    refreshSecretHash,
                    initialToken,
                    initialTokenExpiresAt,
                    expiresAt,
                    newFailedAttempts
            );
        }
    }
}
