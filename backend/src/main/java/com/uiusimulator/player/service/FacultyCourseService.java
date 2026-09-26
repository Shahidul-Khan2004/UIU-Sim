package com.uiusimulator.player.service;

import com.uiusimulator.player.dto.CourseMaterialOpenResponse;
import com.uiusimulator.player.dto.CourseMaterialResponse;
import com.uiusimulator.player.dto.FacultyCoursesResponse;
import com.uiusimulator.player.dto.FacultyCoursesResponse.FacultyCourseItem;
import com.uiusimulator.player.dto.FacultyStudentItem;
import com.uiusimulator.player.entity.ClassroomCourseDefinition;
import com.uiusimulator.player.entity.ClassroomLocationCatalog;
import com.uiusimulator.player.entity.CourseMaterial;
import com.uiusimulator.player.entity.FacultyCourseAssignment;
import com.uiusimulator.player.entity.FacultyCourseAssignmentCatalog;
import com.uiusimulator.player.entity.Player;
import com.uiusimulator.player.entity.PlayerRole;
import com.uiusimulator.player.entity.PlayerSave;
import com.uiusimulator.player.exception.PlayerSaveNotFoundException;
import com.uiusimulator.player.repository.CourseMaterialRepository;
import com.uiusimulator.player.repository.FacultyCourseAssignmentRepository;
import com.uiusimulator.player.repository.PlayerSaveRepository;
import java.net.URI;
import java.util.Comparator;
import java.util.List;
import java.util.UUID;
import org.springframework.dao.DataIntegrityViolationException;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
public class FacultyCourseService {

    public static final String FACULTY_ONLY_MESSAGE = "Faculty courses are available only to faculty.";
    public static final String NOT_ASSIGNED_MESSAGE = "You are not assigned to this course.";
    public static final String TITLE_REQUIRED_MESSAGE = "Please provide required information.";
    public static final String LINK_REQUIRED_MESSAGE = "Please provide required information.";
    public static final String MATERIAL_NOT_FOUND_MESSAGE = "Material not found.";

    private final PlayerService playerService;
    private final PlayerSaveRepository playerSaveRepository;
    private final FacultyCourseAssignmentRepository assignmentRepository;
    private final CourseMaterialRepository materialRepository;

    public FacultyCourseService(
            PlayerService playerService,
            PlayerSaveRepository playerSaveRepository,
            FacultyCourseAssignmentRepository assignmentRepository,
            CourseMaterialRepository materialRepository
    ) {
        this.playerService = playerService;
        this.playerSaveRepository = playerSaveRepository;
        this.assignmentRepository = assignmentRepository;
        this.materialRepository = materialRepository;
    }

    @Transactional
    public FacultyCoursesResponse getFacultyCourses(Jwt jwt) {
        FacultyContext context = requireFaculty(jwt);
        List<FacultyCourseAssignment> assignments = ensureDefaultAssignments(context.player().getId());
        List<FacultyCourseItem> courses = assignments.stream()
                .map(FacultyCourseAssignment::getCourseId)
                .map(FacultyCourseService::toCourseItem)
                .toList();
        return new FacultyCoursesResponse(context.save().getUniversityId(), courses);
    }

    @Transactional
    public List<FacultyStudentItem> getCourseStudents(Jwt jwt, String courseId) {
        requireAssignedFaculty(jwt, courseId);
        ClassroomCourseDefinition course = ClassroomCourseDefinition.byCourseId(normalizeCourseId(courseId));

        List<PlayerSave> students = playerSaveRepository.findByRoleAndDepartmentCode(
                PlayerRole.STUDENT,
                course.departmentCode()
        );
        if (students.isEmpty()) {
            return List.of();
        }

        return students.stream()
                .map(save -> new FacultyStudentItem(
                        blankToDash(save.getPlayerName()),
                        blankToDash(save.getUniversityId())
                ))
                .sorted(Comparator
                        .comparing(FacultyStudentItem::studentName, String.CASE_INSENSITIVE_ORDER)
                        .thenComparing(FacultyStudentItem::studentId, String.CASE_INSENSITIVE_ORDER))
                .toList();
    }

    @Transactional
    public List<CourseMaterialResponse> getCourseMaterials(Jwt jwt, String courseId) {
        requireAssignedFaculty(jwt, courseId);
        return materialRepository.findByCourseIdOrderByCreatedAtAsc(normalizeCourseId(courseId)).stream()
                .map(CourseMaterialResponse::from)
                .toList();
    }

    @Transactional
    public CourseMaterialResponse addCourseMaterial(Jwt jwt, String courseId, String title, String url) {
        FacultyContext context = requireAssignedFaculty(jwt, courseId);
        String normalizedCourseId = normalizeCourseId(courseId);
        ClassroomCourseDefinition.byCourseId(normalizedCourseId);

        CourseMaterial material = CourseMaterial.create(
                normalizedCourseId,
                requireTitle(title),
                requireHttpUrl(url),
                context.player().getId()
        );
        return CourseMaterialResponse.from(materialRepository.save(material));
    }

    @Transactional
    public CourseMaterialOpenResponse openCourseMaterial(Jwt jwt, String courseId, UUID materialId) {
        CourseMaterial material = requireOwnedMaterial(jwt, courseId, materialId);
        return new CourseMaterialOpenResponse(requireHttpUrl(material.getUrl()));
    }

    @Transactional
    public void deleteCourseMaterial(Jwt jwt, String courseId, UUID materialId) {
        CourseMaterial material = requireOwnedMaterial(jwt, courseId, materialId);
        materialRepository.delete(material);
    }

    private CourseMaterial requireOwnedMaterial(Jwt jwt, String courseId, UUID materialId) {
        requireAssignedFaculty(jwt, courseId);
        if (materialId == null) {
            throw new IllegalArgumentException(MATERIAL_NOT_FOUND_MESSAGE);
        }
        return materialRepository.findByIdAndCourseId(materialId, normalizeCourseId(courseId))
                .orElseThrow(() -> new IllegalArgumentException(MATERIAL_NOT_FOUND_MESSAGE));
    }

    private FacultyContext requireAssignedFaculty(Jwt jwt, String courseId) {
        FacultyContext context = requireFaculty(jwt);
        String normalizedCourseId = normalizeCourseId(courseId);
        ClassroomCourseDefinition.byCourseId(normalizedCourseId);
        ensureDefaultAssignments(context.player().getId());
        if (!assignmentRepository.existsByPlayerIdAndCourseId(context.player().getId(), normalizedCourseId)) {
            throw new IllegalArgumentException(NOT_ASSIGNED_MESSAGE);
        }
        return context;
    }

    private FacultyContext requireFaculty(Jwt jwt) {
        Player player = playerService.getOrProvisionPlayer(jwt);
        PlayerSave save = playerSaveRepository.findByPlayerId(player.getId())
                .orElseThrow(PlayerSaveNotFoundException::new);
        if (save.getRole() != PlayerRole.FACULTY) {
            throw new IllegalArgumentException(FACULTY_ONLY_MESSAGE);
        }
        return new FacultyContext(player, save);
    }

    private List<FacultyCourseAssignment> ensureDefaultAssignments(UUID playerId) {
        List<FacultyCourseAssignment> existing = assignmentRepository.findByPlayerIdOrderByCreatedAtAsc(playerId);
        if (!existing.isEmpty()) {
            return existing;
        }

        for (ClassroomCourseDefinition course : FacultyCourseAssignmentCatalog.defaultAssignedCourses()) {
            try {
                assignmentRepository.saveAndFlush(FacultyCourseAssignment.assign(playerId, course.courseId()));
            } catch (DataIntegrityViolationException ignored) {
                // Concurrent first access already inserted this assignment.
            }
        }
        return assignmentRepository.findByPlayerIdOrderByCreatedAtAsc(playerId);
    }

    private static FacultyCourseItem toCourseItem(String courseId) {
        ClassroomCourseDefinition course = ClassroomCourseDefinition.byCourseId(courseId);
        ClassroomLocationCatalog.Location location = ClassroomLocationCatalog.require(course.courseId());
        return new FacultyCourseItem(
                course.courseId(),
                course.courseName(),
                location.classroomNumber(),
                location.floor()
        );
    }

    private static String requireTitle(String title) {
        if (title == null || title.isBlank()) {
            throw new IllegalArgumentException(TITLE_REQUIRED_MESSAGE);
        }
        return title.trim();
    }

    private static String requireHttpUrl(String url) {
        if (!isValidHttpUrl(url)) {
            throw new IllegalArgumentException(LINK_REQUIRED_MESSAGE);
        }
        return url.trim();
    }

    private static boolean isValidHttpUrl(String url) {
        if (url == null || url.isBlank()) {
            return false;
        }
        try {
            URI uri = URI.create(url.trim());
            String scheme = uri.getScheme();
            return ("http".equalsIgnoreCase(scheme) || "https".equalsIgnoreCase(scheme))
                    && uri.getHost() != null
                    && !uri.getHost().isBlank();
        } catch (IllegalArgumentException ex) {
            return false;
        }
    }

    private static String normalizeCourseId(String courseId) {
        if (courseId == null || courseId.isBlank()) {
            throw new IllegalArgumentException("Unknown course.");
        }
        return courseId.trim();
    }

    private static String blankToDash(String value) {
        return value == null || value.isBlank() ? "—" : value.trim();
    }

    private record FacultyContext(Player player, PlayerSave save) {
    }
}
