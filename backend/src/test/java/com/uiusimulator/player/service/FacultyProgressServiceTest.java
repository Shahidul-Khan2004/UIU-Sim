package com.uiusimulator.player.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.uiusimulator.player.dto.FacultyProgressResponse;
import com.uiusimulator.player.dto.PlayerSaveCreateRequest;
import com.uiusimulator.player.entity.Department;
import com.uiusimulator.player.entity.FacultyProgress;
import com.uiusimulator.player.entity.Player;
import com.uiusimulator.player.entity.PlayerRole;
import com.uiusimulator.player.entity.PlayerSave;
import com.uiusimulator.player.repository.DepartmentRepository;
import com.uiusimulator.player.repository.FacultyProgressRepository;
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
@Import({PlayerService.class, PlayerSaveService.class, FacultyProgressService.class, FacultyCourseService.class, FacultyTeachService.class})
class FacultyProgressServiceTest {

    @Autowired
    private FacultyProgressService facultyProgressService;

    @Autowired
    private FacultyCourseService facultyCourseService;

    @Autowired
    private PlayerSaveService playerSaveService;

    @Autowired
    private PlayerService playerService;

    @Autowired
    private PlayerRepository playerRepository;

    @Autowired
    private PlayerSaveRepository playerSaveRepository;

    @Autowired
    private DepartmentRepository departmentRepository;

    @Autowired
    private FacultyProgressRepository facultyProgressRepository;

    private Department cse;

    @BeforeEach
    void seedDepartment() {
        cse = departmentRepository.saveAndFlush(
                Department.createNew("CSE", "Computer Science and Engineering")
        );
    }

    @Test
    void facultyStartsWith50Reputation() {
        Jwt jwt = jwtWith("user_faculty_start");
        createSave(jwt, PlayerRole.FACULTY, "Lail", "F-001");

        FacultyProgressResponse response = facultyProgressService.getProgress(jwt);

        assertThat(response.reputation()).isEqualTo(FacultyProgress.DEFAULT_REPUTATION);
        assertThat(response.facultyIdIssued()).isTrue();
        assertThat(response.computerUsed()).isFalse();
        assertThat(response.officeEntered()).isFalse();
        assertThat(response.officeSetup()).isFalse();
        assertThat(response.classroomScanned()).isFalse();
        assertThat(response.lectureCompleted()).isFalse();
        assertThat(response.icsMaterialPrepared()).isFalse();
        assertThat(response.dmMaterialPrepared()).isFalse();
        assertThat(response.materialsPrepared()).isFalse();
        assertThat(response.icsCompleted()).isFalse();
        assertThat(response.dmCompleted()).isFalse();
        assertThat(response.nextCourseCode()).isEqualTo("ICS");
    }

    @Test
    void leftoverFacultyProgressIsResetOnNewJourney() {
        Jwt jwt = jwtWith("user_faculty_leftover");
        playerSaveService.createSave(
                jwt,
                new PlayerSaveCreateRequest(PlayerRole.FACULTY, "Lail", cse.getId(), "F-010")
        );

        facultyProgressService.useComputer(jwt);
        facultyCourseService.addCourseMaterial(jwt, "ICS", "ICS Notes", "https://drive.example/ics");
        facultyCourseService.addCourseMaterial(jwt, "DM", "DM Notes", "https://drive.example/dm");
        assertThat(facultyProgressService.getProgress(jwt).reputation()).isEqualTo(65);

        playerSaveService.deleteSave(jwt);
        playerSaveService.createSave(
                jwt,
                new PlayerSaveCreateRequest(PlayerRole.FACULTY, "Lail", cse.getId(), "F-011")
        );

        FacultyProgressResponse reset = facultyProgressService.getProgress(jwt);
        assertThat(reset.reputation()).isEqualTo(FacultyProgress.DEFAULT_REPUTATION);
        assertThat(reset.officeSetup()).isFalse();
        assertThat(reset.materialsPrepared()).isFalse();
        assertThat(reset.icsMaterialPrepared()).isFalse();
        assertThat(reset.dmMaterialPrepared()).isFalse();
        assertThat(reset.icsCompleted()).isFalse();
        assertThat(reset.dmCompleted()).isFalse();
    }

    @Test
    void getProgressDoesNotAwardInitializationRewards() {
        Jwt jwt = jwtWith("user_faculty_no_init_reward");
        createSave(jwt, PlayerRole.FACULTY, "Lail", "F-012");

        FacultyProgressResponse first = facultyProgressService.getProgress(jwt);
        FacultyProgressResponse second = facultyProgressService.getProgress(jwt);

        assertThat(first.reputation()).isEqualTo(FacultyProgress.DEFAULT_REPUTATION);
        assertThat(second.reputation()).isEqualTo(FacultyProgress.DEFAULT_REPUTATION);
        assertThat(second.officeSetup()).isFalse();
        assertThat(second.materialsPrepared()).isFalse();
    }

    @Test
    void computerGivesPlus5Once() {
        Jwt jwt = jwtWith("user_faculty_computer");
        createSave(jwt, PlayerRole.FACULTY, "Lail", "F-001");

        FacultyProgressResponse first = facultyProgressService.useComputer(jwt);
        assertThat(first.reputation()).isEqualTo(55);
        assertThat(first.computerUsed()).isTrue();
        assertThat(first.officeEntered()).isTrue();

        FacultyProgressResponse second = facultyProgressService.useComputer(jwt);
        assertThat(second.reputation()).isEqualTo(55);
        assertThat(second.computerUsed()).isTrue();
    }

    @Test
    void firstCourseMaterialGivesPlus5() {
        Jwt jwt = jwtWith("user_faculty_first_material");
        createSave(jwt, PlayerRole.FACULTY, "Lail", "F-001");

        facultyCourseService.addCourseMaterial(jwt, "ICS", "ICS Notes", "https://drive.example/ics");

        FacultyProgressResponse progress = facultyProgressService.getProgress(jwt);
        assertThat(progress.reputation()).isEqualTo(55);
        assertThat(progress.icsMaterialPrepared()).isTrue();
        assertThat(progress.dmMaterialPrepared()).isFalse();
    }

    @Test
    void secondMaterialGivesZero() {
        Jwt jwt = jwtWith("user_faculty_second_material");
        createSave(jwt, PlayerRole.FACULTY, "Lail", "F-001");

        facultyCourseService.addCourseMaterial(jwt, "ICS", "ICS Notes 1", "https://drive.example/ics-1");
        facultyCourseService.addCourseMaterial(jwt, "ICS", "ICS Notes 2", "https://drive.example/ics-2");

        FacultyProgressResponse progress = facultyProgressService.getProgress(jwt);
        assertThat(progress.reputation()).isEqualTo(55);
        assertThat(progress.icsMaterialPrepared()).isTrue();
    }

    @Test
    void firstMaterialPerCourseIsRewardedSeparately() {
        Jwt jwt = jwtWith("user_faculty_both_materials");
        createSave(jwt, PlayerRole.FACULTY, "Lail", "F-001");

        facultyCourseService.addCourseMaterial(jwt, "ICS", "ICS Notes", "https://drive.example/ics");
        facultyCourseService.addCourseMaterial(jwt, "DM", "DM Notes", "https://drive.example/dm");

        FacultyProgressResponse progress = facultyProgressService.getProgress(jwt);
        assertThat(progress.reputation()).isEqualTo(60);
        assertThat(progress.icsMaterialPrepared()).isTrue();
        assertThat(progress.dmMaterialPrepared()).isTrue();
    }

    @Test
    void studentCannotAccessFacultyProgress() {
        Jwt jwt = jwtWith("user_student_progress");
        createSave(jwt, PlayerRole.STUDENT, "Alex Student", "22112345");

        assertThatThrownBy(() -> facultyProgressService.getProgress(jwt))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessage(FacultyProgressService.FACULTY_ONLY_MESSAGE);
        assertThatThrownBy(() -> facultyProgressService.useComputer(jwt))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessage(FacultyProgressService.FACULTY_ONLY_MESSAGE);
        assertThat(facultyProgressRepository.findAll()).isEmpty();
    }

    private Player createSave(Jwt jwt, PlayerRole role, String name, String universityId) {
        Player player = playerService.getOrProvisionPlayer(jwt);
        playerSaveRepository.saveAndFlush(
                PlayerSave.createAfterAdmission(player, role, name, cse, universityId)
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
