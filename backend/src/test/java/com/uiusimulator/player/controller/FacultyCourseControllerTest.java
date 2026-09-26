package com.uiusimulator.player.controller;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.doThrow;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.jwt;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.uiusimulator.auth.security.ClerkJwtAuthenticationFilter;
import com.uiusimulator.common.exception.GlobalExceptionHandler;
import com.uiusimulator.config.ClerkProperties;
import com.uiusimulator.config.SecurityConfig;
import com.uiusimulator.player.dto.CourseMaterialOpenResponse;
import com.uiusimulator.player.dto.CourseMaterialResponse;
import com.uiusimulator.player.dto.FacultyCoursesResponse;
import com.uiusimulator.player.dto.FacultyCoursesResponse.FacultyCourseItem;
import com.uiusimulator.player.dto.FacultyStudentItem;
import com.uiusimulator.player.service.FacultyCourseService;
import java.time.Instant;
import java.util.List;
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

@WebMvcTest(FacultyCourseController.class)
@ActiveProfiles("test")
@EnableConfigurationProperties(ClerkProperties.class)
@Import({SecurityConfig.class, ClerkJwtAuthenticationFilter.class, GlobalExceptionHandler.class})
class FacultyCourseControllerTest {

    @Autowired
    private MockMvc mockMvc;

    @MockitoBean
    private JwtDecoder jwtDecoder;

    @MockitoBean
    private FacultyCourseService facultyCourseService;

    @Test
    void getFacultyCourses_faculty_returnsAssignedCourses() throws Exception {
        when(facultyCourseService.getFacultyCourses(any(Jwt.class)))
                .thenReturn(new FacultyCoursesResponse(
                        "F-001",
                        List.of(
                                new FacultyCourseItem("ICS", "Introduction to Computer Science", "427", 4),
                                new FacultyCourseItem("DM", "Discrete Mathematics", "423", 4)
                        )
                ));

        mockMvc.perform(get("/api/players/me/faculty-courses")
                        .with(jwt().jwt(j -> j.subject("user_me"))))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.facultyId").value("F-001"))
                .andExpect(jsonPath("$.courses[0].courseCode").value("ICS"))
                .andExpect(jsonPath("$.courses[0].classroom").value("427"))
                .andExpect(jsonPath("$.courses[1].courseCode").value("DM"))
                .andExpect(jsonPath("$.courses[1].classroom").value("423"));
    }

    @Test
    void getFacultyCourses_student_returns400() throws Exception {
        when(facultyCourseService.getFacultyCourses(any(Jwt.class)))
                .thenThrow(new IllegalArgumentException(FacultyCourseService.FACULTY_ONLY_MESSAGE));

        mockMvc.perform(get("/api/players/me/faculty-courses")
                        .with(jwt().jwt(j -> j.subject("user_me"))))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.success").value(false))
                .andExpect(jsonPath("$.message").value(FacultyCourseService.FACULTY_ONLY_MESSAGE));
    }

    @Test
    void getCourseStudents_returnsEnrolledStudents() throws Exception {
        when(facultyCourseService.getCourseStudents(any(Jwt.class), eq("ICS")))
                .thenReturn(List.of(new FacultyStudentItem("Rahim", "2025001")));

        mockMvc.perform(get("/api/players/me/faculty-courses/ICS/students")
                        .with(jwt().jwt(j -> j.subject("user_me"))))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$[0].studentName").value("Rahim"))
                .andExpect(jsonPath("$[0].studentId").value("2025001"));
    }

    @Test
    void getCourseStudents_empty_returns200() throws Exception {
        when(facultyCourseService.getCourseStudents(any(Jwt.class), eq("DM")))
                .thenReturn(List.of());

        mockMvc.perform(get("/api/players/me/faculty-courses/DM/students")
                        .with(jwt().jwt(j -> j.subject("user_me"))))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$").isArray())
                .andExpect(jsonPath("$").isEmpty());
    }

    @Test
    void getCourseStudents_studentRole_returns400() throws Exception {
        when(facultyCourseService.getCourseStudents(any(Jwt.class), eq("ICS")))
                .thenThrow(new IllegalArgumentException(FacultyCourseService.FACULTY_ONLY_MESSAGE));

        mockMvc.perform(get("/api/players/me/faculty-courses/ICS/students")
                        .with(jwt().jwt(j -> j.subject("user_me"))))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.success").value(false))
                .andExpect(jsonPath("$.message").value(FacultyCourseService.FACULTY_ONLY_MESSAGE));
    }

    @Test
    void getCourseMaterials_empty_returns200() throws Exception {
        when(facultyCourseService.getCourseMaterials(any(Jwt.class), eq("ICS")))
                .thenReturn(List.of());

        mockMvc.perform(get("/api/players/me/faculty-courses/ICS/materials")
                        .with(jwt().jwt(j -> j.subject("user_me"))))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$").isArray())
                .andExpect(jsonPath("$").isEmpty());
    }

    @Test
    void addCourseMaterial_invalid_returns400() throws Exception {
        when(facultyCourseService.addCourseMaterial(
                any(Jwt.class),
                eq("ICS"),
                eq("Notes"),
                eq("not-a-url")
        )).thenThrow(new IllegalArgumentException(FacultyCourseService.LINK_REQUIRED_MESSAGE));

        mockMvc.perform(post("/api/players/me/faculty-courses/ICS/materials")
                        .with(jwt().jwt(j -> j.subject("user_me")))
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"title":"Notes","url":"not-a-url"}
                                """))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.success").value(false))
                .andExpect(jsonPath("$.message").value(FacultyCourseService.LINK_REQUIRED_MESSAGE));
    }

    @Test
    void addCourseMaterial_json_returnsCreatedMaterial() throws Exception {
        when(facultyCourseService.addCourseMaterial(
                any(Jwt.class),
                eq("ICS"),
                eq("Chapter 1 Notes"),
                eq("https://drive.example/notes")
        )).thenReturn(new CourseMaterialResponse(
                UUID.fromString("11111111-1111-1111-1111-111111111111"),
                "ICS",
                "Chapter 1 Notes",
                "https://drive.example/notes",
                UUID.fromString("22222222-2222-2222-2222-222222222222"),
                Instant.parse("2026-01-01T00:00:00Z")
        ));

        mockMvc.perform(post("/api/players/me/faculty-courses/ICS/materials")
                        .with(jwt().jwt(j -> j.subject("user_me")))
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"title":"Chapter 1 Notes","url":"https://drive.example/notes"}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.title").value("Chapter 1 Notes"))
                .andExpect(jsonPath("$.url").value("https://drive.example/notes"))
                .andExpect(jsonPath("$.type").doesNotExist());
    }

    @Test
    void facultyCourses_unauthenticated_returns401() throws Exception {
        mockMvc.perform(get("/api/players/me/faculty-courses"))
                .andExpect(status().isUnauthorized());
        mockMvc.perform(get("/api/players/me/faculty-courses/ICS/students"))
                .andExpect(status().isUnauthorized());
        mockMvc.perform(get("/api/players/me/faculty-courses/ICS/materials"))
                .andExpect(status().isUnauthorized());
        mockMvc.perform(post("/api/players/me/faculty-courses/ICS/materials")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"title\":\"Notes\",\"url\":\"https://example.com\"}"))
                .andExpect(status().isUnauthorized());
        mockMvc.perform(get("/api/players/me/faculty-courses/ICS/materials/11111111-1111-1111-1111-111111111111/open"))
                .andExpect(status().isUnauthorized());
        mockMvc.perform(delete("/api/players/me/faculty-courses/ICS/materials/11111111-1111-1111-1111-111111111111"))
                .andExpect(status().isUnauthorized());
    }

    @Test
    void openCourseMaterial_returnsUrl() throws Exception {
        UUID materialId = UUID.fromString("11111111-1111-1111-1111-111111111111");
        when(facultyCourseService.openCourseMaterial(any(Jwt.class), eq("ICS"), eq(materialId)))
                .thenReturn(new CourseMaterialOpenResponse("https://drive.example/open"));

        mockMvc.perform(get("/api/players/me/faculty-courses/ICS/materials/" + materialId + "/open")
                        .with(jwt().jwt(j -> j.subject("user_me"))))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.url").value("https://drive.example/open"))
                .andExpect(jsonPath("$.type").doesNotExist());
    }

    @Test
    void deleteCourseMaterial_returns204() throws Exception {
        UUID materialId = UUID.fromString("11111111-1111-1111-1111-111111111111");

        mockMvc.perform(delete("/api/players/me/faculty-courses/ICS/materials/" + materialId)
                        .with(jwt().jwt(j -> j.subject("user_me"))))
                .andExpect(status().isNoContent());

        verify(facultyCourseService).deleteCourseMaterial(any(Jwt.class), eq("ICS"), eq(materialId));
    }

    @Test
    void deleteCourseMaterial_student_returns400() throws Exception {
        UUID materialId = UUID.fromString("11111111-1111-1111-1111-111111111111");
        doThrow(new IllegalArgumentException(FacultyCourseService.FACULTY_ONLY_MESSAGE))
                .when(facultyCourseService)
                .deleteCourseMaterial(any(Jwt.class), eq("ICS"), eq(materialId));

        mockMvc.perform(delete("/api/players/me/faculty-courses/ICS/materials/" + materialId)
                        .with(jwt().jwt(j -> j.subject("user_me"))))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.message").value(FacultyCourseService.FACULTY_ONLY_MESSAGE));
    }
}
