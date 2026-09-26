package com.uiusimulator.player.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.uiusimulator.player.dto.FacultyRoutineResponse;
import com.uiusimulator.player.dto.FacultyRoutineResponse.FacultyRoutineItem;
import com.uiusimulator.player.entity.ClassroomCourseDefinition;
import com.uiusimulator.player.entity.Department;
import com.uiusimulator.player.entity.Player;
import com.uiusimulator.player.entity.PlayerRole;
import com.uiusimulator.player.entity.PlayerSave;
import com.uiusimulator.player.exception.PlayerSaveNotFoundException;
import com.uiusimulator.player.repository.DepartmentRepository;
import com.uiusimulator.player.repository.PlayerRepository;
import com.uiusimulator.player.repository.PlayerSaveRepository;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.orm.jpa.DataJpaTest;
import org.springframework.context.annotation.Import;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.test.context.ActiveProfiles;

@DataJpaTest
@ActiveProfiles("test")
@Import({PlayerService.class, FacultyRoutineService.class})
class FacultyRoutineServiceTest {

    @Autowired
    private FacultyRoutineService facultyRoutineService;

    @Autowired
    private PlayerService playerService;

    @Autowired
    private PlayerRepository playerRepository;

    @Autowired
    private PlayerSaveRepository playerSaveRepository;

    @Autowired
    private DepartmentRepository departmentRepository;

    private Department cse;

    @BeforeEach
    void seedDepartment() {
        cse = departmentRepository.saveAndFlush(
                Department.createNew("CSE", "Computer Science and Engineering")
        );
    }

    @Test
    void getFacultyRoutine_facultySave_returnsAssignedIdentityAndExistingClassrooms() {
        Jwt jwt = jwtWith("user_faculty_routine");
        createSave(jwt, PlayerRole.FACULTY, "Lail", "F-001");

        FacultyRoutineResponse response = facultyRoutineService.getFacultyRoutine(jwt);

        assertThat(response.profile().name()).isEqualTo("Lail");
        assertThat(response.profile().facultyId()).isEqualTo("F-001");
        assertThat(response.profile().department()).isEqualTo("CSE");
        assertThat(response.profile().designation()).isEqualTo(FacultyRoutineService.ASSIGNED_DESIGNATION);
        assertThat(response.profile().office()).isEqualTo(FacultyRoutineService.ASSIGNED_OFFICE);

        assertThat(response.routine()).hasSize(2);
        FacultyRoutineItem ics = response.routine().get(0);
        assertThat(ics.courseId()).isEqualTo(ClassroomCourseDefinition.ICS.courseId());
        assertThat(ics.courseName()).isEqualTo(ClassroomCourseDefinition.ICS.courseName());
        assertThat(ics.classroomNumber()).isEqualTo("427");
        assertThat(ics.floor()).isEqualTo(4);

        FacultyRoutineItem dm = response.routine().get(1);
        assertThat(dm.courseId()).isEqualTo(ClassroomCourseDefinition.DISCRETE_MATHEMATICS.courseId());
        assertThat(dm.courseName()).isEqualTo(ClassroomCourseDefinition.DISCRETE_MATHEMATICS.courseName());
        assertThat(dm.classroomNumber()).isEqualTo("423");
        assertThat(dm.floor()).isEqualTo(4);
    }

    @Test
    void getFacultyRoutine_studentSave_rejects() {
        Jwt jwt = jwtWith("user_student_routine");
        createSave(jwt, PlayerRole.STUDENT, "Alex Student", "22112345");

        assertThatThrownBy(() -> facultyRoutineService.getFacultyRoutine(jwt))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessage("Faculty routine is available only to faculty.");
    }

    @Test
    void getFacultyRoutine_noSave_notFound() {
        Jwt jwt = jwtWith("user_no_save_routine");
        playerService.getOrProvisionPlayer(jwt);

        assertThatThrownBy(() -> facultyRoutineService.getFacultyRoutine(jwt))
                .isInstanceOf(PlayerSaveNotFoundException.class);
    }

    private void createSave(Jwt jwt, PlayerRole role, String name, String universityId) {
        Player player = playerService.getOrProvisionPlayer(jwt);
        playerSaveRepository.saveAndFlush(
                PlayerSave.createAfterAdmission(player, role, name, cse, universityId)
        );
        assertThat(playerRepository.findByClerkUserId(jwt.getSubject())).isPresent();
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
