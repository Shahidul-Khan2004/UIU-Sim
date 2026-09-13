package com.uiusimulator.auth.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.anyInt;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.uiusimulator.auth.exception.AuthenticationFailedException;
import com.uiusimulator.config.ClerkProperties;
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

    @Mock
    private ClerkProperties clerkProperties;

    private final ObjectMapper objectMapper = new ObjectMapper();
    private DevAuthBridgeService bridgeService;

    @BeforeEach
    void setUp() {
        bridgeService = new DevAuthBridgeService(clerkSessionService, clerkProperties, objectMapper);
    }

    @Test
    void putTokenAndConsume_mintsBackendTokenWithConfiguredTtl() {
        when(clerkProperties.effectiveGameTokenTtlSeconds()).thenReturn(3600);
        when(clerkSessionService.createSessionToken("sess_123", 3600)).thenReturn("minted.game.jwt");

        bridgeService.putToken("bridge-1", "browser-issued.short.jwt", "sess_123");

        Optional<DevAuthBridgeService.InitialHandshake> handshake = bridgeService.consumeInitialToken("bridge-1");

        assertThat(handshake).isPresent();
        // Should receive backend-minted JWT, NOT the browser-issued one
        assertThat(handshake.get().token()).isEqualTo("minted.game.jwt");
        assertThat(handshake.get().refreshSecret()).isNotBlank();
        verify(clerkSessionService).createSessionToken("sess_123", 3600);

        // Second consume should fail (single-delivery)
        Optional<DevAuthBridgeService.InitialHandshake> secondConsume = bridgeService.consumeInitialToken("bridge-1");
        assertThat(secondConsume).isEmpty();
    }

    @Test
    void consumeInitialToken_withDefaultTtl60_mintsWithDefault() {
        when(clerkProperties.effectiveGameTokenTtlSeconds()).thenReturn(60);
        when(clerkSessionService.createSessionToken("sess_123", 60)).thenReturn("default-ttl.jwt");

        bridgeService.putToken("bridge-default", "browser.jwt", "sess_123");

        Optional<DevAuthBridgeService.InitialHandshake> handshake = bridgeService.consumeInitialToken("bridge-default");

        assertThat(handshake).isPresent();
        assertThat(handshake.get().token()).isEqualTo("default-ttl.jwt");
        verify(clerkSessionService).createSessionToken("sess_123", 60);
    }

    @Test
    void consumeInitialToken_whenMintingFails_returnEmptyAndLeavesBridgeIntact() {
        when(clerkProperties.effectiveGameTokenTtlSeconds()).thenReturn(3600);
        when(clerkSessionService.createSessionToken("sess_123", 3600))
                .thenThrow(new AuthenticationFailedException("Clerk API unavailable"))
                .thenReturn("retry.success.jwt");

        bridgeService.putToken("bridge-retry", "browser.jwt", "sess_123");

        // First attempt fails due to Clerk API error
        Optional<DevAuthBridgeService.InitialHandshake> attempt1 = bridgeService.consumeInitialToken("bridge-retry");
        assertThat(attempt1).isEmpty();

        // Bridge session should still be intact for retry
        assertThat(bridgeService.isPending("bridge-retry")).isTrue();

        // Retry succeeds after Clerk recovers
        Optional<DevAuthBridgeService.InitialHandshake> attempt2 = bridgeService.consumeInitialToken("bridge-retry");
        assertThat(attempt2).isPresent();
        assertThat(attempt2.get().token()).isEqualTo("retry.success.jwt");
    }

    @Test
    void refreshAndRotate_withValidSecret_rotatesSecretAndMintsNewJwtWithConfiguredTtl() {
        when(clerkProperties.effectiveGameTokenTtlSeconds()).thenReturn(3600);
        when(clerkSessionService.createSessionToken("sess_123", 3600))
                .thenReturn("initial.game.jwt");

        bridgeService.putToken("bridge-1", "browser.jwt", "sess_123");
        var handshake = bridgeService.consumeInitialToken("bridge-1").orElseThrow();
        String secretA = handshake.refreshSecret();

        when(clerkSessionService.isSessionActive("sess_123")).thenReturn(true);
        when(clerkSessionService.createSessionToken("sess_123", 3600)).thenReturn("minted.jwt.1");

        // First refresh with Secret A
        Optional<DevAuthBridgeService.RefreshResult> refresh1 =
                bridgeService.refreshAndRotate("bridge-1", secretA);

        assertThat(refresh1).isPresent();
        assertThat(refresh1.get().token()).isEqualTo("minted.jwt.1");
        assertThat(refresh1.get().expiresInSeconds()).isEqualTo(3600);
        String secretB = refresh1.get().refreshSecret();
        assertThat(secretB).isNotEqualTo(secretA);

        // Replaying Secret A must fail immediately (invalidated)
        Optional<DevAuthBridgeService.RefreshResult> replay =
                bridgeService.refreshAndRotate("bridge-1", secretA);
        assertThat(replay).isEmpty();

        // Subsequent refresh with Secret B succeeds
        when(clerkSessionService.createSessionToken("sess_123", 3600)).thenReturn("minted.jwt.2");
        Optional<DevAuthBridgeService.RefreshResult> refresh2 =
                bridgeService.refreshAndRotate("bridge-1", secretB);

        assertThat(refresh2).isPresent();
        assertThat(refresh2.get().token()).isEqualTo("minted.jwt.2");
        assertThat(refresh2.get().expiresInSeconds()).isEqualTo(3600);
        String secretC = refresh2.get().refreshSecret();
        assertThat(secretC).isNotEqualTo(secretB);
    }

    @Test
    void refreshAndRotate_withDefault60Ttl_reportsCorrectExpiresInSeconds() {
        when(clerkProperties.effectiveGameTokenTtlSeconds()).thenReturn(60);
        when(clerkSessionService.createSessionToken("sess_123", 60)).thenReturn("initial.jwt");

        bridgeService.putToken("bridge-ttl60", "browser.jwt", "sess_123");
        var handshake = bridgeService.consumeInitialToken("bridge-ttl60").orElseThrow();

        when(clerkSessionService.isSessionActive("sess_123")).thenReturn(true);
        when(clerkSessionService.createSessionToken("sess_123", 60)).thenReturn("refreshed.jwt");

        Optional<DevAuthBridgeService.RefreshResult> result =
                bridgeService.refreshAndRotate("bridge-ttl60", handshake.refreshSecret());

        assertThat(result).isPresent();
        assertThat(result.get().expiresInSeconds()).isEqualTo(60);
    }

    @Test
    void refresh_whenClerkSessionInactive_failsAndRemovesSession() {
        when(clerkProperties.effectiveGameTokenTtlSeconds()).thenReturn(60);
        when(clerkSessionService.createSessionToken("sess_inactive", 60)).thenReturn("initial.jwt");

        bridgeService.putToken("bridge-inactive", "browser.jwt", "sess_inactive");
        var handshake = bridgeService.consumeInitialToken("bridge-inactive").orElseThrow();
        String secret = handshake.refreshSecret();

        // Reset invocations so verify(never()) only covers the refresh call, not consume setup
        org.mockito.Mockito.clearInvocations(clerkSessionService);

        when(clerkSessionService.isSessionActive("sess_inactive")).thenReturn(false);

        Optional<DevAuthBridgeService.RefreshResult> result =
                bridgeService.refreshAndRotate("bridge-inactive", secret);

        assertThat(result).isEmpty();
        verify(clerkSessionService, never()).createSessionToken(anyString(), anyInt());

        // Session was removed, so subsequent refresh attempts also fail
        Optional<DevAuthBridgeService.RefreshResult> subsequent =
                bridgeService.refreshAndRotate("bridge-inactive", secret);
        assertThat(subsequent).isEmpty();
    }

    @Test
    void refresh_lockoutAfterFiveFailedAttempts() {
        when(clerkProperties.effectiveGameTokenTtlSeconds()).thenReturn(60);
        when(clerkSessionService.createSessionToken("sess_123", 60)).thenReturn("initial.jwt");

        bridgeService.putToken("bridge-lockout", "browser.jwt", "sess_123");
        var handshake = bridgeService.consumeInitialToken("bridge-lockout").orElseThrow();
        String validSecret = handshake.refreshSecret();

        // Reset invocations so verify(never()) only covers the refresh calls, not consume setup
        org.mockito.Mockito.clearInvocations(clerkSessionService);

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
        verify(clerkSessionService, never()).createSessionToken(anyString(), anyInt());
    }

    @Test
    void refresh_unknownSession_returnsEmpty() {
        Optional<DevAuthBridgeService.RefreshResult> result =
                bridgeService.refreshAndRotate("non-existent-session", "any-secret");
        assertThat(result).isEmpty();
    }
}
