package com.uiusimulator.player.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.uiusimulator.player.dto.CourseMaterialOpenResponse;
import com.uiusimulator.player.dto.CourseMaterialResponse;
import com.uiusimulator.player.dto.FacultyCoursesResponse;
import com.uiusimulator.player.dto.FacultyCoursesResponse.FacultyCourseItem;
import com.uiusimulator.player.dto.FacultyStudentItem;
import com.uiusimulator.player.entity.ClassroomCourseDefinition;
import com.uiusimulator.player.entity.Department;
import com.uiusimulator.player.entity.FacultyCourseAssignment;
import com.uiusimulator.player.entity.Player;
import com.uiusimulator.player.entity.PlayerRole;
import com.uiusimulator.player.entity.PlayerSave;
import com.uiusimulator.player.repository.DepartmentRepository;
import com.uiusimulator.player.repository.CourseMaterialRepository;
import com.uiusimulator.player.repository.FacultyCourseAssignmentRepository;
import com.uiusimulator.player.repository.PlayerRepository;
import com.uiusimulator.player.repository.PlayerSaveRepository;
import jakarta.persistence.EntityManager;
import java.util.List;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.orm.jpa.DataJpaTest;
import org.springframework.context.annotation.Import;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.test.context.ActiveProfiles;

@DataJpaTest
@ActiveProfiles("test")
@Import({PlayerService.class, FacultyCourseService.class})
class FacultyCourseServiceTest {

    @Autowired
    private FacultyCourseService facultyCourseService;

    @Autowired
    private PlayerService playerService;

    @Autowired
    private PlayerRepository playerRepository;

    @Autowired
    private PlayerSaveRepository playerSaveRepository;

    @Autowired
    private DepartmentRepository departmentRepository;

    @Autowired
    private FacultyCourseAssignmentRepository assignmentRepository;

    @Autowired
    private CourseMaterialRepository materialRepository;

    @Autowired
    private EntityManager entityManager;

    private Department cse;
    private Department bba;

    @BeforeEach
    void seedDepartment() {
        cse = departmentRepository.saveAndFlush(
                Department.createNew("CSE", "Computer Science and Engineering")
        );
        bba = departmentRepository.saveAndFlush(
                Department.createNew("BBA", "Bachelor of Business Administration")
        );
    }

    @Test
    void getFacultyCourses_faculty_receivesAssignedCourses() {
        Jwt jwt = jwtWith("user_faculty_courses");
        createSave(jwt, PlayerRole.FACULTY, "Lail", "F-001");

        FacultyCoursesResponse response = facultyCourseService.getFacultyCourses(jwt);

        assertThat(response.facultyId()).isEqualTo("F-001");
        assertThat(response.courses()).hasSize(2);

        FacultyCourseItem ics = response.courses().get(0);
        assertThat(ics.courseCode()).isEqualTo(ClassroomCourseDefinition.ICS.courseId());
        assertThat(ics.courseName()).isEqualTo(ClassroomCourseDefinition.ICS.courseName());
        assertThat(ics.classroom()).isEqualTo("427");
        assertThat(ics.floor()).isEqualTo(4);

        FacultyCourseItem dm = response.courses().get(1);
        assertThat(dm.courseCode()).isEqualTo(ClassroomCourseDefinition.DISCRETE_MATHEMATICS.courseId());
        assertThat(dm.courseName()).isEqualTo(ClassroomCourseDefinition.DISCRETE_MATHEMATICS.courseName());
        assertThat(dm.classroom()).isEqualTo("423");
        assertThat(dm.floor()).isEqualTo(4);

        List<FacultyCourseAssignment> assignments = assignmentRepository.findByPlayerIdOrderByCreatedAtAsc(
                playerRepository.findByClerkUserId(jwt.getSubject()).orElseThrow().getId()
        );
        assertThat(assignments).hasSize(2);
    }

    @Test
    void getFacultyCourses_student_cannotAccess() {
        Jwt jwt = jwtWith("user_student_courses");
        createSave(jwt, PlayerRole.STUDENT, "Alex Student", "22112345");

        assertThatThrownBy(() -> facultyCourseService.getFacultyCourses(jwt))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessage(FacultyCourseService.FACULTY_ONLY_MESSAGE);
    }

    @Test
    void getCourseStudents_faculty_receivesCseStudentsForIcs() {
        Jwt facultyJwt = jwtWith("user_faculty_ics_students");
        createSave(facultyJwt, PlayerRole.FACULTY, "Lail", "F-001");
        createSave(jwtWith("user_cse_rahim"), PlayerRole.STUDENT, "Rahim", "2025001");
        createSave(jwtWith("user_cse_karim"), PlayerRole.STUDENT, "Karim", "2025002");
        createSave(jwtWith("user_bba_nadia"), PlayerRole.STUDENT, "Nadia", "2025003", bba);

        List<FacultyStudentItem> students = facultyCourseService.getCourseStudents(facultyJwt, "ICS");

        assertThat(students).containsExactly(
                new FacultyStudentItem("Karim", "2025002"),
                new FacultyStudentItem("Rahim", "2025001")
        );
    }

    @Test
    void getCourseStudents_faculty_receivesSameCseStudentsForDm() {
        Jwt facultyJwt = jwtWith("user_faculty_dm_students");
        createSave(facultyJwt, PlayerRole.FACULTY, "Lail", "F-001");
        createSave(jwtWith("user_dm_rahim"), PlayerRole.STUDENT, "Rahim", "2025001");
        createSave(jwtWith("user_bba_only"), PlayerRole.STUDENT, "Nadia", "2025003", bba);

        List<FacultyStudentItem> icsStudents = facultyCourseService.getCourseStudents(facultyJwt, "ICS");
        List<FacultyStudentItem> dmStudents = facultyCourseService.getCourseStudents(facultyJwt, "DM");

        assertThat(dmStudents).containsExactly(new FacultyStudentItem("Rahim", "2025001"));
        assertThat(dmStudents).isEqualTo(icsStudents);
    }

    @Test
    void getCourseStudents_emptyDepartment_returnsEmptyList() {
        Jwt facultyJwt = jwtWith("user_faculty_no_students");
        createSave(facultyJwt, PlayerRole.FACULTY, "Lail", "F-001");

        List<FacultyStudentItem> students = facultyCourseService.getCourseStudents(facultyJwt, "DM");

        assertThat(students).isEmpty();
    }

    @Test
    void getCourseMaterials_empty_returnsEmptyList() {
        Jwt jwt = jwtWith("user_faculty_empty_materials");
        createSave(jwt, PlayerRole.FACULTY, "Lail", "F-001");

        assertThat(facultyCourseService.getCourseMaterials(jwt, "ICS")).isEmpty();
    }

    @Test
    void addCourseMaterial_invalidPayload_rejected() {
        Jwt jwt = jwtWith("user_faculty_invalid_material");
        createSave(jwt, PlayerRole.FACULTY, "Lail", "F-001");

        assertThatThrownBy(() -> facultyCourseService.addCourseMaterial(
                jwt, "ICS", " ", "https://example.com"
        )).isInstanceOf(IllegalArgumentException.class)
                .hasMessage(FacultyCourseService.TITLE_REQUIRED_MESSAGE);

        assertThatThrownBy(() -> facultyCourseService.addCourseMaterial(
                jwt, "ICS", "Notes", "not-a-url"
        )).isInstanceOf(IllegalArgumentException.class)
                .hasMessage(FacultyCourseService.LINK_REQUIRED_MESSAGE);

        assertThatThrownBy(() -> facultyCourseService.addCourseMaterial(
                jwt, "ICS", "Notes", ""
        )).isInstanceOf(IllegalArgumentException.class)
                .hasMessage(FacultyCourseService.LINK_REQUIRED_MESSAGE);
    }

    @Test
    void getCourseMaterials_faculty_canViewMaterials() {
        Jwt jwt = jwtWith("user_faculty_view_materials");
        createSave(jwt, PlayerRole.FACULTY, "Lail", "F-001");
        facultyCourseService.addCourseMaterial(
                jwt,
                "ICS",
                "ICS Lecture Notes",
                "https://drive.example/lecture"
        );

        List<CourseMaterialResponse> materials = facultyCourseService.getCourseMaterials(jwt, "ICS");

        assertThat(materials).hasSize(1);
        assertThat(materials.get(0).title()).isEqualTo("ICS Lecture Notes");
        assertThat(materials.get(0).url()).isEqualTo("https://drive.example/lecture");
        assertThat(materials.get(0).courseId()).isEqualTo("ICS");
    }

    @Test
    void addCourseMaterial_faculty_canAddLink() {
        Jwt jwt = jwtWith("user_faculty_add_materials");
        createSave(jwt, PlayerRole.FACULTY, "Lail", "F-001");

        CourseMaterialResponse link = facultyCourseService.addCourseMaterial(
                jwt,
                "DM",
                "Chapter 1 Notes",
                "https://drive.example/notes"
        );
        assertThat(link.title()).isEqualTo("Chapter 1 Notes");
        assertThat(link.url()).isEqualTo("https://drive.example/notes");
        assertThat(facultyCourseService.getCourseMaterials(jwt, "DM")).hasSize(1);
    }

    @Test
    void addCourseMaterial_persistsForCorrectCourseAfterReload() {
        Jwt jwt = jwtWith("user_faculty_persist_material");
        createSave(jwt, PlayerRole.FACULTY, "Lail", "F-001");
        CourseMaterialResponse saved = facultyCourseService.addCourseMaterial(
                jwt,
                "ICS",
                "Lecture Notes",
                "https://drive.example/persist"
        );

        entityManager.flush();
        entityManager.clear();

        var reloaded = materialRepository.findById(saved.id()).orElseThrow();
        assertThat(reloaded.getCourseId()).isEqualTo("ICS");
        assertThat(reloaded.getTitle()).isEqualTo("Lecture Notes");
        assertThat(reloaded.getUrl()).isEqualTo("https://drive.example/persist");
        assertThat(facultyCourseService.getCourseMaterials(jwt, "ICS")).hasSize(1);
        assertThat(facultyCourseService.getCourseMaterials(jwt, "DM")).isEmpty();
    }

    @Test
    void openCourseMaterial_returnsStoredUrl() {
        Jwt jwt = jwtWith("user_faculty_open_material");
        createSave(jwt, PlayerRole.FACULTY, "Lail", "F-001");
        CourseMaterialResponse link = facultyCourseService.addCourseMaterial(
                jwt, "ICS", "Drive Notes", "https://drive.example/open"
        );

        CourseMaterialOpenResponse linkOpen = facultyCourseService.openCourseMaterial(jwt, "ICS", link.id());
        assertThat(linkOpen.url()).isEqualTo("https://drive.example/open");
    }

    @Test
    void deleteCourseMaterial_removesDatabaseRow() {
        Jwt jwt = jwtWith("user_faculty_delete_material");
        createSave(jwt, PlayerRole.FACULTY, "Lail", "F-001");
        CourseMaterialResponse link = facultyCourseService.addCourseMaterial(
                jwt, "ICS", "Drive Notes", "https://drive.example/delete"
        );

        facultyCourseService.deleteCourseMaterial(jwt, "ICS", link.id());

        assertThat(materialRepository.findById(link.id())).isEmpty();
        assertThat(facultyCourseService.getCourseMaterials(jwt, "ICS")).isEmpty();
    }

    @Test
    void deleteCourseMaterial_studentAndUnassignedFaculty_rejected() {
        Jwt ownerJwt = jwtWith("user_faculty_owner_material");
        createSave(ownerJwt, PlayerRole.FACULTY, "Lail", "F-001");
        CourseMaterialResponse material = facultyCourseService.addCourseMaterial(
                ownerJwt, "ICS", "Drive Notes", "https://drive.example/secure"
        );

        Jwt studentJwt = jwtWith("user_student_delete_material");
        createSave(studentJwt, PlayerRole.STUDENT, "Alex Student", "22112345");
        assertThatThrownBy(() -> facultyCourseService.deleteCourseMaterial(studentJwt, "ICS", material.id()))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessage(FacultyCourseService.FACULTY_ONLY_MESSAGE);
        assertThatThrownBy(() -> facultyCourseService.getCourseMaterials(studentJwt, "ICS"))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessage(FacultyCourseService.FACULTY_ONLY_MESSAGE);
        assertThatThrownBy(() -> facultyCourseService.openCourseMaterial(studentJwt, "ICS", material.id()))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessage(FacultyCourseService.FACULTY_ONLY_MESSAGE);
        assertThatThrownBy(() -> facultyCourseService.addCourseMaterial(
                studentJwt, "ICS", "Notes", "https://example.com"
        )).isInstanceOf(IllegalArgumentException.class)
                .hasMessage(FacultyCourseService.FACULTY_ONLY_MESSAGE);

        Jwt otherFacultyJwt = jwtWith("user_faculty_other_material");
        Player other = createSave(otherFacultyJwt, PlayerRole.FACULTY, "Other", "F-002");
        facultyCourseService.getFacultyCourses(otherFacultyJwt);
        assignmentRepository.deleteByPlayerIdAndCourseId(other.getId(), "ICS");
        entityManager.flush();
        assertThatThrownBy(() -> facultyCourseService.deleteCourseMaterial(otherFacultyJwt, "ICS", material.id()))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessage(FacultyCourseService.NOT_ASSIGNED_MESSAGE);
        assertThatThrownBy(() -> facultyCourseService.getCourseMaterials(otherFacultyJwt, "ICS"))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessage(FacultyCourseService.NOT_ASSIGNED_MESSAGE);
        assertThat(materialRepository.findById(material.id())).isPresent();
    }

    @Test
    void unauthorizedRole_receivesFacultyOnlyError() {
        Jwt jwt = jwtWith("user_student_unauthorized");
        createSave(jwt, PlayerRole.STUDENT, "Alex Student", "22112345");

        assertThatThrownBy(() -> facultyCourseService.getCourseStudents(jwt, "ICS"))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessage(FacultyCourseService.FACULTY_ONLY_MESSAGE);
        assertThatThrownBy(() -> facultyCourseService.getCourseMaterials(jwt, "ICS"))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessage(FacultyCourseService.FACULTY_ONLY_MESSAGE);
        assertThatThrownBy(() -> facultyCourseService.addCourseMaterial(
                jwt,
                "ICS",
                "Notes",
                "https://example.com"
        ))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessage(FacultyCourseService.FACULTY_ONLY_MESSAGE);
    }

    private Player createSave(Jwt jwt, PlayerRole role, String name, String universityId) {
        return createSave(jwt, role, name, universityId, cse);
    }

    private Player createSave(Jwt jwt, PlayerRole role, String name, String universityId, Department department) {
        Player player = playerService.getOrProvisionPlayer(jwt);
        playerSaveRepository.saveAndFlush(
                PlayerSave.createAfterAdmission(player, role, name, department, universityId)
        );
        assertThat(playerRepository.findByClerkUserId(jwt.getSubject())).isPresent();
        return player;
    }

    private static Jwt jwtWith(String subject) {
        return Jwt.withTokenValue("token-" + subject)
                .header("alg", "none")
                .subject(subject)
                .claim("email", subject + "@uiu.edu")
                .claim("username", subject)
                .build();
    }
}
