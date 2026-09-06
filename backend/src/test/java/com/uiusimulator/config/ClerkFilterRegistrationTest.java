package com.uiusimulator.config;

import static org.assertj.core.api.Assertions.assertThat;

import com.uiusimulator.auth.security.ClerkJwtAuthenticationFilter;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.context.properties.EnableConfigurationProperties;
import org.springframework.boot.test.autoconfigure.web.servlet.WebMvcTest;
import org.springframework.boot.web.servlet.FilterRegistrationBean;
import org.springframework.context.ApplicationContext;
import org.springframework.context.annotation.Import;
import org.springframework.security.oauth2.jwt.JwtDecoder;
import org.springframework.test.context.ActiveProfiles;
import org.springframework.test.context.bean.override.mockito.MockitoBean;

/**
 * Verifies that {@link ClerkJwtAuthenticationFilter} is NOT auto-registered as a
 * servlet-container filter by Spring Boot. If this test fails, the filter would execute
 * outside the Spring Security chain, causing the {@code SecurityContext} to be empty
 * inside Spring Security and producing 401 on all authenticated endpoints.
 *
 * Regression test for the duplicate filter registration bug.
 */
@WebMvcTest
@ActiveProfiles("test")
@EnableConfigurationProperties(ClerkProperties.class)
@Import({SecurityConfig.class, ClerkJwtAuthenticationFilter.class})
class ClerkFilterRegistrationTest {

    @Autowired
    private ApplicationContext applicationContext;

    @MockitoBean
    private JwtDecoder jwtDecoder;

    @MockitoBean
    private com.uiusimulator.player.service.PlayerService playerService;

    @MockitoBean
    private com.uiusimulator.player.service.PlayerStatsService playerStatsService;

    @MockitoBean
    private com.uiusimulator.auth.service.AuthService authService;

    @MockitoBean
    private com.uiusimulator.auth.service.DevAuthBridgeService devAuthBridgeService;

    @Test
    void clerkFilterRegistrationBean_isDisabled() {
        @SuppressWarnings("unchecked")
        FilterRegistrationBean<ClerkJwtAuthenticationFilter> registration =
                applicationContext.getBean("disableClerkFilterAutoRegistration",
                        FilterRegistrationBean.class);

        assertThat(registration).isNotNull();
        assertThat(registration.isEnabled()).isFalse();
    }

    @Test
    void clerkFilterBean_existsInContext() {
        ClerkJwtAuthenticationFilter filter =
                applicationContext.getBean(ClerkJwtAuthenticationFilter.class);
        assertThat(filter).isNotNull();
    }
}
