package com.uiusimulator.config;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.List;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

/**
 * Development helper: loads {@code backend/.env} into JVM system properties when the
 * Spring Boot process was started without {@code set -a && source .env}.
 * Existing environment variables and system properties are never overridden.
 * Values are not logged.
 */
public final class DevDotEnvLoader {

    private static final Logger log = LoggerFactory.getLogger(DevDotEnvLoader.class);

    private DevDotEnvLoader() {
    }

    public static void loadIfPresent() {
        Path envFile = resolveEnvFile();
        if (envFile == null) {
            return;
        }

        try {
            List<String> lines = Files.readAllLines(envFile);
            int applied = 0;
            for (String raw : lines) {
                if (applyLine(raw)) {
                    applied++;
                }
            }
            if (applied > 0) {
                log.info("[Auth] Loaded {} unset development environment keys from {}", applied, envFile);
            }
        } catch (IOException ex) {
            log.warn("[Auth] Could not read development .env file {}: {}", envFile, ex.getMessage());
        }
    }

    static Path resolveEnvFile() {
        Path cwd = Path.of(".env");
        if (Files.isRegularFile(cwd)) {
            return cwd.toAbsolutePath().normalize();
        }
        Path nested = Path.of("backend", ".env");
        if (Files.isRegularFile(nested)) {
            return nested.toAbsolutePath().normalize();
        }
        return null;
    }

    static boolean applyLine(String raw) {
        if (raw == null) {
            return false;
        }
        String line = raw.trim();
        if (line.isEmpty() || line.startsWith("#")) {
            return false;
        }
        if (line.startsWith("export ")) {
            line = line.substring("export ".length()).trim();
        }
        int eq = line.indexOf('=');
        if (eq <= 0) {
            return false;
        }
        String key = line.substring(0, eq).trim();
        String value = unquote(line.substring(eq + 1).trim());
        if (key.isEmpty() || System.getenv(key) != null || System.getProperty(key) != null) {
            return false;
        }
        System.setProperty(key, value);
        return true;
    }

    private static String unquote(String value) {
        if (value.length() >= 2) {
            char first = value.charAt(0);
            char last = value.charAt(value.length() - 1);
            if ((first == '"' && last == '"') || (first == '\'' && last == '\'')) {
                return value.substring(1, value.length() - 1);
            }
        }
        return value;
    }
}
