package com.uiusimulator.player.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.uiusimulator.player.dto.ActivityResolveRequest;
import com.uiusimulator.player.dto.DayAdvanceRequest;
import com.uiusimulator.player.dto.DayFinalizeResponse;
import com.uiusimulator.player.dto.LibrarySelfStudyMilestoneRequest;
import com.uiusimulator.player.dto.LibrarySelfStudySessionResponse;
import com.uiusimulator.player.dto.PlayerSaveCreateRequest;
import com.uiusimulator.player.entity.Department;
import com.uiusimulator.player.entity.Player;
import com.uiusimulator.player.entity.PlayerRole;
import com.uiusimulator.player.entity.PlayerStats;
import com.uiusimulator.player.repository.DepartmentRepository;
import com.uiusimulator.player.repository.PlayerDayActivityRepository;
import com.uiusimulator.player.repository.PlayerRepository;
import com.uiusimulator.player.repository.PlayerStatsRepository;
import java.time.Instant;
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
        com.uiusimulator.assessment.service.AssessmentService.class,
        com.uiusimulator.assessment.service.CourseEnrollmentService.class,
        com.uiusimulator.assessment.service.SecureAssessmentRandom.class,
        com.uiusimulator.assessment.config.AssessmentCatalog.class,
        com.uiusimulator.assessment.config.CheatPolicy.class,
        PlayerService.class,
        PlayerSaveService.class,
        FacultyProgressService.class,
        PlayerActivityService.class,
        PlayerDayService.class,
        LibrarySelfStudyService.class,
        AttendIcsService.class,
        AttendIcsServiceTest.MutableClockConfig.class
})
class LibrarySelfStudyServiceTest {

    @Autowired
    private LibrarySelfStudyService librarySelfStudyService;

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

    @Autowired
    private AttendIcsServiceTest.MutableClock mutableClock;

    private Department cse;

    @BeforeEach
    void seedDepartments() {
        mutableClock.setInstant(Instant.parse("2026-01-01T10:00:00Z"));
        cse = departmentRepository.saveAndFlush(
                Department.createNew("CSE", "Computer Science and Engineering")
        );
    }

    @Test
    void start_createsOneInProgressAttemptWithoutReward() {
        Jwt jwt = jwtWith("user_self_start");
        createSave(jwt, PlayerRole.STUDENT);

        LibrarySelfStudySessionResponse response = librarySelfStudyService.start(jwt);

        assertThat(response.activityId()).isEqualTo("LIBRARY_SELF_STUDY");
        assertThat(response.status()).isEqualTo("IN_PROGRESS");
        assertThat(response.outcome()).isEqualTo("STARTED");
        assertThat(response.alreadyApplied()).isFalse();
        assertThat(response.alreadyCompleted()).isFalse();
        assertThat(response.auraDelta()).isZero();
        assertThat(response.reputationDelta()).isZero();
        assertThat(response.academicReputation()).isEqualTo(50);

        Player player = player(jwt);
        assertThat(playerDayActivityRepository.findByPlayer_IdAndActivityId(player.getId(), "LIBRARY_SELF_STUDY"))
                .isPresent();
    }

    @Test
    void start_duplicateIsIdempotent() {
        Jwt jwt = jwtWith("user_self_start_dup");
        createSave(jwt, PlayerRole.STUDENT);

        LibrarySelfStudySessionResponse first = librarySelfStudyService.start(jwt);
        LibrarySelfStudySessionResponse second = librarySelfStudyService.start(jwt);

        assertThat(first.alreadyApplied()).isFalse();
        assertThat(second.alreadyApplied()).isTrue();
        assertThat(second.alreadyCompleted()).isTrue();
        assertThat(second.appliedReputationDelta()).isZero();
        assertThat(second.academicReputation()).isEqualTo(50);

        Player player = player(jwt);
        assertThat(playerDayActivityRepository.findByPlayer_IdOrderByResolvedAtAsc(player.getId())
                .stream()
                .filter(a -> "LIBRARY_SELF_STUDY".equals(a.getActivityId()))
                .count()).isEqualTo(1);
    }

    @Test
    void faculty_cannotStart() {
        Jwt jwt = jwtWith("user_self_faculty");
        createSave(jwt, PlayerRole.FACULTY);

        assertThatThrownBy(() -> librarySelfStudyService.start(jwt))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessageContaining("Only students");

        assertThat(playerDayActivityRepository.findByPlayer_IdAndActivityId(player(jwt).getId(), "LIBRARY_SELF_STUDY"))
                .isEmpty();
    }

    @Test
    void milestones_awardPlusOneEachAndCompleteAtNinety() {
        Jwt jwt = jwtWith("user_self_milestones");
        createSave(jwt, PlayerRole.STUDENT);
        librarySelfStudyService.start(jwt);
        librarySelfStudyService.resume(jwt);

        mutableClock.advanceSeconds(30);
        LibrarySelfStudySessionResponse at30 = librarySelfStudyService.claimMilestone(
                jwt, new LibrarySelfStudyMilestoneRequest(30));
        assertThat(at30.appliedReputationDelta()).isEqualTo(1);
        assertThat(at30.academicReputation()).isEqualTo(51);
        assertThat(at30.aura()).isEqualTo(50);
        assertThat(at30.status()).isEqualTo("IN_PROGRESS");

        mutableClock.advanceSeconds(30);
        LibrarySelfStudySessionResponse at60 = librarySelfStudyService.claimMilestone(
                jwt, new LibrarySelfStudyMilestoneRequest(60));
        assertThat(at60.appliedReputationDelta()).isEqualTo(1);
        assertThat(at60.academicReputation()).isEqualTo(52);

        mutableClock.advanceSeconds(30);
        LibrarySelfStudySessionResponse at90 = librarySelfStudyService.claimMilestone(
                jwt, new LibrarySelfStudyMilestoneRequest(90));
        assertThat(at90.appliedReputationDelta()).isEqualTo(1);
        assertThat(at90.academicReputation()).isEqualTo(53);
        assertThat(at90.status()).isEqualTo("COMPLETED");
        assertThat(at90.alreadyCompleted()).isTrue();
        assertThat(stats(jwt).getAura()).isEqualTo(50);
    }

    @Test
    void duplicateMilestone_doesNotReaward() {
        Jwt jwt = jwtWith("user_self_dup_ms");
        createSave(jwt, PlayerRole.STUDENT);
        librarySelfStudyService.start(jwt);
        librarySelfStudyService.resume(jwt);
        mutableClock.advanceSeconds(30);

        LibrarySelfStudySessionResponse first = librarySelfStudyService.claimMilestone(
                jwt, new LibrarySelfStudyMilestoneRequest(30));
        LibrarySelfStudySessionResponse second = librarySelfStudyService.claimMilestone(
                jwt, new LibrarySelfStudyMilestoneRequest(30));

        assertThat(first.appliedReputationDelta()).isEqualTo(1);
        assertThat(second.alreadyApplied()).isTrue();
        assertThat(second.appliedReputationDelta()).isZero();
        assertThat(second.academicReputation()).isEqualTo(51);
        assertThat(stats(jwt).getAcademicReputation()).isEqualTo(51);
        assertThat(stats(jwt).getAura()).isEqualTo(50);
    }

    @Test
    void abandon_completesTodayWithoutExtraReward() {
        Jwt jwt = jwtWith("user_self_abandon");
        createSave(jwt, PlayerRole.STUDENT);
        librarySelfStudyService.start(jwt);
        librarySelfStudyService.resume(jwt);
        mutableClock.advanceSeconds(10);

        LibrarySelfStudySessionResponse left = librarySelfStudyService.abandon(jwt);

        assertThat(left.status()).isEqualTo("COMPLETED");
        assertThat(left.outcome()).isEqualTo("ABANDONED");
        assertThat(left.alreadyCompleted()).isTrue();
        assertThat(left.appliedReputationDelta()).isZero();
        assertThat(left.academicReputation()).isEqualTo(50);
        assertThat(stats(jwt).getAura()).isEqualTo(50);

        LibrarySelfStudySessionResponse restart = librarySelfStudyService.start(jwt);
        assertThat(restart.alreadyCompleted()).isTrue();
        assertThat(restart.alreadyApplied()).isTrue();
        assertThat(restart.appliedReputationDelta()).isZero();
        assertThat(stats(jwt).getAcademicReputation()).isEqualTo(50);

        assertThatThrownBy(() -> librarySelfStudyService.resume(jwt))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessageContaining("already completed");
    }

    @Test
    void abandon_afterMilestone_keepsEarnedReputation() {
        Jwt jwt = jwtWith("user_self_abandon_ms");
        createSave(jwt, PlayerRole.STUDENT);
        librarySelfStudyService.start(jwt);
        librarySelfStudyService.resume(jwt);
        mutableClock.advanceSeconds(30);
        librarySelfStudyService.claimMilestone(jwt, new LibrarySelfStudyMilestoneRequest(30));

        LibrarySelfStudySessionResponse left = librarySelfStudyService.abandon(jwt);

        assertThat(left.status()).isEqualTo("COMPLETED");
        assertThat(left.outcome()).isEqualTo("ABANDONED");
        assertThat(left.reputationDelta()).isEqualTo(1);
        assertThat(left.academicReputation()).isEqualTo(51);
        assertThat(stats(jwt).getAura()).isEqualTo(50);

        LibrarySelfStudySessionResponse restart = librarySelfStudyService.start(jwt);
        assertThat(restart.alreadyCompleted()).isTrue();
        assertThat(restart.academicReputation()).isEqualTo(51);
    }

    @Test
    void completedStart_doesNotReaward() {
        Jwt jwt = jwtWith("user_self_done");
        createSave(jwt, PlayerRole.STUDENT);
        completeFullSession(jwt);

        LibrarySelfStudySessionResponse restart = librarySelfStudyService.start(jwt);
        assertThat(restart.alreadyCompleted()).isTrue();
        assertThat(restart.alreadyApplied()).isTrue();
        assertThat(restart.appliedReputationDelta()).isZero();
        assertThat(restart.academicReputation()).isEqualTo(53);
        assertThat(stats(jwt).getAura()).isEqualTo(50);
    }

    @Test
    void pausedTime_doesNotCountTowardMilestone() {
        Jwt jwt = jwtWith("user_self_pause");
        createSave(jwt, PlayerRole.STUDENT);
        librarySelfStudyService.start(jwt);
        librarySelfStudyService.resume(jwt);
        mutableClock.advanceSeconds(20);
        librarySelfStudyService.pause(jwt);
        mutableClock.advanceSeconds(40);
        librarySelfStudyService.resume(jwt);

        assertThatThrownBy(() -> librarySelfStudyService.claimMilestone(
                jwt, new LibrarySelfStudyMilestoneRequest(30)))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessageContaining("not yet earned");

        mutableClock.advanceSeconds(10);
        LibrarySelfStudySessionResponse at30 = librarySelfStudyService.claimMilestone(
                jwt, new LibrarySelfStudyMilestoneRequest(30));
        assertThat(at30.appliedReputationDelta()).isEqualTo(1);
        assertThat(at30.academicReputation()).isEqualTo(51);
    }

    @Test
    void ignoringSelfStudy_hasNoEndDayPenalty() {
        Jwt jwt = jwtWith("user_self_ignore");
        createSave(jwt, PlayerRole.STUDENT);
        playerActivityService.resolveActivity(jwt, new ActivityResolveRequest("BREAKFAST", "RICE"));

        DayFinalizeResponse summary = playerDayService.finalizeCurrentDay(jwt);

        assertThat(summary.activities()).noneMatch(a -> "LIBRARY_SELF_STUDY".equals(a.activityId()));
        assertThat(stats(jwt).getAcademicReputation()).isEqualTo(38);
        assertThat(stats(jwt).getAura()).isEqualTo(53);
        assertThat(playerDayActivityRepository.findByPlayer_IdAndActivityId(player(jwt).getId(), "LIBRARY_SELF_STUDY"))
                .isEmpty();
    }

    @Test
    void incompleteSelfStudy_isOmittedFromSummary() {
        Jwt jwt = jwtWith("user_self_incomplete");
        createSave(jwt, PlayerRole.STUDENT);
        playerActivityService.resolveActivity(jwt, new ActivityResolveRequest("BREAKFAST", "RICE"));
        librarySelfStudyService.start(jwt);

        DayFinalizeResponse summary = playerDayService.finalizeCurrentDay(jwt);

        assertThat(summary.activities()).noneMatch(a -> "LIBRARY_SELF_STUDY".equals(a.activityId()));
        var row = playerDayActivityRepository
                .findByPlayer_IdAndActivityId(player(jwt).getId(), "LIBRARY_SELF_STUDY")
                .orElseThrow();
        assertThat(row.getStatus().name()).isEqualTo("IN_PROGRESS");
        assertThat(row.getReputationDelta()).isZero();
    }

    @Test
    void completedSelfStudy_appearsAsBonus() {
        Jwt jwt = jwtWith("user_self_summary");
        createSave(jwt, PlayerRole.STUDENT);
        playerActivityService.resolveActivity(jwt, new ActivityResolveRequest("BREAKFAST", "RICE"));
        completeFullSession(jwt);

        DayFinalizeResponse summary = playerDayService.finalizeCurrentDay(jwt);

        assertThat(summary.activities()).anySatisfy(activity -> {
            assertThat(activity.activityId()).isEqualTo("LIBRARY_SELF_STUDY");
            assertThat(activity.status()).isEqualTo("COMPLETED");
            assertThat(activity.auraDelta()).isZero();
            assertThat(activity.academicReputationDelta()).isEqualTo(3);
        });
        assertThat(stats(jwt).getAura()).isEqualTo(53);
        assertThat(stats(jwt).getAcademicReputation()).isEqualTo(41);
    }

    @Test
    void nextDay_resetsSelfStudyAvailability() {
        Jwt jwt = jwtWith("user_self_nextday");
        createSave(jwt, PlayerRole.STUDENT);
        playerActivityService.resolveActivity(jwt, new ActivityResolveRequest("BREAKFAST", "RICE"));
        completeFullSession(jwt);

        playerDayService.finalizeCurrentDay(jwt);
        playerDayService.advanceDay(jwt, new DayAdvanceRequest(1, 1));

        assertThat(playerDayActivityRepository.findByPlayer_IdAndActivityId(player(jwt).getId(), "LIBRARY_SELF_STUDY"))
                .isEmpty();

        LibrarySelfStudySessionResponse restarted = librarySelfStudyService.start(jwt);
        assertThat(restarted.alreadyApplied()).isFalse();
        assertThat(restarted.alreadyCompleted()).isFalse();
        assertThat(restarted.dayNumber()).isEqualTo(2);
        assertThat(stats(jwt).getAcademicReputation()).isEqualTo(41);
    }

    @Test
    void nextDay_resetsAfterAbandon() {
        Jwt jwt = jwtWith("user_self_nextday_abandon");
        createSave(jwt, PlayerRole.STUDENT);
        playerActivityService.resolveActivity(jwt, new ActivityResolveRequest("BREAKFAST", "RICE"));
        librarySelfStudyService.start(jwt);
        librarySelfStudyService.abandon(jwt);

        playerDayService.finalizeCurrentDay(jwt);
        playerDayService.advanceDay(jwt, new DayAdvanceRequest(1, 1));

        LibrarySelfStudySessionResponse restarted = librarySelfStudyService.start(jwt);
        assertThat(restarted.alreadyApplied()).isFalse();
        assertThat(restarted.alreadyCompleted()).isFalse();
        assertThat(restarted.dayNumber()).isEqualTo(2);
    }

    @Test
    void newGame_clearsSelfStudyState() {
        Jwt jwt = jwtWith("user_self_newgame");
        createSave(jwt, PlayerRole.STUDENT);
        completeFullSession(jwt);

        playerSaveService.deleteSave(jwt);

        assertThat(playerDayActivityRepository.findByPlayer_IdAndActivityId(player(jwt).getId(), "LIBRARY_SELF_STUDY"))
                .isEmpty();
        assertThat(stats(jwt).getAcademicReputation()).isEqualTo(50);
    }

    @Test
    void reputationStaysWithinBounds() {
        Jwt jwt = jwtWith("user_self_cap");
        createSave(jwt, PlayerRole.STUDENT);
        stats(jwt).modifyStats(0, 49);
        playerStatsRepository.saveAndFlush(stats(jwt));

        librarySelfStudyService.start(jwt);
        librarySelfStudyService.resume(jwt);
        mutableClock.advanceSeconds(30);
        LibrarySelfStudySessionResponse at30 = librarySelfStudyService.claimMilestone(
                jwt, new LibrarySelfStudyMilestoneRequest(30));

        assertThat(at30.requestedReputationDelta()).isEqualTo(1);
        assertThat(at30.appliedReputationDelta()).isEqualTo(1);
        assertThat(at30.academicReputation()).isEqualTo(100);
        mutableClock.advanceSeconds(30);
        LibrarySelfStudySessionResponse at60 = librarySelfStudyService.claimMilestone(
                jwt, new LibrarySelfStudyMilestoneRequest(60));
        assertThat(at60.appliedReputationDelta()).isZero();
        assertThat(at60.academicReputation()).isEqualTo(100);
        assertThat(stats(jwt).getAura()).isEqualTo(50);
    }

    private void completeFullSession(Jwt jwt) {
        librarySelfStudyService.start(jwt);
        librarySelfStudyService.resume(jwt);
        mutableClock.advanceSeconds(30);
        librarySelfStudyService.claimMilestone(jwt, new LibrarySelfStudyMilestoneRequest(30));
        mutableClock.advanceSeconds(30);
        librarySelfStudyService.claimMilestone(jwt, new LibrarySelfStudyMilestoneRequest(60));
        mutableClock.advanceSeconds(30);
        librarySelfStudyService.claimMilestone(jwt, new LibrarySelfStudyMilestoneRequest(90));
    }

    private void createSave(Jwt jwt, PlayerRole role) {
        playerSaveService.createSave(
                jwt,
                new PlayerSaveCreateRequest(role, "Self Study Student", cse.getId(), "22110001")
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
