package com.uiusimulator.player.controller;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.when;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.jwt;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.uiusimulator.auth.security.ClerkJwtAuthenticationFilter;
import com.uiusimulator.common.exception.GlobalExceptionHandler;
import com.uiusimulator.config.ClerkProperties;
import com.uiusimulator.config.SecurityConfig;
import com.uiusimulator.player.dto.FacultyProgressResponse;
import com.uiusimulator.player.service.FacultyProgressService;
import com.uiusimulator.player.service.FacultyTeachService;
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

@WebMvcTest(FacultyProgressController.class)
@ActiveProfiles("test")
@EnableConfigurationProperties(ClerkProperties.class)
@Import({SecurityConfig.class, ClerkJwtAuthenticationFilter.class, GlobalExceptionHandler.class})
class FacultyProgressControllerTest {

    @Autowired
    private MockMvc mockMvc;

    @MockitoBean
    private JwtDecoder jwtDecoder;

    @MockitoBean
    private FacultyProgressService facultyProgressService;

    @MockitoBean
    private FacultyTeachService facultyTeachService;

    @Test
    void getFacultyProgress_faculty_returnsReputation() throws Exception {
        when(facultyProgressService.getProgress(any(Jwt.class)))
                .thenReturn(FacultyProgressResponse.stub(50, false, false, false, false, false, false, false));

        mockMvc.perform(get("/api/players/me/faculty-progress")
                        .with(jwt().jwt(j -> j.subject("user_me"))))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.reputation").value(50));
    }

    @Test
    void getFacultyProgress_student_returns400() throws Exception {
        when(facultyProgressService.getProgress(any(Jwt.class)))
                .thenThrow(new IllegalArgumentException(FacultyProgressService.FACULTY_ONLY_MESSAGE));

        mockMvc.perform(get("/api/players/me/faculty-progress")
                        .with(jwt().jwt(j -> j.subject("user_me"))))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.success").value(false))
                .andExpect(jsonPath("$.message").value(FacultyProgressService.FACULTY_ONLY_MESSAGE));
    }

    @Test
    void computer_faculty_returnsUpdatedReputation() throws Exception {
        when(facultyProgressService.useComputer(any(Jwt.class)))
                .thenReturn(FacultyProgressResponse.stub(55, true, true, false, false, false, false, false));

        mockMvc.perform(post("/api/players/me/faculty-progress/computer")
                        .with(jwt().jwt(j -> j.subject("user_me"))))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.reputation").value(55))
                .andExpect(jsonPath("$.computerUsed").value(true));
    }

    @Test
    void scan_faculty_returnsUpdatedReputation() throws Exception {
        when(facultyTeachService.scanClassroom(any(Jwt.class), eq("ICS")))
                .thenReturn(FacultyProgressResponse.stub(60, false, false, true, false, false, false, false));

        mockMvc.perform(post("/api/players/me/faculty-teach/scan")
                        .with(jwt().jwt(j -> j.subject("user_me")))
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"courseId\":\"ICS\"}"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.reputation").value(60))
                .andExpect(jsonPath("$.classroomScanned").value(true));
    }

    @Test
    void complete_faculty_returnsUpdatedReputation() throws Exception {
        when(facultyTeachService.completeLecture(any(Jwt.class)))
                .thenReturn(FacultyProgressResponse.stub(65, false, false, true, true, false, false, false));

        mockMvc.perform(post("/api/players/me/faculty-teach/complete")
                        .with(jwt().jwt(j -> j.subject("user_me"))))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.reputation").value(65))
                .andExpect(jsonPath("$.lectureCompleted").value(true));
    }

    @Test
    void leave_faculty_returnsUpdatedReputation() throws Exception {
        when(facultyTeachService.leaveLecture(any(Jwt.class)))
                .thenReturn(FacultyProgressResponse.stub(40, false, false, true, false, true, false, false));

        mockMvc.perform(post("/api/players/me/faculty-teach/leave")
                        .with(jwt().jwt(j -> j.subject("user_me"))))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.reputation").value(40))
                .andExpect(jsonPath("$.lectureLeft").value(true));
    }

    @Test
    void facultyProgress_unauthenticated_returns401() throws Exception {
        mockMvc.perform(get("/api/players/me/faculty-progress"))
                .andExpect(status().isUnauthorized());
        mockMvc.perform(post("/api/players/me/faculty-progress/computer"))
                .andExpect(status().isUnauthorized());
        mockMvc.perform(post("/api/players/me/faculty-teach/scan")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"courseId\":\"ICS\"}"))
                .andExpect(status().isUnauthorized());
        mockMvc.perform(post("/api/players/me/faculty-teach/complete"))
                .andExpect(status().isUnauthorized());
        mockMvc.perform(post("/api/players/me/faculty-teach/leave"))
                .andExpect(status().isUnauthorized());
    }
}
