package com.uiusimulator.config;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Component;

@Component
public class ClerkAuthStartupLogger {

    private static final Logger log = LoggerFactory.getLogger(ClerkAuthStartupLogger.class);

    public ClerkAuthStartupLogger(ClerkProperties clerkProperties) {
        log.info("[Auth] Unity game JWT TTL configured: {} seconds",
                clerkProperties.effectiveGameTokenTtlSeconds());
    }
}
