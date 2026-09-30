package com.uiusimulator.player.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.uiusimulator.player.dto.FacultyProgressResponse;
import com.uiusimulator.player.entity.Department;
import com.uiusimulator.player.entity.FacultyCoffeeOption;
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
@Import({
        PlayerService.class,
        FacultyProgressService.class,
        FacultyTeachService.class,
        FacultyCourseService.class
})
class FacultyCoffeeServiceTest {

    @Autowired
    private FacultyProgressService facultyProgressService;

    @Autowired
    private FacultyTeachService facultyTeachService;

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
    void cappuccinoGivesPlus1OnceAfterBothClasses() {
        Jwt jwt = jwtWith("user_faculty_coffee_cappuccino");
        createSave(jwt, PlayerRole.FACULTY, "Lail", "F-C01");
        completeBothClasses(jwt);

        FacultyProgressResponse first = facultyProgressService.claimCoffee(jwt, FacultyCoffeeOption.CAPPUCCINO);
        assertThat(first.reputation()).isEqualTo(71);
        assertThat(first.coffeeClaimedForCurrentDay()).isTrue();
        assertThat(first.coffeeOption()).isEqualTo("CAPPUCCINO");
        assertThat(first.icsCompleted()).isTrue();
        assertThat(first.dmCompleted()).isTrue();

        FacultyProgressResponse second = facultyProgressService.claimCoffee(jwt, FacultyCoffeeOption.CAPPUCCINO);
        assertThat(second.reputation()).isEqualTo(71);
        assertThat(second.coffeeClaimedForCurrentDay()).isTrue();
        assertThat(second.coffeeOption()).isEqualTo("CAPPUCCINO");
    }

    @Test
    void cookieComboGivesPlus2Exclusive() {
        Jwt jwt = jwtWith("user_faculty_coffee_cookie");
        createSave(jwt, PlayerRole.FACULTY, "Lail", "F-C02");
        completeBothClasses(jwt);

        FacultyProgressResponse response = facultyProgressService.claimCoffee(
                jwt,
                FacultyCoffeeOption.CAPPUCCINO_COOKIE
        );

        assertThat(response.reputation()).isEqualTo(72);
        assertThat(response.coffeeOption()).isEqualTo("CAPPUCCINO_COOKIE");
    }

    @Test
    void brownieComboGivesPlus3ExclusiveNotSix() {
        Jwt jwt = jwtWith("user_faculty_coffee_brownie");
        createSave(jwt, PlayerRole.FACULTY, "Lail", "F-C03");
        completeBothClasses(jwt);

        FacultyProgressResponse response = facultyProgressService.claimCoffee(
                jwt,
                FacultyCoffeeOption.CAPPUCCINO_COOKIE_BROWNIE
        );

        assertThat(response.reputation()).isEqualTo(73);
        assertThat(response.coffeeOption()).isEqualTo("CAPPUCCINO_COOKIE_BROWNIE");
    }

    @Test
    void differentOptionAfterClaimDoesNotAwardAgain() {
        Jwt jwt = jwtWith("user_faculty_coffee_switch");
        createSave(jwt, PlayerRole.FACULTY, "Lail", "F-C04");
        completeBothClasses(jwt);

        facultyProgressService.claimCoffee(jwt, FacultyCoffeeOption.CAPPUCCINO);
        FacultyProgressResponse second = facultyProgressService.claimCoffee(
                jwt,
                FacultyCoffeeOption.CAPPUCCINO_COOKIE_BROWNIE
        );

        assertThat(second.reputation()).isEqualTo(71);
        assertThat(second.coffeeOption()).isEqualTo("CAPPUCCINO");
    }

    @Test
    void coffeeBlockedBeforeBothClassesComplete() {
        Jwt jwt = jwtWith("user_faculty_coffee_early");
        createSave(jwt, PlayerRole.FACULTY, "Lail", "F-C05");
        facultyTeachService.scanClassroom(jwt, "ICS");
        facultyTeachService.completeLecture(jwt);

        assertThatThrownBy(() -> facultyProgressService.claimCoffee(jwt, FacultyCoffeeOption.CAPPUCCINO))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessage(FacultyProgressService.COFFEE_CLASSES_REQUIRED_MESSAGE);

        FacultyProgressResponse progress = facultyProgressService.getProgress(jwt);
        assertThat(progress.reputation()).isEqualTo(65);
        assertThat(progress.coffeeClaimedForCurrentDay()).isFalse();
        assertThat(progress.coffeeOption()).isNull();
    }

    @Test
    void studentCannotClaimCoffee() {
        Jwt jwt = jwtWith("user_student_coffee");
        createSave(jwt, PlayerRole.STUDENT, "Alex Student", "22112345");

        assertThatThrownBy(() -> facultyProgressService.claimCoffee(jwt, FacultyCoffeeOption.CAPPUCCINO))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessage(FacultyProgressService.FACULTY_ONLY_MESSAGE);
        assertThat(facultyProgressRepository.findAll()).isEmpty();
    }

    @Test
    void nullOptionRejected() {
        Jwt jwt = jwtWith("user_faculty_coffee_null");
        createSave(jwt, PlayerRole.FACULTY, "Lail", "F-C06");
        completeBothClasses(jwt);

        assertThatThrownBy(() -> facultyProgressService.claimCoffee(jwt, null))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessage("Coffee option is required.");
    }

    @Test
    void coffeeBecomesAvailableAgainOnLaterDay() {
        Jwt jwt = jwtWith("user_faculty_coffee_nextday");
        Player player = createSave(jwt, PlayerRole.FACULTY, "Lail", "F-C07");
        completeBothClasses(jwt);

        FacultyProgressResponse day1 = facultyProgressService.claimCoffee(jwt, FacultyCoffeeOption.CAPPUCCINO);
        assertThat(day1.reputation()).isEqualTo(71);
        assertThat(day1.coffeeClaimedForCurrentDay()).isTrue();

        PlayerSave save = playerSaveRepository.findByPlayerId(player.getId()).orElseThrow();
        save.advanceToNextDay();
        playerSaveRepository.saveAndFlush(save);

        FacultyProgressResponse hydrated = facultyProgressService.getProgress(jwt);
        assertThat(hydrated.coffeeClaimedForCurrentDay()).isFalse();
        assertThat(hydrated.coffeeOption()).isNull();
        assertThat(hydrated.reputation()).isEqualTo(71);

        FacultyProgressResponse day2 = facultyProgressService.claimCoffee(
                jwt,
                FacultyCoffeeOption.CAPPUCCINO_COOKIE
        );
        assertThat(day2.reputation()).isEqualTo(73);
        assertThat(day2.coffeeClaimedForCurrentDay()).isTrue();
        assertThat(day2.coffeeOption()).isEqualTo("CAPPUCCINO_COOKIE");

        FacultyProgressResponse day2Again = facultyProgressService.claimCoffee(
                jwt,
                FacultyCoffeeOption.CAPPUCCINO_COOKIE_BROWNIE
        );
        assertThat(day2Again.reputation()).isEqualTo(73);
        assertThat(day2Again.coffeeOption()).isEqualTo("CAPPUCCINO_COOKIE");
    }

    @Test
    void repeatedSameDayClaimsRemainIdempotent() {
        Jwt jwt = jwtWith("user_faculty_coffee_race");
        createSave(jwt, PlayerRole.FACULTY, "Lail", "F-C08");
        completeBothClasses(jwt);

        FacultyProgressResponse first = facultyProgressService.claimCoffee(
                jwt,
                FacultyCoffeeOption.CAPPUCCINO_COOKIE_BROWNIE
        );
        FacultyProgressResponse second = facultyProgressService.claimCoffee(
                jwt,
                FacultyCoffeeOption.CAPPUCCINO_COOKIE_BROWNIE
        );
        FacultyProgressResponse third = facultyProgressService.claimCoffee(
                jwt,
                FacultyCoffeeOption.CAPPUCCINO
        );

        assertThat(first.reputation()).isEqualTo(73);
        assertThat(second.reputation()).isEqualTo(73);
        assertThat(third.reputation()).isEqualTo(73);
        assertThat(third.coffeeOption()).isEqualTo("CAPPUCCINO_COOKIE_BROWNIE");
    }

    @Test
    void coffeeDoesNotChangeClassroomProgressFlags() {
        Jwt jwt = jwtWith("user_faculty_coffee_preserve");
        createSave(jwt, PlayerRole.FACULTY, "Lail", "F-C09");
        completeBothClasses(jwt);

        FacultyProgressResponse before = facultyProgressService.getProgress(jwt);
        facultyProgressService.claimCoffee(jwt, FacultyCoffeeOption.CAPPUCCINO);
        FacultyProgressResponse after = facultyProgressService.getProgress(jwt);

        assertThat(after.icsCompleted()).isEqualTo(before.icsCompleted());
        assertThat(after.dmCompleted()).isEqualTo(before.dmCompleted());
        assertThat(after.classroomScanned()).isEqualTo(before.classroomScanned());
        assertThat(after.lectureCompleted()).isEqualTo(before.lectureCompleted());
        assertThat(after.computerUsed()).isEqualTo(before.computerUsed());
        assertThat(after.reputation()).isEqualTo(before.reputation() + 1);
    }

    private void completeBothClasses(Jwt jwt) {
        facultyTeachService.scanClassroom(jwt, "ICS");
        facultyTeachService.completeLecture(jwt);
        facultyTeachService.scanClassroom(jwt, "DM");
        facultyTeachService.completeLecture(jwt);
        FacultyProgressResponse progress = facultyProgressService.getProgress(jwt);
        assertThat(progress.reputation()).isEqualTo(70);
        assertThat(progress.icsCompleted()).isTrue();
        assertThat(progress.dmCompleted()).isTrue();
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
