package com.uiusimulator.config;

import com.uiusimulator.auth.security.ClerkJwtAuthenticationFilter;
import jakarta.servlet.http.HttpServletResponse;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.http.HttpMethod;
import org.springframework.security.config.Customizer;
import org.springframework.security.config.annotation.web.builders.HttpSecurity;
import org.springframework.security.config.annotation.web.configuration.EnableWebSecurity;
import org.springframework.security.config.annotation.web.configurers.AbstractHttpConfigurer;
import org.springframework.security.config.http.SessionCreationPolicy;
import org.springframework.security.oauth2.jwt.JwtDecoder;
import org.springframework.security.oauth2.jwt.JwtValidators;
import org.springframework.security.oauth2.jwt.NimbusJwtDecoder;
import org.springframework.security.web.SecurityFilterChain;
import org.springframework.security.web.authentication.UsernamePasswordAuthenticationFilter;
import org.springframework.boot.web.servlet.FilterRegistrationBean;

@Configuration
@EnableWebSecurity
public class SecurityConfig {

    @Bean
    SecurityFilterChain securityFilterChain(
            HttpSecurity http,
            ClerkJwtAuthenticationFilter clerkJwtAuthenticationFilter
    ) throws Exception {
        http
                .csrf(AbstractHttpConfigurer::disable)
                .cors(Customizer.withDefaults())
                .sessionManagement(session -> session.sessionCreationPolicy(SessionCreationPolicy.STATELESS))
                .exceptionHandling(ex -> ex.authenticationEntryPoint((request, response, authException) -> {
                    response.setStatus(HttpServletResponse.SC_UNAUTHORIZED);
                    response.setContentType("application/json");
                    response.getWriter().write(
                            "{\"success\":false,\"message\":\"Authentication failed\",\"timestamp\":\""
                                    + java.time.Instant.now()
                                    + "\",\"path\":\""
                                    + request.getRequestURI()
                                    + "\"}"
                    );
                }))
                .authorizeHttpRequests(auth -> auth
                        .requestMatchers("/auth/**").permitAll()
                        .requestMatchers("/actuator/health", "/actuator/health/**").permitAll()
                        .requestMatchers("/error").permitAll()
                        .requestMatchers(HttpMethod.OPTIONS, "/**").permitAll()
                        .requestMatchers("/api/**").authenticated()
                        .anyRequest().permitAll()
                )
                .addFilterBefore(clerkJwtAuthenticationFilter, UsernamePasswordAuthenticationFilter.class);

        return http.build();
    }

    @Bean
    JwtDecoder jwtDecoder(ClerkProperties clerkProperties) {
        NimbusJwtDecoder decoder = NimbusJwtDecoder.withJwkSetUri(clerkProperties.jwksUrl()).build();
        decoder.setJwtValidator(JwtValidators.createDefaultWithIssuer(clerkProperties.issuer()));
        return decoder;
    }

    /**
     * Prevents Spring Boot from auto-registering {@link ClerkJwtAuthenticationFilter} as a
     * servlet-container filter. The filter must run exclusively within the Spring Security
     * chain (via {@code addFilterBefore}) so that {@code SecurityContextHolderFilter} properly
     * manages the {@code SecurityContext} lifecycle.
     */
    @Bean
    FilterRegistrationBean<ClerkJwtAuthenticationFilter> disableClerkFilterAutoRegistration(
            ClerkJwtAuthenticationFilter filter) {
        FilterRegistrationBean<ClerkJwtAuthenticationFilter> registration =
                new FilterRegistrationBean<>(filter);
        registration.setEnabled(false);
        return registration;
    }
}
