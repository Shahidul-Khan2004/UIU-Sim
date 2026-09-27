package com.uiusimulator.player.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.uiusimulator.player.dto.FacultyProgressResponse;
import com.uiusimulator.player.entity.ClassroomCourseDefinition;
import com.uiusimulator.player.entity.Department;
import com.uiusimulator.player.entity.FacultyProgress;
import com.uiusimulator.player.entity.Player;
import com.uiusimulator.player.entity.PlayerRole;
import com.uiusimulator.player.entity.PlayerSave;
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
@Import({PlayerService.class, FacultyProgressService.class, FacultyTeachService.class, FacultyCourseService.class})
class FacultyTeachServiceTest {

    @Autowired
    private FacultyTeachService facultyTeachService;

    @Autowired
    private FacultyProgressService facultyProgressService;

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
    void scanningDiscreteMathematicsFirstIsWrongClassroomAndPenalizes() {
        Jwt jwt = jwtWith("user_faculty_wrong_room");
        createSave(jwt, PlayerRole.FACULTY, "Lali", "F-001");

        FacultyProgressResponse response = facultyTeachService.scanClassroom(jwt, "DM");

        assertThat(response.reputation()).isEqualTo(45);
        assertThat(response.classroomScanned()).isFalse();
        assertThat(response.icsCompleted()).isFalse();
        assertThat(response.dmCompleted()).isFalse();
        assertThat(response.nextCourseCode()).isEqualTo("ICS");
        assertThat(response.activeCourseCode()).isNull();
        assertThat(response.teachBlockedReason()).isEqualTo(
                FacultyTeachService.wrongClassroomMessage(ClassroomCourseDefinition.ICS)
        );
    }

    @Test
    void icsLectureCompletesIndependentlyThenUnlocksDiscreteMathematics() {
        Jwt jwt = jwtWith("user_faculty_ics_then_dm");
        createSave(jwt, PlayerRole.FACULTY, "Lali", "F-001");

        FacultyProgressResponse scanIcs = facultyTeachService.scanClassroom(jwt, "ICS");
        assertThat(scanIcs.reputation()).isEqualTo(60);
        assertThat(scanIcs.teachBlockedReason()).isNull();
        assertThat(scanIcs.activeCourseCode()).isEqualTo("ICS");

        FacultyProgressResponse completeIcs = facultyTeachService.completeLecture(jwt);
        assertThat(completeIcs.reputation()).isEqualTo(65);
        assertThat(completeIcs.icsCompleted()).isTrue();
        assertThat(completeIcs.dmCompleted()).isFalse();
        assertThat(completeIcs.nextCourseCode()).isEqualTo("DM");
        assertThat(completeIcs.activeCourseCode()).isNull();
        assertThat(completeIcs.lectureCompleted()).isFalse();

        FacultyProgressResponse scanDm = facultyTeachService.scanClassroom(jwt, "DM");
        assertThat(scanDm.reputation()).isEqualTo(65);
        assertThat(scanDm.teachBlockedReason()).isNull();
        assertThat(scanDm.activeCourseCode()).isEqualTo("DM");

        FacultyProgressResponse completeDm = facultyTeachService.completeLecture(jwt);
        assertThat(completeDm.reputation()).isEqualTo(70);
        assertThat(completeDm.icsCompleted()).isTrue();
        assertThat(completeDm.dmCompleted()).isTrue();
        assertThat(completeDm.nextCourseCode()).isNull();
        assertThat(completeDm.lectureCompleted()).isTrue();
    }

    @Test
    void returningToCompletedIcsIsAlreadyTakenWithoutReputationChange() {
        Jwt jwt = jwtWith("user_faculty_already_taken");
        createSave(jwt, PlayerRole.FACULTY, "Lali", "F-001");
        facultyTeachService.scanClassroom(jwt, "ICS");
        facultyTeachService.completeLecture(jwt);

        FacultyProgressResponse again = facultyTeachService.scanClassroom(jwt, "ICS");

        assertThat(again.reputation()).isEqualTo(65);
        assertThat(again.icsCompleted()).isTrue();
        assertThat(again.dmCompleted()).isFalse();
        assertThat(again.activeCourseCode()).isNull();
        assertThat(again.teachBlockedReason()).isEqualTo(FacultyTeachService.ALREADY_TAKEN_MESSAGE);
    }

    @Test
    void classroomScanGivesPlus10OnceOnFirstSuccessfulClassroom() {
        Jwt jwt = jwtWith("user_faculty_scan");
        createSave(jwt, PlayerRole.FACULTY, "Lail", "F-001");

        FacultyProgressResponse first = facultyTeachService.scanClassroom(jwt, "ICS");
        assertThat(first.reputation()).isEqualTo(60);
        assertThat(first.classroomScanned()).isTrue();

        FacultyProgressResponse second = facultyTeachService.scanClassroom(jwt, "ICS");
        assertThat(second.reputation()).isEqualTo(60);
        assertThat(second.classroomScanned()).isTrue();
        assertThat(second.teachBlockedReason()).isNull();
    }

    @Test
    void completeLectureGivesPlus5OncePerCourse() {
        Jwt jwt = jwtWith("user_faculty_complete");
        createSave(jwt, PlayerRole.FACULTY, "Lail", "F-001");
        facultyTeachService.scanClassroom(jwt, "ICS");

        FacultyProgressResponse first = facultyTeachService.completeLecture(jwt);
        assertThat(first.reputation()).isEqualTo(65);
        assertThat(first.icsCompleted()).isTrue();

        assertThatThrownBy(() -> facultyTeachService.completeLecture(jwt))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessage(FacultyTeachService.SCAN_REQUIRED_MESSAGE);
    }

    @Test
    void leaveLectureGivesMinus10ForActiveSessionOnly() {
        Jwt jwt = jwtWith("user_faculty_leave");
        createSave(jwt, PlayerRole.FACULTY, "Lail", "F-001");
        facultyTeachService.scanClassroom(jwt, "ICS");

        FacultyProgressResponse first = facultyTeachService.leaveLecture(jwt);
        assertThat(first.reputation()).isEqualTo(50);
        assertThat(first.lectureLeft()).isTrue();
        assertThat(first.icsCompleted()).isFalse();
        assertThat(first.activeCourseCode()).isNull();

        assertThatThrownBy(() -> facultyTeachService.leaveLecture(jwt))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessage(FacultyTeachService.SCAN_REQUIRED_MESSAGE);

        FacultyProgressResponse retry = facultyTeachService.scanClassroom(jwt, "ICS");
        assertThat(retry.reputation()).isEqualTo(50);
        assertThat(retry.teachBlockedReason()).isNull();
        assertThat(retry.activeCourseCode()).isEqualTo("ICS");
    }

    @Test
    void completeRequiresScan() {
        Jwt jwt = jwtWith("user_faculty_complete_no_scan");
        createSave(jwt, PlayerRole.FACULTY, "Lail", "F-001");

        assertThatThrownBy(() -> facultyTeachService.completeLecture(jwt))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessage(FacultyTeachService.SCAN_REQUIRED_MESSAGE);
    }

    @Test
    void studentCannotTeach() {
        Jwt jwt = jwtWith("user_student_teach");
        createSave(jwt, PlayerRole.STUDENT, "Alex Student", "22112345");

        assertThatThrownBy(() -> facultyTeachService.scanClassroom(jwt, "ICS"))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessage(FacultyProgressService.FACULTY_ONLY_MESSAGE);
        assertThatThrownBy(() -> facultyTeachService.completeLecture(jwt))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessage(FacultyProgressService.FACULTY_ONLY_MESSAGE);
        assertThatThrownBy(() -> facultyTeachService.leaveLecture(jwt))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessage(FacultyProgressService.FACULTY_ONLY_MESSAGE);
        assertThat(facultyProgressService).isNotNull();
        assertThatThrownBy(() -> facultyProgressService.getProgress(jwt))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessage(FacultyProgressService.FACULTY_ONLY_MESSAGE);
    }

    @Test
    void englishCourseIsNotAssigned() {
        Jwt jwt = jwtWith("user_faculty_english");
        createSave(jwt, PlayerRole.FACULTY, "Lail", "F-001");

        assertThatThrownBy(() -> facultyTeachService.scanClassroom(jwt, "ENGLISH"))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessage(FacultyTeachService.NOT_ASSIGNED_MESSAGE);
        assertThat(facultyProgressService.getProgress(jwt).reputation())
                .isEqualTo(FacultyProgress.DEFAULT_REPUTATION);
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
