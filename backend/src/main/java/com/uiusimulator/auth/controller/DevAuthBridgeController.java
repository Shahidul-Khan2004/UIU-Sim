package com.uiusimulator.auth.controller;

import com.uiusimulator.auth.service.DevAuthBridgeService;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.validation.Valid;
import jakarta.validation.constraints.NotBlank;
import java.time.Instant;
import java.util.Map;
import java.util.Optional;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

/**
 * Development-only HTTP bridge so Unity Editor on Linux can receive Clerk JWTs
 * and rotate in-memory bearer refresh credentials without OS custom-scheme registration.
 */
@RestController
@RequestMapping("/auth/dev")
public class DevAuthBridgeController {

    private final DevAuthBridgeService bridgeService;

    public DevAuthBridgeController(DevAuthBridgeService bridgeService) {
        this.bridgeService = bridgeService;
    }

    public record CompleteRequest(String token, String sessionId) {
        public CompleteRequest(String token) {
            this(token, null);
        }
    }

    public record RefreshRequest(
            @NotBlank(message = "bridgeSessionId is required")
            String bridgeSessionId,

            @NotBlank(message = "refreshSecret is required")
            String refreshSecret
    ) {
    }

    @PostMapping("/bridge/{sessionId}")
    public ResponseEntity<Map<String, Object>> complete(
            @PathVariable String sessionId,
            @RequestBody CompleteRequest body
    ) {
        bridgeService.putToken(sessionId, body.token(), body.sessionId());
        return ResponseEntity.ok(Map.of(
                "success", true,
                "message", "Token stored for Unity. You can return to the Editor."
        ));
    }

    @GetMapping("/bridge/{sessionId}")
    public ResponseEntity<Map<String, Object>> poll(@PathVariable String sessionId) {
        Optional<DevAuthBridgeService.InitialHandshake> handshake = bridgeService.consumeInitialToken(sessionId);
        if (handshake.isEmpty()) {
            return ResponseEntity.ok(Map.of(
                    "success", true,
                    "ready", false
            ));
        }

        DevAuthBridgeService.InitialHandshake hs = handshake.get();
        return ResponseEntity.ok(Map.of(
                "success", true,
                "ready", true,
                "token", hs.token(),
                "refreshSecret", hs.refreshSecret(),
                "bridgeSessionId", sessionId
        ));
    }

    @PostMapping("/bridge/refresh")
    public ResponseEntity<Map<String, Object>> refresh(
            @Valid @RequestBody RefreshRequest body,
            HttpServletRequest request
    ) {
        Optional<DevAuthBridgeService.RefreshResult> result =
                bridgeService.refreshAndRotate(body.bridgeSessionId(), body.refreshSecret());

        if (result.isEmpty()) {
            return ResponseEntity.status(HttpStatus.UNAUTHORIZED).body(Map.of(
                    "success", false,
                    "message", "Authentication failed",
                    "timestamp", Instant.now().toString(),
                    "path", request.getRequestURI()
            ));
        }

        DevAuthBridgeService.RefreshResult res = result.get();
        return ResponseEntity.ok(Map.of(
                "success", true,
                "token", res.token(),
                "refreshSecret", res.refreshSecret(),
                "expiresInSeconds", res.expiresInSeconds()
        ));
    }
}
