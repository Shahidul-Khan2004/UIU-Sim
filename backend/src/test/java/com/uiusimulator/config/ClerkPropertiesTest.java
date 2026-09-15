package com.uiusimulator.config;

import static org.assertj.core.api.Assertions.assertThat;

import org.junit.jupiter.api.Test;

class ClerkPropertiesTest {

    @Test
    void effectiveGameTokenTtlSeconds_usesConfiguredPositiveValue() {
        ClerkProperties properties = new ClerkProperties(
                "pk_test", "sk_test", "https://issuer", "https://issuer/jwks", "http://localhost:8080", 3600);
        assertThat(properties.effectiveGameTokenTtlSeconds()).isEqualTo(3600);
    }

    @Test
    void effectiveGameTokenTtlSeconds_fallsBackToSixtyWhenMissingOrInvalid() {
        ClerkProperties missing = new ClerkProperties(null, null, null, null, null, null);
        ClerkProperties zero = new ClerkProperties(null, null, null, null, null, 0);
        ClerkProperties negative = new ClerkProperties(null, null, null, null, null, -1);

        assertThat(missing.effectiveGameTokenTtlSeconds()).isEqualTo(60);
        assertThat(zero.effectiveGameTokenTtlSeconds()).isEqualTo(60);
        assertThat(negative.effectiveGameTokenTtlSeconds()).isEqualTo(60);
    }
}
