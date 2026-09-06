package com.uiusimulator.auth.security;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

import com.uiusimulator.config.ClerkProperties;
import jakarta.servlet.FilterChain;
import java.time.Instant;
import java.util.List;
import java.util.Map;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;
import org.springframework.mock.web.MockHttpServletRequest;
import org.springframework.mock.web.MockHttpServletResponse;
import org.springframework.security.core.context.SecurityContextHolder;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.security.oauth2.jwt.JwtDecoder;
import org.springframework.security.oauth2.jwt.JwtException;

@ExtendWith(MockitoExtension.class)
class ClerkJwtAuthenticationFilterTest {

    @Mock
    private JwtDecoder jwtDecoder;

    @Mock
    private ClerkProperties clerkProperties;

    @Mock
    private FilterChain filterChain;

    private ClerkJwtAuthenticationFilter filter;

    @BeforeEach
    void setUp() {
        SecurityContextHolder.clearContext();
        filter = new ClerkJwtAuthenticationFilter(jwtDecoder, clerkProperties);
    }

    @Test
    void invalidJwt_returnsUnauthorizedAndDoesNotContinue() throws Exception {
        when(jwtDecoder.decode(anyString())).thenThrow(new JwtException("bad token"));

        MockHttpServletRequest request = new MockHttpServletRequest("POST", "/api/auth/login");
        request.addHeader("Authorization", "Bearer not-a-real-token");
        MockHttpServletResponse response = new MockHttpServletResponse();

        filter.doFilter(request, response, filterChain);

        assertThat(response.getStatus()).isEqualTo(401);
        assertThat(response.getContentAsString()).contains("Authentication token is invalid.");
        verify(filterChain, never()).doFilter(request, response);
        assertThat(SecurityContextHolder.getContext().getAuthentication()).isNull();
    }

    @Test
    void validJwt_setsAuthenticationAndContinues() throws Exception {
        when(clerkProperties.authorizedPartyList()).thenReturn(List.of("http://localhost:8080"));
        Jwt jwt = new Jwt(
                "token",
                Instant.now(),
                Instant.now().plusSeconds(60),
                Map.of("alg", "RS256"),
                Map.of("sub", "user_ok", "azp", "http://localhost:8080")
        );
        when(jwtDecoder.decode("good-token")).thenReturn(jwt);

        MockHttpServletRequest request = new MockHttpServletRequest("POST", "/api/auth/login");
        request.addHeader("Authorization", "Bearer good-token");
        MockHttpServletResponse response = new MockHttpServletResponse();

        filter.doFilter(request, response, filterChain);

        verify(filterChain).doFilter(request, response);
        assertThat(SecurityContextHolder.getContext().getAuthentication()).isNotNull();
        assertThat(SecurityContextHolder.getContext().getAuthentication().getName()).isEqualTo("user_ok");
    }

    @Test
    void wrongIssuer_returnsUnauthorized() throws Exception {
        when(jwtDecoder.decode(anyString()))
                .thenThrow(new JwtException("The iss claim is not valid"));

        MockHttpServletRequest request = new MockHttpServletRequest("GET", "/api/players/me");
        request.addHeader("Authorization", "Bearer wrong-issuer-token");
        MockHttpServletResponse response = new MockHttpServletResponse();

        filter.doFilter(request, response, filterChain);

        assertThat(response.getStatus()).isEqualTo(401);
        assertThat(response.getContentAsString()).contains("Authentication token is invalid.");
        verify(filterChain, never()).doFilter(request, response);
        assertThat(SecurityContextHolder.getContext().getAuthentication()).isNull();
    }

    @Test
    void wrongAzp_returnsUnauthorized() throws Exception {
        when(clerkProperties.authorizedPartyList()).thenReturn(List.of("http://localhost:8080"));
        Jwt jwt = new Jwt(
                "token",
                Instant.now(),
                Instant.now().plusSeconds(60),
                Map.of("alg", "RS256"),
                Map.of("sub", "user_bad_azp", "azp", "https://evil-site.example.com")
        );
        when(jwtDecoder.decode("bad-azp-token")).thenReturn(jwt);

        MockHttpServletRequest request = new MockHttpServletRequest("GET", "/api/players/me");
        request.addHeader("Authorization", "Bearer bad-azp-token");
        MockHttpServletResponse response = new MockHttpServletResponse();

        filter.doFilter(request, response, filterChain);

        assertThat(response.getStatus()).isEqualTo(401);
        assertThat(response.getContentAsString()).contains("Authentication token is invalid.");
        verify(filterChain, never()).doFilter(request, response);
        assertThat(SecurityContextHolder.getContext().getAuthentication()).isNull();
    }

    @Test
    void missingAuthorizationHeader_leavesAuthenticationUnsetAndContinues() throws Exception {
        MockHttpServletRequest request = new MockHttpServletRequest("GET", "/api/players/me");
        MockHttpServletResponse response = new MockHttpServletResponse();

        filter.doFilter(request, response, filterChain);

        verify(filterChain).doFilter(request, response);
        assertThat(SecurityContextHolder.getContext().getAuthentication()).isNull();
        verify(jwtDecoder, never()).decode(anyString());
    }

    @Test
    void malformedToken_returnsUnauthorized() throws Exception {
        when(jwtDecoder.decode(anyString()))
                .thenThrow(new JwtException("An error occurred while attempting to decode the Jwt"));

        MockHttpServletRequest request = new MockHttpServletRequest("GET", "/api/players/me");
        request.addHeader("Authorization", "Bearer not.a.valid.jwt");
        MockHttpServletResponse response = new MockHttpServletResponse();

        filter.doFilter(request, response, filterChain);

        assertThat(response.getStatus()).isEqualTo(401);
        assertThat(response.getContentAsString()).contains("Authentication token is invalid.");
        verify(filterChain, never()).doFilter(request, response);
        assertThat(SecurityContextHolder.getContext().getAuthentication()).isNull();
    }
}

