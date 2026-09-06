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
        @SuppressWarnings("unchecked")
        HttpResponse<String> response = mock(HttpResponse.class);
        when(response.statusCode()).thenReturn(200);
        when(response.body()).thenReturn("{\"jwt\":\"fresh.session.jwt\"}");
        when(httpClient.send(any(HttpRequest.class), any(HttpResponse.BodyHandler.class))).thenReturn(response);

        String jwt = clerkSessionService.createSessionToken("sess_123");

        assertThat(jwt).isEqualTo("fresh.session.jwt");
    }

    @Test
    void createSessionToken_httpError_throwsAuthenticationFailedException() throws Exception {
        when(clerkProperties.secretKey()).thenReturn("sk_test_123");
        @SuppressWarnings("unchecked")
        HttpResponse<String> response = mock(HttpResponse.class);
        when(response.statusCode()).thenReturn(401);
        when(httpClient.send(any(HttpRequest.class), any(HttpResponse.BodyHandler.class))).thenReturn(response);

        assertThatThrownBy(() -> clerkSessionService.createSessionToken("sess_123"))
                .isInstanceOf(AuthenticationFailedException.class)
                .hasMessageContaining("Failed to mint Clerk session token");
    }

    @Test
    void createSessionToken_missingConfig_throwsAuthenticationFailedException() {
        when(clerkProperties.secretKey()).thenReturn("");

        assertThatThrownBy(() -> clerkSessionService.createSessionToken("sess_123"))
                .isInstanceOf(AuthenticationFailedException.class)
                .hasMessageContaining("Clerk secret key is not configured");
    }
}
