package com.uiusimulator.config;

import static org.assertj.core.api.Assertions.assertThat;

import org.junit.jupiter.api.Test;

class DevDotEnvLoaderTest {

    @Test
    void applyLine_ignoresCommentsAndDoesNotOverrideExistingProperties() {
        String uniqueKey = "UIU_TEST_DOTENV_" + System.nanoTime();
        assertThat(DevDotEnvLoader.applyLine("# CLERK_GAME_TOKEN_TTL_SECONDS=60")).isFalse();
        assertThat(DevDotEnvLoader.applyLine("")).isFalse();

        assertThat(DevDotEnvLoader.applyLine(uniqueKey + "=3600")).isTrue();
        assertThat(System.getProperty(uniqueKey)).isEqualTo("3600");

        assertThat(DevDotEnvLoader.applyLine(uniqueKey + "=60")).isFalse();
        assertThat(System.getProperty(uniqueKey)).isEqualTo("3600");

        System.clearProperty(uniqueKey);
    }

    @Test
    void applyLine_supportsExportAndQuotes() {
        String uniqueKey = "UIU_TEST_DOTENV_Q_" + System.nanoTime();
        assertThat(DevDotEnvLoader.applyLine("export " + uniqueKey + "=\"3600\"")).isTrue();
        assertThat(System.getProperty(uniqueKey)).isEqualTo("3600");
        System.clearProperty(uniqueKey);
    }
}
