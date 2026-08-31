package com.uiusimulator.auth.controller;

import com.uiusimulator.auth.service.DevAuthBridgeService;
import java.util.Map;
import java.util.Optional;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

/**
 * Development-only HTTP bridge so Unity Editor on Linux can receive Clerk JWTs
 * without OS custom-scheme registration.
 */
@RestController
@RequestMapping("/auth/dev")
public class DevAuthBridgeController {

    private final DevAuthBridgeService bridgeService;

    public DevAuthBridgeController(DevAuthBridgeService bridgeService) {
        this.bridgeService = bridgeService;
    }

    public record CompleteRequest(String token) {
    }

    @PostMapping("/bridge/{sessionId}")
    public ResponseEntity<Map<String, Object>> complete(
            @PathVariable String sessionId,
            @RequestBody CompleteRequest body
    ) {
        bridgeService.putToken(sessionId, body.token());
        return ResponseEntity.ok(Map.of(
                "success", true,
                "message", "Token stored for Unity. You can return to the Editor."
        ));
    }

    @GetMapping("/bridge/{sessionId}")
    public ResponseEntity<Map<String, Object>> poll(@PathVariable String sessionId) {
        Optional<String> token = bridgeService.consumeToken(sessionId);
        if (token.isEmpty()) {
            return ResponseEntity.ok(Map.of(
                    "success", true,
                    "ready", false
            ));
        }
        return ResponseEntity.ok(Map.of(
                "success", true,
                "ready", true,
                "token", token.get()
        ));
    }
}
