package com.uiusimulator.player.controller;

import com.uiusimulator.player.dto.CourseMaterialOpenResponse;
import com.uiusimulator.player.dto.CourseMaterialResponse;
import com.uiusimulator.player.dto.CreateCourseMaterialRequest;
import com.uiusimulator.player.dto.FacultyCoursesResponse;
import com.uiusimulator.player.dto.FacultyStudentItem;
import com.uiusimulator.player.service.FacultyCourseService;
import jakarta.validation.Valid;
import java.util.List;
import java.util.UUID;
import org.springframework.http.MediaType;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/api/players")
public class FacultyCourseController {

    private final FacultyCourseService facultyCourseService;

    public FacultyCourseController(FacultyCourseService facultyCourseService) {
        this.facultyCourseService = facultyCourseService;
    }

    @GetMapping("/me/faculty-courses")
    public ResponseEntity<FacultyCoursesResponse> getFacultyCourses(@AuthenticationPrincipal Jwt jwt) {
        return ResponseEntity.ok(facultyCourseService.getFacultyCourses(jwt));
    }

    @GetMapping("/me/faculty-courses/{courseId}/students")
    public ResponseEntity<List<FacultyStudentItem>> getCourseStudents(
            @AuthenticationPrincipal Jwt jwt,
            @PathVariable String courseId
    ) {
        return ResponseEntity.ok(facultyCourseService.getCourseStudents(jwt, courseId));
    }

    @GetMapping("/me/faculty-courses/{courseId}/materials")
    public ResponseEntity<List<CourseMaterialResponse>> getCourseMaterials(
            @AuthenticationPrincipal Jwt jwt,
            @PathVariable String courseId
    ) {
        return ResponseEntity.ok(facultyCourseService.getCourseMaterials(jwt, courseId));
    }

    @PostMapping(
            value = "/me/faculty-courses/{courseId}/materials",
            consumes = MediaType.APPLICATION_JSON_VALUE
    )
    public ResponseEntity<CourseMaterialResponse> addCourseMaterial(
            @AuthenticationPrincipal Jwt jwt,
            @PathVariable String courseId,
            @Valid @RequestBody CreateCourseMaterialRequest request
    ) {
        return ResponseEntity.ok(facultyCourseService.addCourseMaterial(
                jwt,
                courseId,
                request.title(),
                request.url()
        ));
    }

    @GetMapping("/me/faculty-courses/{courseId}/materials/{materialId}/open")
    public ResponseEntity<CourseMaterialOpenResponse> openCourseMaterial(
            @AuthenticationPrincipal Jwt jwt,
            @PathVariable String courseId,
            @PathVariable UUID materialId
    ) {
        return ResponseEntity.ok(facultyCourseService.openCourseMaterial(jwt, courseId, materialId));
    }

    @DeleteMapping("/me/faculty-courses/{courseId}/materials/{materialId}")
    public ResponseEntity<Void> deleteCourseMaterial(
            @AuthenticationPrincipal Jwt jwt,
            @PathVariable String courseId,
            @PathVariable UUID materialId
    ) {
        facultyCourseService.deleteCourseMaterial(jwt, courseId, materialId);
        return ResponseEntity.noContent().build();
    }
}
