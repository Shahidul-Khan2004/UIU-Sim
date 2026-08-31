package com.uiusimulator.auth.service;

import java.time.Instant;
import java.util.Iterator;
import java.util.Map;
import java.util.Optional;
import java.util.concurrent.ConcurrentHashMap;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;

/**
 * In-memory bridge for Unity Editor / Linux development where custom URI schemes
 * are not registered with the OS. Tokens expire quickly and are one-time consume.
 */
@Service
public class DevAuthBridgeService {

    private static final Logger log = LoggerFactory.getLogger(DevAuthBridgeService.class);
    private static final long TTL_SECONDS = 300;

    private final ConcurrentHashMap<String, Entry> sessions = new ConcurrentHashMap<>();

    public void putToken(String sessionId, String token) {
        purgeExpired();
        if (sessionId == null || sessionId.isBlank() || token == null || token.isBlank()) {
            throw new IllegalArgumentException("sessionId and token are required");
        }
        sessions.put(sessionId, new Entry(token, Instant.now().plusSeconds(TTL_SECONDS)));
        log.info("Dev auth bridge token stored for sessionId={}", sessionId);
    }

    public Optional<String> consumeToken(String sessionId) {
        purgeExpired();
        if (sessionId == null || sessionId.isBlank()) {
            return Optional.empty();
        }
        Entry entry = sessions.remove(sessionId);
        if (entry == null || entry.expiresAt().isBefore(Instant.now())) {
            return Optional.empty();
        }
        log.info("Dev auth bridge token consumed for sessionId={}", sessionId);
        return Optional.of(entry.token());
    }

    public boolean isPending(String sessionId) {
        purgeExpired();
        Entry entry = sessions.get(sessionId);
        return entry != null && entry.expiresAt().isAfter(Instant.now());
    }

    private void purgeExpired() {
        Instant now = Instant.now();
        Iterator<Map.Entry<String, Entry>> iterator = sessions.entrySet().iterator();
        while (iterator.hasNext()) {
            Map.Entry<String, Entry> item = iterator.next();
            if (item.getValue().expiresAt().isBefore(now)) {
                iterator.remove();
            }
        }
    }

    private record Entry(String token, Instant expiresAt) {
    }
}
