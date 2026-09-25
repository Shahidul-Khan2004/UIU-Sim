package com.uiusimulator.player.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.uiusimulator.player.dto.ActivityResolveRequest;
import com.uiusimulator.player.dto.DayAdvanceRequest;
import com.uiusimulator.player.dto.DayFinalizeResponse;
import com.uiusimulator.player.dto.LibraryStudyCompleteRequest;
import com.uiusimulator.player.dto.LibraryStudyResponse;
import com.uiusimulator.player.dto.PlayerSaveCreateRequest;
import com.uiusimulator.player.entity.Department;
import com.uiusimulator.player.entity.LibraryStudyDefinition;
import com.uiusimulator.player.entity.Player;
import com.uiusimulator.player.entity.PlayerRole;
import com.uiusimulator.player.entity.PlayerStats;
import com.uiusimulator.player.repository.DepartmentRepository;
import com.uiusimulator.player.repository.PlayerDayActivityRepository;
import com.uiusimulator.player.repository.PlayerRepository;
import com.uiusimulator.player.repository.PlayerStatsRepository;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.CsvSource;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.orm.jpa.DataJpaTest;
import org.springframework.context.annotation.Import;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.test.context.ActiveProfiles;

@DataJpaTest
@ActiveProfiles("test")
@Import({
        PlayerService.class,
        PlayerSaveService.class,
        PlayerActivityService.class,
        PlayerDayService.class,
        LibraryStudyService.class,
        AttendIcsService.class,
        com.uiusimulator.config.TimeConfig.class
})
class LibraryStudyServiceTest {

    @Autowired
    private LibraryStudyService libraryStudyService;

    @Autowired
    private PlayerActivityService playerActivityService;

    @Autowired
    private PlayerDayService playerDayService;

    @Autowired
    private PlayerSaveService playerSaveService;

    @Autowired
    private PlayerRepository playerRepository;

    @Autowired
    private PlayerStatsRepository playerStatsRepository;

    @Autowired
    private PlayerDayActivityRepository playerDayActivityRepository;

    @Autowired
    private DepartmentRepository departmentRepository;

    private Department cse;

    @BeforeEach
    void seedDepartments() {
        cse = departmentRepository.saveAndFlush(
                Department.createNew("CSE", "Computer Science and Engineering")
        );
    }

    @Test
    void start_createsOneInProgressAttemptWithoutReward() {
        Jwt jwt = jwtWith("user_lib_start");
        createSave(jwt);

        LibraryStudyResponse response = libraryStudyService.start(jwt);

        assertThat(response.activityId()).isEqualTo("LIBRARY_STUDY");
        assertThat(response.status()).isEqualTo("IN_PROGRESS");
        assertThat(response.outcome()).isEqualTo("STARTED");
        assertThat(response.alreadyStarted()).isFalse();
        assertThat(response.alreadyCompleted()).isFalse();
        assertThat(response.auraDelta()).isZero();
        assertThat(response.reputationDelta()).isZero();
        assertThat(response.score()).isNull();
        assertThat(response.aura()).isEqualTo(50);
        assertThat(response.academicReputation()).isEqualTo(50);

        Player player = player(jwt);
        assertThat(playerDayActivityRepository.findByPlayer_IdAndActivityId(player.getId(), "LIBRARY_STUDY")).isPresent();
    }

    @Test
    void start_duplicateIsIdempotent() {
        Jwt jwt = jwtWith("user_lib_start_dup");
        createSave(jwt);

        LibraryStudyResponse first = libraryStudyService.start(jwt);
        LibraryStudyResponse second = libraryStudyService.start(jwt);

        assertThat(first.alreadyStarted()).isFalse();
        assertThat(second.alreadyStarted()).isTrue();
        assertThat(second.alreadyCompleted()).isFalse();
        assertThat(second.status()).isEqualTo("IN_PROGRESS");
        assertThat(second.academicReputation()).isEqualTo(50);

        Player player = player(jwt);
        assertThat(playerDayActivityRepository.findByPlayer_IdOrderByResolvedAtAsc(player.getId())
                .stream()
                .filter(a -> "LIBRARY_STUDY".equals(a.getActivityId()))
                .count()).isEqualTo(1);
    }

    @Test
    void complete_withoutStart_isRejected() {
        Jwt jwt = jwtWith("user_lib_nostart");
        createSave(jwt);

        assertThatThrownBy(() -> libraryStudyService.complete(jwt, new LibraryStudyCompleteRequest(75)))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessageContaining("has not been started");

        assertThat(stats(jwt).getAcademicReputation()).isEqualTo(50);
        assertThat(stats(jwt).getAura()).isEqualTo(50);
    }

    @ParameterizedTest
    @CsvSource({
            "0, 0",
            "19, 0",
            "20, 1",
            "25, 1",
            "39, 1",
            "40, 2",
            "50, 2",
            "59, 2",
            "60, 3",
            "73, 3",
            "75, 3",
            "79, 3",
            "80, 4",
            "90, 4",
            "99, 4",
            "100, 5"
    })
    void complete_mapsScoreToReputation(int score, int expectedReward) {
        Jwt jwt = jwtWith("user_lib_score_" + score);
        createSave(jwt);
        libraryStudyService.start(jwt);

        LibraryStudyResponse response = libraryStudyService.complete(jwt, new LibraryStudyCompleteRequest(score));

        assertThat(LibraryStudyDefinition.reputationForScore(score)).isEqualTo(expectedReward);
        assertThat(response.score()).isEqualTo(score);
        assertThat(response.requestedReputationDelta()).isEqualTo(expectedReward);
        assertThat(response.reputationDelta()).isEqualTo(expectedReward);
        assertThat(response.appliedReputationDelta()).isEqualTo(expectedReward);
        assertThat(response.auraDelta()).isZero();
        assertThat(response.alreadyCompleted()).isFalse();
        assertThat(response.status()).isEqualTo("COMPLETED");
        assertThat(response.academicReputation()).isEqualTo(50 + expectedReward);
        assertThat(response.aura()).isEqualTo(50);
        assertThat(expectedReward).isLessThanOrEqualTo(LibraryStudyDefinition.MAX_REPUTATION_REWARD);
        assertThat(stats(jwt).getAura()).isEqualTo(50);
    }

    @Test
    void complete_duplicateDoesNotReaward() {
        Jwt jwt = jwtWith("user_lib_dup_complete");
        createSave(jwt);
        libraryStudyService.start(jwt);
        LibraryStudyResponse first = libraryStudyService.complete(jwt, new LibraryStudyCompleteRequest(75));
        LibraryStudyResponse second = libraryStudyService.complete(jwt, new LibraryStudyCompleteRequest(100));

        assertThat(first.academicReputation()).isEqualTo(53);
        assertThat(second.alreadyCompleted()).isTrue();
        assertThat(second.appliedReputationDelta()).isZero();
        assertThat(second.score()).isEqualTo(75);
        assertThat(second.reputationDelta()).isEqualTo(3);
        assertThat(second.academicReputation()).isEqualTo(53);
        assertThat(stats(jwt).getAura()).isEqualTo(50);

        LibraryStudyResponse restart = libraryStudyService.start(jwt);
        assertThat(restart.alreadyCompleted()).isTrue();
        assertThat(restart.academicReputation()).isEqualTo(53);
    }

    @Test
    void complete_reputationStaysWithinBounds() {
        Jwt jwt = jwtWith("user_lib_cap");
        createSave(jwt);
        stats(jwt).modifyStats(0, 49);
        playerStatsRepository.saveAndFlush(stats(jwt));
        libraryStudyService.start(jwt);

        LibraryStudyResponse response = libraryStudyService.complete(jwt, new LibraryStudyCompleteRequest(100));

        assertThat(response.requestedReputationDelta()).isEqualTo(5);
        assertThat(response.appliedReputationDelta()).isEqualTo(1);
        assertThat(response.reputationDelta()).isEqualTo(1);
        assertThat(response.academicReputation()).isEqualTo(100);
        assertThat(stats(jwt).getAcademicReputation()).isEqualTo(100);
        assertThat(playerDayActivityRepository.findByPlayer_IdAndActivityId(player(jwt).getId(), "LIBRARY_STUDY")
                .orElseThrow().getReputationDelta()).isEqualTo(1);
        assertThat(stats(jwt).getAura()).isEqualTo(50);

        playerActivityService.resolveActivity(jwt, new ActivityResolveRequest("BREAKFAST", "POROTTA_WAIT"));
        DayFinalizeResponse summary = playerDayService.finalizeCurrentDay(jwt);
        assertThat(summary.activities())
                .filteredOn(a -> "LIBRARY_STUDY".equals(a.activityId()))
                .first()
                .satisfies(a -> assertThat(a.academicReputationDelta()).isEqualTo(1));
    }

    @Test
    void ignoringLibraryStudy_hasNoEndDayPenalty() {
        Jwt jwt = jwtWith("user_lib_ignore");
        createSave(jwt);
        playerActivityService.resolveActivity(jwt, new ActivityResolveRequest("BREAKFAST", "RICE"));

        DayFinalizeResponse summary = playerDayService.finalizeCurrentDay(jwt);

        assertThat(summary.activities()).noneMatch(a -> "LIBRARY_STUDY".equals(a.activityId()));
        assertThat(summary.totalAcademicReputationDelta()).isEqualTo(-12);
        assertThat(stats(jwt).getAcademicReputation()).isEqualTo(38);
        assertThat(stats(jwt).getAura()).isEqualTo(53);
        assertThat(playerDayActivityRepository.findByPlayer_IdAndActivityId(player(jwt).getId(), "LIBRARY_STUDY")).isEmpty();
    }

    @Test
    void incompleteStudy_hasNoEndDayPenaltyAndIsOmittedFromSummary() {
        Jwt jwt = jwtWith("user_lib_incomplete");
        createSave(jwt);
        playerActivityService.resolveActivity(jwt, new ActivityResolveRequest("BREAKFAST", "RICE"));
        libraryStudyService.start(jwt);

        DayFinalizeResponse summary = playerDayService.finalizeCurrentDay(jwt);

        assertThat(summary.activities()).noneMatch(a -> "LIBRARY_STUDY".equals(a.activityId()));
        assertThat(summary.totalAcademicReputationDelta()).isEqualTo(-12);
        assertThat(stats(jwt).getAcademicReputation()).isEqualTo(38);
        assertThat(stats(jwt).getAura()).isEqualTo(53);

        var row = playerDayActivityRepository.findByPlayer_IdAndActivityId(player(jwt).getId(), "LIBRARY_STUDY").orElseThrow();
        assertThat(row.getStatus().name()).isEqualTo("IN_PROGRESS");
        assertThat(row.getReputationDelta()).isZero();
    }

    @Test
    void completedStudy_appearsAsBonusWithoutAffectingBreakfast() {
        Jwt jwt = jwtWith("user_lib_summary");
        createSave(jwt);
        playerActivityService.resolveActivity(jwt, new ActivityResolveRequest("BREAKFAST", "RICE"));
        libraryStudyService.start(jwt);
        libraryStudyService.complete(jwt, new LibraryStudyCompleteRequest(90));

        DayFinalizeResponse summary = playerDayService.finalizeCurrentDay(jwt);

        assertThat(summary.activities()).anySatisfy(activity -> {
            assertThat(activity.activityId()).isEqualTo("LIBRARY_STUDY");
            assertThat(activity.status()).isEqualTo("COMPLETED");
            assertThat(activity.auraDelta()).isZero();
            assertThat(activity.academicReputationDelta()).isEqualTo(4);
        });
        assertThat(summary.activities()).anySatisfy(activity -> {
            assertThat(activity.activityId()).isEqualTo("BREAKFAST");
            assertThat(activity.outcome()).isEqualTo("RICE");
            assertThat(activity.auraDelta()).isEqualTo(3);
        });
        assertThat(stats(jwt).getAura()).isEqualTo(53);
        assertThat(stats(jwt).getAcademicReputation()).isEqualTo(42);
    }

    @Test
    void nextDay_resetsLibraryStudyAvailability() {
        Jwt jwt = jwtWith("user_lib_nextday");
        createSave(jwt);
        playerActivityService.resolveActivity(jwt, new ActivityResolveRequest("BREAKFAST", "RICE"));
        libraryStudyService.start(jwt);
        libraryStudyService.complete(jwt, new LibraryStudyCompleteRequest(50));

        playerDayService.finalizeCurrentDay(jwt);
        playerDayService.advanceDay(jwt, new DayAdvanceRequest(1, 1));

        assertThat(playerDayActivityRepository.findByPlayer_IdAndActivityId(player(jwt).getId(), "LIBRARY_STUDY")).isEmpty();
        assertThat(playerDayActivityRepository.findByPlayer_IdAndActivityId(player(jwt).getId(), "BREAKFAST")).isEmpty();

        LibraryStudyResponse restarted = libraryStudyService.start(jwt);
        assertThat(restarted.alreadyStarted()).isFalse();
        assertThat(restarted.alreadyCompleted()).isFalse();
        assertThat(restarted.dayNumber()).isEqualTo(2);
        assertThat(stats(jwt).getAcademicReputation()).isEqualTo(40);
    }

    @Test
    void newGame_clearsLibraryStudyState() {
        Jwt jwt = jwtWith("user_lib_newgame");
        createSave(jwt);
        libraryStudyService.start(jwt);
        libraryStudyService.complete(jwt, new LibraryStudyCompleteRequest(100));

        playerSaveService.deleteSave(jwt);

        assertThat(playerDayActivityRepository.findByPlayer_IdAndActivityId(player(jwt).getId(), "LIBRARY_STUDY")).isEmpty();
        assertThat(stats(jwt).getAcademicReputation()).isEqualTo(50);
    }

    @Test
    void scoreMapping_rejectsOutOfRange() {
        assertThatThrownBy(() -> LibraryStudyDefinition.reputationForScore(-1))
                .isInstanceOf(IllegalArgumentException.class);
        assertThatThrownBy(() -> LibraryStudyDefinition.reputationForScore(101))
                .isInstanceOf(IllegalArgumentException.class);
    }

    private void createSave(Jwt jwt) {
        playerSaveService.createSave(
                jwt,
                new PlayerSaveCreateRequest(PlayerRole.STUDENT, "Library Student", cse.getId(), "22110001")
        );
    }

    private Player player(Jwt jwt) {
        return playerRepository.findByClerkUserId(jwt.getSubject()).orElseThrow();
    }

    private PlayerStats stats(Jwt jwt) {
        return playerStatsRepository.findByPlayerId(player(jwt).getId()).orElseThrow();
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
