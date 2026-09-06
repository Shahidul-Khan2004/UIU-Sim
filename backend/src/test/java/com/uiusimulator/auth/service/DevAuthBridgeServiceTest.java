package com.uiusimulator.auth.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

import com.fasterxml.jackson.databind.ObjectMapper;
import java.util.Optional;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

@ExtendWith(MockitoExtension.class)
class DevAuthBridgeServiceTest {

    @Mock
    private ClerkSessionService clerkSessionService;

    private final ObjectMapper objectMapper = new ObjectMapper();
    private DevAuthBridgeService bridgeService;

    @BeforeEach
    void setUp() {
        bridgeService = new DevAuthBridgeService(clerkSessionService, objectMapper);
    }

    @Test
    void putTokenAndConsume_deliversInitialTokenAndSecretOnce() {
        bridgeService.putToken("bridge-1", "sample.initial.jwt", "sess_123");

        Optional<DevAuthBridgeService.InitialHandshake> handshake = bridgeService.consumeInitialToken("bridge-1");

        assertThat(handshake).isPresent();
        assertThat(handshake.get().token()).isEqualTo("sample.initial.jwt");
        assertThat(handshake.get().refreshSecret()).isNotBlank();

        // Second consume should fail (single-delivery)
        Optional<DevAuthBridgeService.InitialHandshake> secondConsume = bridgeService.consumeInitialToken("bridge-1");
        assertThat(secondConsume).isEmpty();
    }

    @Test
    void refreshAndRotate_withValidSecret_rotatesSecretAndMintsNewJwt() {
        bridgeService.putToken("bridge-1", "sample.initial.jwt", "sess_123");
        var handshake = bridgeService.consumeInitialToken("bridge-1").orElseThrow();
        String secretA = handshake.refreshSecret();

        when(clerkSessionService.isSessionActive("sess_123")).thenReturn(true);
        when(clerkSessionService.createSessionToken("sess_123")).thenReturn("minted.jwt.1");

        // First refresh with Secret A
        Optional<DevAuthBridgeService.RefreshResult> refresh1 =
                bridgeService.refreshAndRotate("bridge-1", secretA);

        assertThat(refresh1).isPresent();
        assertThat(refresh1.get().token()).isEqualTo("minted.jwt.1");
        assertThat(refresh1.get().expiresInSeconds()).isEqualTo(60);
        String secretB = refresh1.get().refreshSecret();
        assertThat(secretB).isNotEqualTo(secretA);

        // Replaying Secret A must fail immediately (invalidated)
        Optional<DevAuthBridgeService.RefreshResult> replay =
                bridgeService.refreshAndRotate("bridge-1", secretA);
        assertThat(replay).isEmpty();

        // Subsequent refresh with Secret B succeeds
        when(clerkSessionService.createSessionToken("sess_123")).thenReturn("minted.jwt.2");
        Optional<DevAuthBridgeService.RefreshResult> refresh2 =
                bridgeService.refreshAndRotate("bridge-1", secretB);

        assertThat(refresh2).isPresent();
        assertThat(refresh2.get().token()).isEqualTo("minted.jwt.2");
        String secretC = refresh2.get().refreshSecret();
        assertThat(secretC).isNotEqualTo(secretB);
    }

    @Test
    void refresh_whenClerkSessionInactive_failsAndRemovesSession() {
        bridgeService.putToken("bridge-inactive", "sample.initial.jwt", "sess_inactive");
        var handshake = bridgeService.consumeInitialToken("bridge-inactive").orElseThrow();
        String secret = handshake.refreshSecret();

        when(clerkSessionService.isSessionActive("sess_inactive")).thenReturn(false);

        Optional<DevAuthBridgeService.RefreshResult> result =
                bridgeService.refreshAndRotate("bridge-inactive", secret);

        assertThat(result).isEmpty();
        verify(clerkSessionService, never()).createSessionToken(anyString());

        // Session was removed, so subsequent refresh attempts also fail
        Optional<DevAuthBridgeService.RefreshResult> subsequent =
                bridgeService.refreshAndRotate("bridge-inactive", secret);
        assertThat(subsequent).isEmpty();
    }

    @Test
    void refresh_lockoutAfterFiveFailedAttempts() {
        bridgeService.putToken("bridge-lockout", "sample.initial.jwt", "sess_123");
        var handshake = bridgeService.consumeInitialToken("bridge-lockout").orElseThrow();
        String validSecret = handshake.refreshSecret();

        // 5 consecutive wrong secret attempts
        for (int i = 0; i < 5; i++) {
            Optional<DevAuthBridgeService.RefreshResult> wrong =
                    bridgeService.refreshAndRotate("bridge-lockout", "invalid-secret-" + i);
            assertThat(wrong).isEmpty();
        }

        // Even with valid secret, 6th attempt fails due to lockout
        Optional<DevAuthBridgeService.RefreshResult> lockedOut =
                bridgeService.refreshAndRotate("bridge-lockout", validSecret);
        assertThat(lockedOut).isEmpty();
        verify(clerkSessionService, never()).createSessionToken(anyString());
    }

    @Test
    void refresh_unknownSession_returnsEmpty() {
        Optional<DevAuthBridgeService.RefreshResult> result =
                bridgeService.refreshAndRotate("non-existent-session", "any-secret");
        assertThat(result).isEmpty();
    }
}
