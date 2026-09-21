package com.uiusimulator.player.controller;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.when;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.jwt;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.uiusimulator.auth.security.ClerkJwtAuthenticationFilter;
import com.uiusimulator.common.exception.GlobalExceptionHandler;
import com.uiusimulator.config.ClerkProperties;
import com.uiusimulator.config.SecurityConfig;
import com.uiusimulator.player.dto.PlayerSaveCreateRequest;
import com.uiusimulator.player.dto.PlayerSaveResponse;
import com.uiusimulator.player.dto.PlayerSaveStatusResponse;
import com.uiusimulator.player.entity.PlayerRole;
import com.uiusimulator.player.exception.PlayerSaveAlreadyExistsException;
import com.uiusimulator.player.service.PlayerSaveService;
import java.util.UUID;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.context.properties.EnableConfigurationProperties;
import org.springframework.boot.test.autoconfigure.web.servlet.WebMvcTest;
import org.springframework.context.annotation.Import;
import org.springframework.http.MediaType;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.security.oauth2.jwt.JwtDecoder;
import org.springframework.test.context.ActiveProfiles;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;

@WebMvcTest(PlayerSaveController.class)
@ActiveProfiles("test")
@EnableConfigurationProperties(ClerkProperties.class)
@Import({SecurityConfig.class, ClerkJwtAuthenticationFilter.class, GlobalExceptionHandler.class})
class PlayerSaveControllerTest {

    @Autowired
    private MockMvc mockMvc;

    @Autowired
    private ObjectMapper objectMapper;

    @MockitoBean
    private JwtDecoder jwtDecoder;

    @MockitoBean
    private PlayerSaveService playerSaveService;

    @Test
    void getSave_noSave_returnsHasSaveFalse() throws Exception {
        when(playerSaveService.getSaveStatus(any(Jwt.class)))
                .thenReturn(PlayerSaveStatusResponse.none());

        mockMvc.perform(get("/api/players/me/save")
                        .with(jwt().jwt(j -> j.subject("user_me"))))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.hasSave").value(false))
                .andExpect(jsonPath("$.save").doesNotExist());
    }

    @Test
    void getSave_withSave_returnsJourney() throws Exception {
        when(playerSaveService.getSaveStatus(any(Jwt.class)))
                .thenReturn(PlayerSaveStatusResponse.of(new PlayerSaveResponse(
                        "STUDENT", "CSE", "22112345", 1, 1, true
                )));

        mockMvc.perform(get("/api/players/me/save")
                        .with(jwt().jwt(j -> j.subject("user_me"))))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.hasSave").value(true))
                .andExpect(jsonPath("$.save.role").value("STUDENT"))
                .andExpect(jsonPath("$.save.department").value("CSE"))
                .andExpect(jsonPath("$.save.universityId").value("22112345"))
                .andExpect(jsonPath("$.save.semester").value(1))
                .andExpect(jsonPath("$.save.currentDay").value(1))
                .andExpect(jsonPath("$.save.admissionCompleted").value(true));
    }

    @Test
    void createSave_validRequest_returns201() throws Exception {
        UUID departmentId = UUID.randomUUID();
        when(playerSaveService.createSave(any(Jwt.class), any(PlayerSaveCreateRequest.class)))
                .thenReturn(PlayerSaveStatusResponse.of(new PlayerSaveResponse(
                        "STUDENT", "CSE", "22112345", 1, 1, true
                )));

        PlayerSaveCreateRequest request = new PlayerSaveCreateRequest(
                PlayerRole.STUDENT,
                departmentId,
                "22112345"
        );

        mockMvc.perform(post("/api/players/me/save")
                        .with(jwt().jwt(j -> j.subject("user_me")))
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(objectMapper.writeValueAsString(request)))
                .andExpect(status().isCreated())
                .andExpect(jsonPath("$.hasSave").value(true))
                .andExpect(jsonPath("$.save.department").value("CSE"));
    }

    @Test
    void createSave_duplicate_returns409() throws Exception {
        when(playerSaveService.createSave(any(Jwt.class), any(PlayerSaveCreateRequest.class)))
                .thenThrow(new PlayerSaveAlreadyExistsException());

        PlayerSaveCreateRequest request = new PlayerSaveCreateRequest(
                PlayerRole.STUDENT,
                UUID.randomUUID(),
                "1"
        );

        mockMvc.perform(post("/api/players/me/save")
                        .with(jwt().jwt(j -> j.subject("user_me")))
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(objectMapper.writeValueAsString(request)))
                .andExpect(status().isConflict())
                .andExpect(jsonPath("$.success").value(false))
                .andExpect(jsonPath("$.message").value(
                        "An active university journey already exists for this player"
                ));
    }

    @Test
    void createSave_missingRole_returns400() throws Exception {
        mockMvc.perform(post("/api/players/me/save")
                        .with(jwt().jwt(j -> j.subject("user_me")))
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"departmentId\":\"" + UUID.randomUUID() + "\"}"))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.success").value(false));
    }

    @Test
    void deleteSave_authenticated_returnsHasSaveFalse() throws Exception {
        when(playerSaveService.deleteSave(any(Jwt.class)))
                .thenReturn(PlayerSaveStatusResponse.none());

        mockMvc.perform(delete("/api/players/me/save")
                        .with(jwt().jwt(j -> j.subject("user_me"))))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.hasSave").value(false));
    }

    @Test
    void getSave_unauthenticated_returns401() throws Exception {
        mockMvc.perform(get("/api/players/me/save"))
                .andExpect(status().isUnauthorized());
    }
}
