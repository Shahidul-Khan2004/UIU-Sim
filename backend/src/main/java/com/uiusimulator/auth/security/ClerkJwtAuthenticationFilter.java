package com.uiusimulator.auth.security;

import com.uiusimulator.config.ClerkProperties;
import jakarta.servlet.FilterChain;
import jakarta.servlet.ServletException;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import java.io.IOException;
import java.util.List;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.http.HttpHeaders;
import org.springframework.security.core.context.SecurityContextHolder;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.security.oauth2.jwt.JwtDecoder;
import org.springframework.security.oauth2.jwt.JwtException;
import org.springframework.security.oauth2.server.resource.authentication.JwtAuthenticationToken;
import org.springframework.stereotype.Component;
import org.springframework.web.filter.OncePerRequestFilter;

/**
 * Reads the Bearer JWT, validates it via Clerk JWKS ({@link JwtDecoder}),
 * checks authorized party (azp) when present, and sets the security context.
 */
@Component
public class ClerkJwtAuthenticationFilter extends OncePerRequestFilter {

    private static final Logger log = LoggerFactory.getLogger(ClerkJwtAuthenticationFilter.class);

    private final JwtDecoder jwtDecoder;
    private final ClerkProperties clerkProperties;

    public ClerkJwtAuthenticationFilter(JwtDecoder jwtDecoder, ClerkProperties clerkProperties) {
        this.jwtDecoder = jwtDecoder;
        this.clerkProperties = clerkProperties;
    }

    @Override
    protected void doFilterInternal(
            HttpServletRequest request,
            HttpServletResponse response,
            FilterChain filterChain
    ) throws ServletException, IOException {
        String header = request.getHeader(HttpHeaders.AUTHORIZATION);
        if (header == null || !header.startsWith("Bearer ")) {
            filterChain.doFilter(request, response);
            return;
        }

        String token = header.substring(7).trim();
        if (token.isEmpty()) {
            filterChain.doFilter(request, response);
            return;
        }

        try {
            Jwt jwt = jwtDecoder.decode(token);
            validateAuthorizedParty(jwt);
            JwtAuthenticationToken authentication = new JwtAuthenticationToken(jwt, List.of());
            authentication.setAuthenticated(true);
            SecurityContextHolder.getContext().setAuthentication(authentication);
            log.info("Clerk user validated clerkUserId={}", jwt.getSubject());
        } catch (JwtException | IllegalArgumentException ex) {
            log.error("Invalid JWT: {}", ex.getMessage());
            SecurityContextHolder.clearContext();
            response.setStatus(HttpServletResponse.SC_UNAUTHORIZED);
            response.setContentType("application/json");
            response.getWriter().write(
                    "{\"success\":false,\"message\":\"Invalid or expired authentication token\",\"timestamp\":\""
                            + java.time.Instant.now()
                            + "\",\"path\":\""
                            + request.getRequestURI()
                            + "\"}"
            );
            return;
        }

        filterChain.doFilter(request, response);
    }

    private void validateAuthorizedParty(Jwt jwt) {
        List<String> allowed = clerkProperties.authorizedPartyList();
        if (allowed.isEmpty()) {
            return;
        }
        String azp = jwt.getClaimAsString("azp");
        if (azp == null || azp.isBlank()) {
            return;
        }
        if (!allowed.contains(azp)) {
            throw new IllegalArgumentException("JWT azp claim is not an authorized party");
        }
    }
}
