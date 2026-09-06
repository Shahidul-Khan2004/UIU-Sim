package com.uiusimulator.auth.controller;

import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.uiusimulator.auth.security.ClerkJwtAuthenticationFilter;
import com.uiusimulator.auth.service.DevAuthBridgeService;
import com.uiusimulator.common.exception.GlobalExceptionHandler;
import com.uiusimulator.config.ClerkProperties;
import com.uiusimulator.config.SecurityConfig;
import java.util.Optional;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.context.properties.EnableConfigurationProperties;
import org.springframework.boot.test.autoconfigure.web.servlet.WebMvcTest;
import org.springframework.context.annotation.Import;
import org.springframework.http.MediaType;
import org.springframework.security.oauth2.jwt.JwtDecoder;
import org.springframework.test.context.ActiveProfiles;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;

@WebMvcTest(DevAuthBridgeController.class)
@ActiveProfiles("test")
@EnableConfigurationProperties(ClerkProperties.class)
@Import({SecurityConfig.class, ClerkJwtAuthenticationFilter.class, GlobalExceptionHandler.class})
class DevAuthBridgeControllerTest {

    @Autowired
    private MockMvc mockMvc;

    @Autowired
    private ObjectMapper objectMapper;

    @MockitoBean
    private DevAuthBridgeService bridgeService;

    @MockitoBean
    private JwtDecoder jwtDecoder;

    @Test
    void complete_storesTokenAndReturnsSuccess() throws Exception {
        var body = new DevAuthBridgeController.CompleteRequest("sample.jwt.token", "sess_123");

        mockMvc.perform(post("/auth/dev/bridge/session-abc")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(objectMapper.writeValueAsString(body)))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.success").value(true))
                .andExpect(jsonPath("$.message").exists());

        verify(bridgeService).putToken("session-abc", "sample.jwt.token", "sess_123");
    }

    @Test
    void poll_whenReady_returnsTokenAndSecret() throws Exception {
        when(bridgeService.consumeInitialToken("session-abc"))
                .thenReturn(Optional.of(new DevAuthBridgeService.InitialHandshake("sample.jwt.token", "secret-xyz")));

        mockMvc.perform(get("/auth/dev/bridge/session-abc"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.success").value(true))
                .andExpect(jsonPath("$.ready").value(true))
                .andExpect(jsonPath("$.token").value("sample.jwt.token"))
                .andExpect(jsonPath("$.refreshSecret").value("secret-xyz"))
                .andExpect(jsonPath("$.bridgeSessionId").value("session-abc"));
    }

    @Test
    void poll_whenNotReady_returnsReadyFalse() throws Exception {
        when(bridgeService.consumeInitialToken("session-abc")).thenReturn(Optional.empty());

        mockMvc.perform(get("/auth/dev/bridge/session-abc"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.success").value(true))
                .andExpect(jsonPath("$.ready").value(false));
    }

    @Test
    void refresh_validCredentials_returnsNewTokenAndRotatedSecret() throws Exception {
        when(bridgeService.refreshAndRotate(eq("bridge-123"), eq("secret-old")))
                .thenReturn(Optional.of(new DevAuthBridgeService.RefreshResult("new.jwt.token", "secret-new", 60)));

        var body = new DevAuthBridgeController.RefreshRequest("bridge-123", "secret-old");

        mockMvc.perform(post("/auth/dev/bridge/refresh")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(objectMapper.writeValueAsString(body)))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.success").value(true))
                .andExpect(jsonPath("$.token").value("new.jwt.token"))
                .andExpect(jsonPath("$.refreshSecret").value("secret-new"))
                .andExpect(jsonPath("$.expiresInSeconds").value(60));
    }

    @Test
    void refresh_invalidOrExpiredCredentials_returns401Unauthorized() throws Exception {
        when(bridgeService.refreshAndRotate(eq("bridge-123"), eq("wrong-secret")))
                .thenReturn(Optional.empty());

        var body = new DevAuthBridgeController.RefreshRequest("bridge-123", "wrong-secret");

        mockMvc.perform(post("/auth/dev/bridge/refresh")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(objectMapper.writeValueAsString(body)))
                .andExpect(status().isUnauthorized())
                .andExpect(jsonPath("$.success").value(false))
                .andExpect(jsonPath("$.message").value("Authentication failed"));
    }

    @Test
    void refresh_blankInputs_returns400BadRequest() throws Exception {
        var body = new DevAuthBridgeController.RefreshRequest("", "   ");

        mockMvc.perform(post("/auth/dev/bridge/refresh")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(objectMapper.writeValueAsString(body)))
                .andExpect(status().isBadRequest());
    }
}
