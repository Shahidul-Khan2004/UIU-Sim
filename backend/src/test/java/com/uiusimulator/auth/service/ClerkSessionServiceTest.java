package com.uiusimulator.auth.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.uiusimulator.auth.exception.AuthenticationFailedException;
import com.uiusimulator.config.ClerkProperties;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

@ExtendWith(MockitoExtension.class)
class ClerkSessionServiceTest {

    @Mock
    private ClerkProperties clerkProperties;

    @Mock
    private HttpClient httpClient;

    private final ObjectMapper objectMapper = new ObjectMapper();
    private ClerkSessionService clerkSessionService;

    @BeforeEach
    void setUp() {
        clerkSessionService = new ClerkSessionService(clerkProperties, objectMapper, httpClient);
    }

    @Test
    void isSessionActive_whenActive_returnsTrue() throws Exception {
        when(clerkProperties.secretKey()).thenReturn("sk_test_123");
        @SuppressWarnings("unchecked")
        HttpResponse<String> response = mock(HttpResponse.class);
        when(response.statusCode()).thenReturn(200);
        when(response.body()).thenReturn("{\"status\":\"active\"}");
        when(httpClient.send(any(HttpRequest.class), any(HttpResponse.BodyHandler.class))).thenReturn(response);

        boolean active = clerkSessionService.isSessionActive("sess_123");

        assertThat(active).isTrue();
    }

    @Test
    void isSessionActive_whenRevokedOrEnded_returnsFalse() throws Exception {
        when(clerkProperties.secretKey()).thenReturn("sk_test_123");
        @SuppressWarnings("unchecked")
        HttpResponse<String> response = mock(HttpResponse.class);
        when(response.statusCode()).thenReturn(200);
        when(response.body()).thenReturn("{\"status\":\"revoked\"}");
        when(httpClient.send(any(HttpRequest.class), any(HttpResponse.BodyHandler.class))).thenReturn(response);

        boolean active = clerkSessionService.isSessionActive("sess_123");

        assertThat(active).isFalse();
    }

    @Test
    void isSessionActive_whenNotFoundOrErrorStatus_returnsFalse() throws Exception {
        when(clerkProperties.secretKey()).thenReturn("sk_test_123");
        @SuppressWarnings("unchecked")
        HttpResponse<String> response = mock(HttpResponse.class);
        when(response.statusCode()).thenReturn(404);
        when(httpClient.send(any(HttpRequest.class), any(HttpResponse.BodyHandler.class))).thenReturn(response);

        boolean active = clerkSessionService.isSessionActive("sess_404");

        assertThat(active).isFalse();
    }

    @Test
    void isSessionActive_missingSessionIdOrSecretKey_returnsFalse() {
        assertThat(clerkSessionService.isSessionActive(null)).isFalse();
        assertThat(clerkSessionService.isSessionActive("")).isFalse();

        when(clerkProperties.secretKey()).thenReturn(null);
        assertThat(clerkSessionService.isSessionActive("sess_123")).isFalse();
    }

    @Test
    void createSessionToken_success_returnsMintedJwt() throws Exception {
        when(clerkProperties.secretKey()).thenReturn("sk_test_123");
        when(clerkProperties.effectiveGameTokenTtlSeconds()).thenReturn(60);
        @SuppressWarnings("unchecked")
        HttpResponse<String> response = mock(HttpResponse.class);
        when(response.statusCode()).thenReturn(200);
        when(response.body()).thenReturn("{\"jwt\":\"fresh.session.jwt\"}");
        when(httpClient.send(any(HttpRequest.class), any(HttpResponse.BodyHandler.class))).thenReturn(response);

        String jwt = clerkSessionService.createSessionToken("sess_123");

        assertThat(jwt).isEqualTo("fresh.session.jwt");
    }

    @Test
    void createSessionToken_withExplicitTtl_sendsConfiguredTtlToClerk() throws Exception {
        when(clerkProperties.secretKey()).thenReturn("sk_test_123");
        @SuppressWarnings("unchecked")
        HttpResponse<String> response = mock(HttpResponse.class);
        when(response.statusCode()).thenReturn(200);
        when(response.body()).thenReturn("{\"jwt\":\"long-lived.session.jwt\"}");

        ArgumentCaptor<HttpRequest> requestCaptor = ArgumentCaptor.forClass(HttpRequest.class);
        when(httpClient.send(requestCaptor.capture(), any(HttpResponse.BodyHandler.class))).thenReturn(response);

        String jwt = clerkSessionService.createSessionToken("sess_123", 3600);

        assertThat(jwt).isEqualTo("long-lived.session.jwt");
        // Verify the HTTP request URI targets the correct session
        HttpRequest capturedRequest = requestCaptor.getValue();
        assertThat(capturedRequest.uri().toString()).contains("/sessions/sess_123/tokens");
    }

    @Test
    void createSessionToken_defaultOverload_usesConfiguredTtl() throws Exception {
        when(clerkProperties.secretKey()).thenReturn("sk_test_123");
        when(clerkProperties.effectiveGameTokenTtlSeconds()).thenReturn(3600);
        @SuppressWarnings("unchecked")
        HttpResponse<String> response = mock(HttpResponse.class);
        when(response.statusCode()).thenReturn(200);
        when(response.body()).thenReturn("{\"jwt\":\"configured.ttl.jwt\"}");
        when(httpClient.send(any(HttpRequest.class), any(HttpResponse.BodyHandler.class))).thenReturn(response);

        String jwt = clerkSessionService.createSessionToken("sess_123");

        assertThat(jwt).isEqualTo("configured.ttl.jwt");
    }

    @Test
    void createSessionToken_nonPositiveTtl_throwsAuthenticationFailedException() {
        assertThatThrownBy(() -> clerkSessionService.createSessionToken("sess_123", 0))
                .isInstanceOf(AuthenticationFailedException.class)
                .hasMessageContaining("Token TTL must be a positive number of seconds");

        assertThatThrownBy(() -> clerkSessionService.createSessionToken("sess_123", -1))
                .isInstanceOf(AuthenticationFailedException.class)
                .hasMessageContaining("Token TTL must be a positive number of seconds");
    }

    @Test
    void createSessionToken_httpError_throwsAuthenticationFailedException() throws Exception {
        when(clerkProperties.secretKey()).thenReturn("sk_test_123");
        @SuppressWarnings("unchecked")
        HttpResponse<String> response = mock(HttpResponse.class);
        when(response.statusCode()).thenReturn(401);
        when(httpClient.send(any(HttpRequest.class), any(HttpResponse.BodyHandler.class))).thenReturn(response);

        assertThatThrownBy(() -> clerkSessionService.createSessionToken("sess_123", 60))
                .isInstanceOf(AuthenticationFailedException.class)
                .hasMessageContaining("Failed to mint Clerk session token");
    }

    @Test
    void createSessionToken_missingConfig_throwsAuthenticationFailedException() {
        when(clerkProperties.secretKey()).thenReturn("");

        assertThatThrownBy(() -> clerkSessionService.createSessionToken("sess_123", 60))
                .isInstanceOf(AuthenticationFailedException.class)
                .hasMessageContaining("Clerk secret key is not configured");
    }
}
