package com.uiusimulator.player.controller;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.when;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.jwt;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.uiusimulator.auth.security.ClerkJwtAuthenticationFilter;
import com.uiusimulator.common.exception.GlobalExceptionHandler;
import com.uiusimulator.config.ClerkProperties;
import com.uiusimulator.config.SecurityConfig;
import com.uiusimulator.player.dto.FacultyRoutineResponse;
import com.uiusimulator.player.dto.FacultyRoutineResponse.FacultyProfile;
import com.uiusimulator.player.dto.FacultyRoutineResponse.FacultyRoutineItem;
import com.uiusimulator.player.exception.PlayerSaveNotFoundException;
import com.uiusimulator.player.service.FacultyRoutineService;
import java.util.List;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.context.properties.EnableConfigurationProperties;
import org.springframework.boot.test.autoconfigure.web.servlet.WebMvcTest;
import org.springframework.context.annotation.Import;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.security.oauth2.jwt.JwtDecoder;
import org.springframework.test.context.ActiveProfiles;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;

@WebMvcTest(FacultyRoutineController.class)
@ActiveProfiles("test")
@EnableConfigurationProperties(ClerkProperties.class)
@Import({SecurityConfig.class, ClerkJwtAuthenticationFilter.class, GlobalExceptionHandler.class})
class FacultyRoutineControllerTest {

    @Autowired
    private MockMvc mockMvc;

    @MockitoBean
    private JwtDecoder jwtDecoder;

    @MockitoBean
    private FacultyRoutineService facultyRoutineService;

    @Test
    void getFacultyRoutine_faculty_returnsProfileAndExistingClassrooms() throws Exception {
        when(facultyRoutineService.getFacultyRoutine(any(Jwt.class)))
                .thenReturn(new FacultyRoutineResponse(
                        new FacultyProfile("Lail", "F-001", "CSE", "Lecturer", "335"),
                        List.of(
                                new FacultyRoutineItem("ICS", "Introduction to Computer Science", "427", 4),
                                new FacultyRoutineItem("DM", "Discrete Mathematics", "423", 4)
                        )
                ));

        mockMvc.perform(get("/api/players/me/faculty-routine")
                        .with(jwt().jwt(j -> j.subject("user_me"))))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.profile.name").value("Lail"))
                .andExpect(jsonPath("$.profile.facultyId").value("F-001"))
                .andExpect(jsonPath("$.profile.department").value("CSE"))
                .andExpect(jsonPath("$.profile.designation").value("Lecturer"))
                .andExpect(jsonPath("$.profile.office").value("335"))
                .andExpect(jsonPath("$.routine[0].courseId").value("ICS"))
                .andExpect(jsonPath("$.routine[0].courseName").value("Introduction to Computer Science"))
                .andExpect(jsonPath("$.routine[0].classroomNumber").value("427"))
                .andExpect(jsonPath("$.routine[0].floor").value(4))
                .andExpect(jsonPath("$.routine[1].courseId").value("DM"))
                .andExpect(jsonPath("$.routine[1].courseName").value("Discrete Mathematics"))
                .andExpect(jsonPath("$.routine[1].classroomNumber").value("423"))
                .andExpect(jsonPath("$.routine[1].floor").value(4));
    }

    @Test
    void getFacultyRoutine_student_returns400() throws Exception {
        when(facultyRoutineService.getFacultyRoutine(any(Jwt.class)))
                .thenThrow(new IllegalArgumentException("Faculty routine is available only to faculty."));

        mockMvc.perform(get("/api/players/me/faculty-routine")
                        .with(jwt().jwt(j -> j.subject("user_me"))))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.success").value(false))
                .andExpect(jsonPath("$.message").value("Faculty routine is available only to faculty."));
    }

    @Test
    void getFacultyRoutine_noSave_returns404() throws Exception {
        when(facultyRoutineService.getFacultyRoutine(any(Jwt.class)))
                .thenThrow(new PlayerSaveNotFoundException());

        mockMvc.perform(get("/api/players/me/faculty-routine")
                        .with(jwt().jwt(j -> j.subject("user_me"))))
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.success").value(false))
                .andExpect(jsonPath("$.message").value(
                        "No active university journey found. Complete admission first."
                ));
    }

    @Test
    void getFacultyRoutine_unauthenticated_returns401() throws Exception {
        mockMvc.perform(get("/api/players/me/faculty-routine"))
                .andExpect(status().isUnauthorized());
    }
}
