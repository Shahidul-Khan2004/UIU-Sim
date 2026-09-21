package com.uiusimulator.player.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.uiusimulator.player.dto.AttendIcsMilestoneRequest;
import com.uiusimulator.player.dto.AttendIcsSessionResponse;
import com.uiusimulator.player.dto.DayAdvanceRequest;
import com.uiusimulator.player.dto.DayFinalizeResponse;
import com.uiusimulator.player.dto.PlayerSaveCreateRequest;
import com.uiusimulator.player.entity.AttendIcsDefinition;
import com.uiusimulator.player.entity.Department;
import com.uiusimulator.player.entity.Player;
import com.uiusimulator.player.entity.PlayerRole;
import com.uiusimulator.player.repository.DepartmentRepository;
import com.uiusimulator.player.repository.PlayerDayActivityRepository;
import com.uiusimulator.player.repository.PlayerRepository;
import com.uiusimulator.player.repository.PlayerStatsRepository;
import java.time.Clock;
import java.time.Instant;
import java.time.ZoneOffset;
import java.util.concurrent.atomic.AtomicReference;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.orm.jpa.DataJpaTest;
import org.springframework.boot.test.context.TestConfiguration;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Import;
import org.springframework.context.annotation.Primary;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.test.context.ActiveProfiles;

@DataJpaTest
@ActiveProfiles("test")
@Import({
        PlayerService.class,
        PlayerSaveService.class,
        PlayerActivityService.class,
        PlayerDayService.class,
        AttendIcsService.class,
        AttendIcsServiceTest.MutableClockConfig.class
})
class AttendIcsServiceTest {

    @Autowired
    private AttendIcsService attendIcsService;

    @Autowired
    private PlayerDayService playerDayService;

    @Autowired
    private PlayerActivityService playerActivityService;

    @Autowired
    private PlayerSaveService playerSaveService;

    @Autowired
    private PlayerService playerService;

    @Autowired
    private PlayerRepository playerRepository;

    @Autowired
    private PlayerStatsRepository playerStatsRepository;

    @Autowired
    private PlayerDayActivityRepository playerDayActivityRepository;

    @Autowired
    private DepartmentRepository departmentRepository;

    @Autowired
    private MutableClock mutableClock;

    private Department cse;
    private Department bba;

    @BeforeEach
    void setUp() {
        mutableClock.setInstant(Instant.parse("2026-01-01T10:00:00Z"));
        cse = departmentRepository.saveAndFlush(Department.createNew("CSE", "Computer Science and Engineering"));
        bba = departmentRepository.saveAndFlush(Department.createNew("BBA", "Bachelor of Business Administration"));
    }

    @Test
    void milestone30_awardsPlus4Reputation() {
        Jwt jwt = jwtWith("ics_m30");
        createCseStudent(jwt);
        attendIcsService.startLecture(jwt);
        mutableClock.advanceSeconds(30);

        AttendIcsSessionResponse response = attendIcsService.claimMilestone(jwt, new AttendIcsMilestoneRequest(30));

        assertThat(response.milestoneSeconds()).isEqualTo(30);
        assertThat(response.requestedReputationDelta()).isEqualTo(4);
        assertThat(response.appliedReputationDelta()).isEqualTo(4);
        assertThat(response.reputationDelta()).isEqualTo(4);
        assertThat(response.auraDelta()).isEqualTo(0);
        assertThat(response.academicReputation()).isEqualTo(54);
        assertThat(response.status()).isEqualTo("IN_PROGRESS");
    }

    @Test
    void milestone60_cumulativePlus8() {
        Jwt jwt = jwtWith("ics_m60");
        createCseStudent(jwt);
        attendIcsService.startLecture(jwt);
        claimThrough(jwt, 60);

        var activity = playerDayActivityRepository.findByPlayer_IdAndActivityId(
                playerRepository.findByClerkUserId("ics_m60").orElseThrow().getId(),
                AttendIcsDefinition.ACTIVITY_ID
        ).orElseThrow();

        assertThat(activity.getMilestoneSeconds()).isEqualTo(60);
        assertThat(activity.getReputationDelta()).isEqualTo(8);
        assertThat(activity.getAuraDelta()).isEqualTo(0);
        assertThat(playerStatsRepository.findByPlayerId(activity.getPlayer().getId()).orElseThrow()
                .getAcademicReputation()).isEqualTo(58);
    }

    @Test
    void milestone90_cumulativePlus12_andCompletes() {
        Jwt jwt = jwtWith("ics_m90");
        createCseStudent(jwt);
        attendIcsService.startLecture(jwt);
        claimThrough(jwt, 90);

        var activity = playerDayActivityRepository.findByPlayer_IdAndActivityId(
                playerRepository.findByClerkUserId("ics_m90").orElseThrow().getId(),
                AttendIcsDefinition.ACTIVITY_ID
        ).orElseThrow();

        assertThat(activity.getMilestoneSeconds()).isEqualTo(90);
        assertThat(activity.getReputationDelta()).isEqualTo(12);
        assertThat(activity.getAuraDelta()).isEqualTo(0);
        assertThat(activity.getStatus().name()).isEqualTo("COMPLETED");
        assertThat(activity.getOutcome()).isEqualTo("COMPLETED");
        assertThat(playerStatsRepository.findByPlayerId(activity.getPlayer().getId()).orElseThrow()
                .getAcademicReputation()).isEqualTo(62);
    }

    @Test
    void fullAttendance_hasNoAuraReward() {
        Jwt jwt = jwtWith("ics_no_aura");
        createCseStudent(jwt);
        attendIcsService.startLecture(jwt);
        claimThrough(jwt, 90);

        assertThat(playerStatsRepository.findByPlayerId(
                playerRepository.findByClerkUserId("ics_no_aura").orElseThrow().getId()
        ).orElseThrow().getAura()).isEqualTo(50);
    }

    @Test
    void leaveAt0s_netMinus5() {
        Jwt jwt = jwtWith("ics_leave0");
        createCseStudent(jwt);
        attendIcsService.startLecture(jwt);

        AttendIcsSessionResponse response = attendIcsService.leaveEarly(jwt);

        assertThat(response.outcome()).isEqualTo("LEFT_EARLY");
        assertThat(response.reputationDelta()).isEqualTo(-5);
        assertThat(response.academicReputation()).isEqualTo(45);
        assertThat(response.auraDelta()).isEqualTo(0);
    }

    @Test
    void leaveAt30s_netMinus1() {
        Jwt jwt = jwtWith("ics_leave30");
        createCseStudent(jwt);
        attendIcsService.startLecture(jwt);
        claimThrough(jwt, 30);

        AttendIcsSessionResponse response = attendIcsService.leaveEarly(jwt);

        assertThat(response.outcome()).isEqualTo("LEFT_EARLY");
        assertThat(response.reputationDelta()).isEqualTo(-1);
        assertThat(response.academicReputation()).isEqualTo(49);
    }

    @Test
    void leaveAt60s_netPlus3() {
        Jwt jwt = jwtWith("ics_leave60");
        createCseStudent(jwt);
        attendIcsService.startLecture(jwt);
        claimThrough(jwt, 60);

        AttendIcsSessionResponse response = attendIcsService.leaveEarly(jwt);

        assertThat(response.outcome()).isEqualTo("LEFT_EARLY");
        assertThat(response.reputationDelta()).isEqualTo(3);
        assertThat(response.academicReputation()).isEqualTo(53);
    }

    @Test
    void leaveEarly_afterCompleted_rejectsWithoutChangingStats() {
        Jwt jwt = jwtWith("ics_full_no_leave");
        createCseStudent(jwt);
        attendIcsService.startLecture(jwt);
        claimThrough(jwt, 90);

        assertThatThrownBy(() -> attendIcsService.leaveEarly(jwt))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessageContaining("already completed");

        DayFinalizeResponse summary = playerDayService.finalizeCurrentDay(jwt);
        assertThat(summary.activities())
                .filteredOn(a -> AttendIcsDefinition.ACTIVITY_ID.equals(a.activityId()))
                .first()
                .satisfies(a -> {
                    assertThat(a.outcome()).isEqualTo("COMPLETED");
                    assertThat(a.academicReputationDelta()).isEqualTo(12);
                });
        assertThat(summary.academicReputation()).isEqualTo(52);
    }

    @Test
    void earlyLeave_cannotResumeOrFarm() {
        Jwt jwt = jwtWith("ics_no_farm");
        createCseStudent(jwt);
        attendIcsService.startLecture(jwt);
        claimThrough(jwt, 30);
        attendIcsService.leaveEarly(jwt);

        AttendIcsSessionResponse restarted = attendIcsService.startLecture(jwt);
        assertThat(restarted.alreadyApplied()).isTrue();
        assertThat(restarted.outcome()).isEqualTo("LEFT_EARLY");

        assertThatThrownBy(() -> attendIcsService.claimMilestone(jwt, new AttendIcsMilestoneRequest(60)))
                .isInstanceOf(IllegalArgumentException.class);

        AttendIcsSessionResponse leaveAgain = attendIcsService.leaveEarly(jwt);
        assertThat(leaveAgain.alreadyApplied()).isTrue();
        assertThat(leaveAgain.reputationDelta()).isEqualTo(-1);
        assertThat(playerStatsRepository.findByPlayerId(
                playerRepository.findByClerkUserId("ics_no_farm").orElseThrow().getId()
        ).orElseThrow().getAcademicReputation()).isEqualTo(49);
    }

    @Test
    void proxy_awardsPlus5AuraZeroReputation() {
        Jwt jwt = jwtWith("ics_proxy");
        createCseStudent(jwt);

        AttendIcsSessionResponse response = attendIcsService.punchProxy(jwt);

        assertThat(response.outcome()).isEqualTo("PROXY");
        assertThat(response.auraDelta()).isEqualTo(5);
        assertThat(response.reputationDelta()).isEqualTo(0);
        assertThat(response.aura()).isEqualTo(55);
        assertThat(response.academicReputation()).isEqualTo(50);
        assertThat(response.status()).isEqualTo("COMPLETED");
    }

    @Test
    void proxy_cannotBeRepeated() {
        Jwt jwt = jwtWith("ics_proxy_dup");
        createCseStudent(jwt);
        attendIcsService.punchProxy(jwt);

        AttendIcsSessionResponse second = attendIcsService.punchProxy(jwt);

        assertThat(second.alreadyApplied()).isTrue();
        assertThat(second.aura()).isEqualTo(55);
        assertThat(playerStatsRepository.findByPlayerId(
                playerRepository.findByClerkUserId("ics_proxy_dup").orElseThrow().getId()
        ).orElseThrow().getAura()).isEqualTo(55);
    }

    @Test
    void proxy_cannotSubsequentlyAttend() {
        Jwt jwt = jwtWith("ics_proxy_then_attend");
        createCseStudent(jwt);
        attendIcsService.punchProxy(jwt);

        AttendIcsSessionResponse start = attendIcsService.startLecture(jwt);
        assertThat(start.alreadyApplied()).isTrue();
        assertThat(start.outcome()).isEqualTo("PROXY");

        assertThatThrownBy(() -> attendIcsService.claimMilestone(jwt, new AttendIcsMilestoneRequest(30)))
                .isInstanceOf(IllegalArgumentException.class);
    }

    @Test
    void endDay_doesNotPenalizeProxyAgain() {
        Jwt jwt = jwtWith("ics_proxy_finalize");
        createCseStudent(jwt);
        attendIcsService.punchProxy(jwt);

        DayFinalizeResponse first = playerDayService.finalizeCurrentDay(jwt);
        DayFinalizeResponse second = playerDayService.finalizeCurrentDay(jwt);

        assertThat(first.activities())
                .filteredOn(a -> AttendIcsDefinition.ACTIVITY_ID.equals(a.activityId()))
                .first()
                .satisfies(a -> {
                    assertThat(a.outcome()).isEqualTo("PROXY");
                    assertThat(a.auraDelta()).isEqualTo(5);
                    assertThat(a.academicReputationDelta()).isEqualTo(0);
                });
        assertThat(first.aura()).isEqualTo(50); // breakfast also auto-missed −5 → 50? wait 55-5=50
        assertThat(second.aura()).isEqualTo(first.aura());
        assertThat(second.academicReputation()).isEqualTo(40);
    }

    @Test
    void neverAttended_skipMinus5Reputation() {
        Jwt jwt = jwtWith("ics_skip");
        createCseStudent(jwt);

        DayFinalizeResponse summary = playerDayService.finalizeCurrentDay(jwt);

        assertThat(summary.activities())
                .filteredOn(a -> AttendIcsDefinition.ACTIVITY_ID.equals(a.activityId()))
                .first()
                .satisfies(a -> {
                    assertThat(a.status()).isEqualTo("MISSED");
                    assertThat(a.outcome()).isEqualTo("SKIPPED");
                    assertThat(a.academicReputationDelta()).isEqualTo(-5);
                    assertThat(a.auraDelta()).isEqualTo(0);
                });
        assertThat(summary.academicReputation()).isEqualTo(35);
    }

    @Test
    void endDay_doesNotPenalizeEarlyLeaversTwice() {
        Jwt jwt = jwtWith("ics_leave_finalize");
        createCseStudent(jwt);
        attendIcsService.startLecture(jwt);
        claimThrough(jwt, 60);
        attendIcsService.leaveEarly(jwt);

        DayFinalizeResponse first = playerDayService.finalizeCurrentDay(jwt);
        DayFinalizeResponse second = playerDayService.finalizeCurrentDay(jwt);

        assertThat(first.activities())
                .filteredOn(a -> AttendIcsDefinition.ACTIVITY_ID.equals(a.activityId()))
                .first()
                .satisfies(a -> assertThat(a.academicReputationDelta()).isEqualTo(3));
        assertThat(first.academicReputation()).isEqualTo(43);
        assertThat(second.academicReputation()).isEqualTo(43);
    }

    @Test
    void milestones_areExactlyOnce() {
        Jwt jwt = jwtWith("ics_once");
        createCseStudent(jwt);
        attendIcsService.startLecture(jwt);
        mutableClock.advanceSeconds(30);

        AttendIcsSessionResponse first = attendIcsService.claimMilestone(jwt, new AttendIcsMilestoneRequest(30));
        AttendIcsSessionResponse second = attendIcsService.claimMilestone(jwt, new AttendIcsMilestoneRequest(30));

        assertThat(first.alreadyApplied()).isFalse();
        assertThat(second.alreadyApplied()).isTrue();
        assertThat(second.academicReputation()).isEqualTo(54);
    }

    @Test
    void summaryTotals_includeMilestonesAndEarlyLeavePenalty() {
        Jwt jwt = jwtWith("ics_summary_net");
        createCseStudent(jwt);
        attendIcsService.startLecture(jwt);
        claimThrough(jwt, 30);
        attendIcsService.leaveEarly(jwt);

        DayFinalizeResponse summary = playerDayService.finalizeCurrentDay(jwt);

        assertThat(summary.activities())
                .filteredOn(a -> AttendIcsDefinition.ACTIVITY_ID.equals(a.activityId()))
                .first()
                .satisfies(a -> assertThat(a.academicReputationDelta()).isEqualTo(-1));
        assertThat(summary.totalAcademicReputationDelta()).isEqualTo(-11);
    }

    @Test
    void bbaStudent_cannotResolveIcs() {
        Jwt jwt = jwtWith("ics_bba");
        playerSaveService.createSave(
                jwt,
                new PlayerSaveCreateRequest(PlayerRole.STUDENT, "BBA Student", bba.getId(), "22110001")
        );

        assertThatThrownBy(() -> attendIcsService.startLecture(jwt))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessageContaining("CSE students");

        DayFinalizeResponse summary = playerDayService.finalizeCurrentDay(jwt);
        assertThat(summary.activities())
                .filteredOn(a -> AttendIcsDefinition.ACTIVITY_ID.equals(a.activityId()))
                .isEmpty();
        assertThat(summary.academicReputation()).isEqualTo(50);
    }

    @Test
    void faculty_cannotResolveIcs() {
        Jwt jwt = jwtWith("ics_faculty");
        playerSaveService.createSave(
                jwt,
                new PlayerSaveCreateRequest(PlayerRole.FACULTY, "Faculty", cse.getId(), "F22110001")
        );

        assertThatThrownBy(() -> attendIcsService.punchProxy(jwt))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessageContaining("CSE students");
    }

    @Test
    void unscheduledDay_cannotResolveIcs() {
        Jwt jwt = jwtWith("ics_day2");
        createCseStudent(jwt);
        playerActivityService.resolveActivity(
                jwt,
                new com.uiusimulator.player.dto.ActivityResolveRequest("BREAKFAST", "RICE")
        );
        attendIcsService.punchProxy(jwt);
        playerDayService.finalizeCurrentDay(jwt);
        playerDayService.advanceDay(jwt, new DayAdvanceRequest(1, 1));

        assertThatThrownBy(() -> attendIcsService.startLecture(jwt))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessageContaining("not scheduled");
    }

    @Test
    void pausedSession_cannotClaimMilestone() {
        Jwt jwt = jwtWith("ics_paused");
        createCseStudent(jwt);
        attendIcsService.startLecture(jwt);
        mutableClock.advanceSeconds(30);
        attendIcsService.pauseLecture(jwt);

        assertThatThrownBy(() -> attendIcsService.claimMilestone(jwt, new AttendIcsMilestoneRequest(30)))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessageContaining("paused");
    }

    @Test
    void pauseFreezesActiveTime() {
        Jwt jwt = jwtWith("ics_freeze");
        createCseStudent(jwt);
        attendIcsService.startLecture(jwt);
        mutableClock.advanceSeconds(20);
        attendIcsService.pauseLecture(jwt);
        mutableClock.advanceSeconds(40); // wall clock while paused must not count
        attendIcsService.resumeLecture(jwt);
        mutableClock.advanceSeconds(9);

        assertThatThrownBy(() -> attendIcsService.claimMilestone(jwt, new AttendIcsMilestoneRequest(30)))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessageContaining("not yet earned");

        mutableClock.advanceSeconds(1);
        AttendIcsSessionResponse response = attendIcsService.claimMilestone(jwt, new AttendIcsMilestoneRequest(30));
        assertThat(response.reputationDelta()).isEqualTo(4);
    }

    @Test
    void exactly90_completionWinsOverLeave() {
        Jwt jwt = jwtWith("ics_race");
        createCseStudent(jwt);
        attendIcsService.startLecture(jwt);
        claimThrough(jwt, 90);

        assertThatThrownBy(() -> attendIcsService.leaveEarly(jwt))
                .isInstanceOf(IllegalArgumentException.class);

        assertThat(playerDayActivityRepository.findByPlayer_IdAndActivityId(
                playerRepository.findByClerkUserId("ics_race").orElseThrow().getId(),
                AttendIcsDefinition.ACTIVITY_ID
        ).orElseThrow().getOutcome()).isEqualTo("COMPLETED");
    }

    @Test
    void englishAndDm_completeIndependently_andDoNotShareMilestones() {
        Jwt jwt = jwtWith("classrooms_independent");
        createCseStudent(jwt);

        attendIcsService.startLecture(jwt, "ATTEND_ENGLISH");
        attendIcsService.startLecture(jwt, "ATTEND_DM");
        mutableClock.advanceSeconds(30);
        attendIcsService.claimMilestone(jwt, "ATTEND_ENGLISH", new AttendIcsMilestoneRequest(30));

        var playerId = playerRepository.findByClerkUserId("classrooms_independent").orElseThrow().getId();
        var english = playerDayActivityRepository.findByPlayer_IdAndActivityId(playerId, "ATTEND_ENGLISH").orElseThrow();
        var dm = playerDayActivityRepository.findByPlayer_IdAndActivityId(playerId, "ATTEND_DM").orElseThrow();

        assertThat(english.getMilestoneSeconds()).isEqualTo(30);
        assertThat(english.getReputationDelta()).isEqualTo(4);
        assertThat(dm.getMilestoneSeconds()).isEqualTo(0);
        assertThat(dm.getReputationDelta()).isEqualTo(0);
        assertThat(playerDayActivityRepository.findByPlayer_IdAndActivityId(playerId, "ATTEND_ICS")).isEmpty();

        claimThrough(jwt, "ATTEND_DM", 90);
        claimThrough(jwt, "ATTEND_ENGLISH", 90);

        assertThat(playerDayActivityRepository.findByPlayer_IdAndActivityId(playerId, "ATTEND_ENGLISH").orElseThrow()
                .getReputationDelta()).isEqualTo(12);
        assertThat(playerDayActivityRepository.findByPlayer_IdAndActivityId(playerId, "ATTEND_DM").orElseThrow()
                .getOutcome()).isEqualTo("COMPLETED");
        assertThat(playerStatsRepository.findByPlayerId(playerId).orElseThrow().getAcademicReputation()).isEqualTo(74);
        assertThat(playerStatsRepository.findByPlayerId(playerId).orElseThrow().getAura()).isEqualTo(50);
    }

    @Test
    void proxyEnglish_doesNotResolveDm_andCannotBeRepeated() {
        Jwt jwt = jwtWith("classrooms_proxy");
        createCseStudent(jwt);

        AttendIcsSessionResponse first = attendIcsService.punchProxy(jwt, "ATTEND_ENGLISH");
        AttendIcsSessionResponse second = attendIcsService.punchProxy(jwt, "ATTEND_ENGLISH");
        attendIcsService.startLecture(jwt, "ATTEND_DM");

        assertThat(first.outcome()).isEqualTo("PROXY");
        assertThat(first.auraDelta()).isEqualTo(5);
        assertThat(first.reputationDelta()).isEqualTo(0);
        assertThat(second.alreadyApplied()).isTrue();
        assertThat(second.aura()).isEqualTo(55);

        var playerId = playerRepository.findByClerkUserId("classrooms_proxy").orElseThrow().getId();
        assertThat(playerDayActivityRepository.findByPlayer_IdAndActivityId(playerId, "ATTEND_DM").orElseThrow()
                .getOutcome()).isEqualTo("ATTENDING");
        assertThat(playerDayActivityRepository.findByPlayer_IdAndActivityId(playerId, "ATTEND_ICS")).isEmpty();

        DayFinalizeResponse summary = playerDayService.finalizeCurrentDay(jwt);
        assertThat(summary.activities())
                .filteredOn(a -> "ATTEND_ENGLISH".equals(a.activityId()))
                .first()
                .satisfies(a -> {
                    assertThat(a.outcome()).isEqualTo("PROXY");
                    assertThat(a.academicReputationDelta()).isEqualTo(0);
                    assertThat(a.auraDelta()).isEqualTo(5);
                });
        assertThat(summary.activities())
                .filteredOn(a -> "ATTEND_DM".equals(a.activityId()))
                .first()
                .satisfies(a -> assertThat(a.outcome()).isEqualTo("LEFT_EARLY"));
        assertThat(summary.activities())
                .filteredOn(a -> "ATTEND_ICS".equals(a.activityId()))
                .first()
                .satisfies(a -> assertThat(a.outcome()).isEqualTo("SKIPPED"));

        int reputation = summary.academicReputation();
        assertThat(playerDayService.finalizeCurrentDay(jwt).academicReputation()).isEqualTo(reputation);
    }

    @Test
    void skipOnlyUnresolvedCourse_whenOthersAreComplete() {
        Jwt jwt = jwtWith("classrooms_skip_one");
        createCseStudent(jwt);
        attendIcsService.startLecture(jwt);
        claimThrough(jwt, 90);
        attendIcsService.startLecture(jwt, "ATTEND_DM");
        claimThrough(jwt, "ATTEND_DM", 90);

        DayFinalizeResponse summary = playerDayService.finalizeCurrentDay(jwt);

        assertThat(summary.activities())
                .filteredOn(a -> "ATTEND_ICS".equals(a.activityId()))
                .first()
                .satisfies(a -> {
                    assertThat(a.outcome()).isEqualTo("COMPLETED");
                    assertThat(a.academicReputationDelta()).isEqualTo(12);
                });
        assertThat(summary.activities())
                .filteredOn(a -> "ATTEND_DM".equals(a.activityId()))
                .first()
                .satisfies(a -> {
                    assertThat(a.outcome()).isEqualTo("COMPLETED");
                    assertThat(a.academicReputationDelta()).isEqualTo(12);
                });
        assertThat(summary.activities())
                .filteredOn(a -> "ATTEND_ENGLISH".equals(a.activityId()))
                .first()
                .satisfies(a -> {
                    assertThat(a.outcome()).isEqualTo("SKIPPED");
                    assertThat(a.academicReputationDelta()).isEqualTo(-5);
                });
        assertThat(summary.academicReputation()).isEqualTo(69);
        assertThat(playerDayService.finalizeCurrentDay(jwt).academicReputation()).isEqualTo(69);
    }

    @Test
    void earlyLeave_isOncePerCourse() {
        Jwt jwt = jwtWith("classrooms_leave");
        createCseStudent(jwt);
        attendIcsService.startLecture(jwt, "ATTEND_ENGLISH");
        mutableClock.advanceSeconds(30);
        attendIcsService.claimMilestone(jwt, "ATTEND_ENGLISH", new AttendIcsMilestoneRequest(30));
        AttendIcsSessionResponse left = attendIcsService.leaveEarly(jwt, "ATTEND_ENGLISH");
        AttendIcsSessionResponse again = attendIcsService.leaveEarly(jwt, "ATTEND_ENGLISH");

        assertThat(left.outcome()).isEqualTo("LEFT_EARLY");
        assertThat(left.reputationDelta()).isEqualTo(-1);
        assertThat(again.alreadyApplied()).isTrue();

        attendIcsService.startLecture(jwt, "ATTEND_DM");
        AttendIcsSessionResponse dmLeave = attendIcsService.leaveEarly(jwt, "ATTEND_DM");
        assertThat(dmLeave.reputationDelta()).isEqualTo(-5);

        var playerId = playerRepository.findByClerkUserId("classrooms_leave").orElseThrow().getId();
        assertThat(playerStatsRepository.findByPlayerId(playerId).orElseThrow().getAcademicReputation()).isEqualTo(44);
    }

    @Test
    void bbaAndFaculty_cannotReceiveCseClassroomRewards() {
        Jwt bba = jwtWith("classrooms_bba");
        playerSaveService.createSave(
                bba,
                new PlayerSaveCreateRequest(PlayerRole.STUDENT, "BBA Student", bbaDepartment().getId(), "22110001")
        );
        assertThatThrownBy(() -> attendIcsService.startLecture(bba, "ATTEND_ENGLISH"))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessageContaining("CSE students");
        assertThatThrownBy(() -> attendIcsService.punchProxy(bba, "ATTEND_DM"))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessageContaining("CSE students");

        Jwt faculty = jwtWith("classrooms_faculty");
        playerSaveService.createSave(
                faculty,
                new PlayerSaveCreateRequest(PlayerRole.FACULTY, "Faculty", cse.getId(), "F22110001")
        );
        assertThatThrownBy(() -> attendIcsService.claimMilestone(
                faculty,
                "ATTEND_DM",
                new AttendIcsMilestoneRequest(30)
        )).isInstanceOf(IllegalArgumentException.class)
                .hasMessageContaining("CSE students");

        DayFinalizeResponse summary = playerDayService.finalizeCurrentDay(bba);
        assertThat(summary.activities())
                .filteredOn(a -> a.activityId().startsWith("ATTEND_"))
                .isEmpty();
        assertThat(summary.academicReputation()).isEqualTo(50);
    }

    @Test
    void unknownActivity_cannotMintReputation() {
        Jwt jwt = jwtWith("classrooms_unknown");
        createCseStudent(jwt);

        assertThatThrownBy(() -> attendIcsService.startLecture(jwt, "ATTEND_CLASS"))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessageContaining("Unknown classroom activity");
        assertThatThrownBy(() -> attendIcsService.claimMilestone(
                jwt,
                "ATTEND_WHATEVER",
                new AttendIcsMilestoneRequest(30)
        )).isInstanceOf(IllegalArgumentException.class);

        assertThat(playerStatsRepository.findByPlayerId(
                playerRepository.findByClerkUserId("classrooms_unknown").orElseThrow().getId()
        ).orElseThrow().getAcademicReputation()).isEqualTo(50);
    }

    @Test
    void day2_doesNotPenalizeUnscheduledClassrooms() {
        Jwt jwt = jwtWith("classrooms_day2");
        createCseStudent(jwt);
        playerActivityService.resolveActivity(
                jwt,
                new com.uiusimulator.player.dto.ActivityResolveRequest("BREAKFAST", "RICE")
        );
        attendIcsService.punchProxy(jwt);
        attendIcsService.punchProxy(jwt, "ATTEND_ENGLISH");
        attendIcsService.punchProxy(jwt, "ATTEND_DM");
        playerDayService.finalizeCurrentDay(jwt);
        playerDayService.advanceDay(jwt, new DayAdvanceRequest(1, 1));

        DayFinalizeResponse day2 = playerDayService.finalizeCurrentDay(jwt);
        assertThat(day2.day()).isEqualTo(2);
        assertThat(day2.activities())
                .filteredOn(a -> a.activityId().startsWith("ATTEND_"))
                .isEmpty();
    }

    @Test
    void duplicateMilestone_cannotFarmEnglish() {
        Jwt jwt = jwtWith("classrooms_farm");
        createCseStudent(jwt);
        attendIcsService.startLecture(jwt, "ATTEND_ENGLISH");
        mutableClock.advanceSeconds(30);

        AttendIcsSessionResponse first = attendIcsService.claimMilestone(
                jwt, "ATTEND_ENGLISH", new AttendIcsMilestoneRequest(30));
        AttendIcsSessionResponse second = attendIcsService.claimMilestone(
                jwt, "ATTEND_ENGLISH", new AttendIcsMilestoneRequest(30));

        assertThat(first.alreadyApplied()).isFalse();
        assertThat(second.alreadyApplied()).isTrue();
        assertThat(second.academicReputation()).isEqualTo(54);
    }

    private Department bbaDepartment() {
        return bba;
    }

    private void claimThrough(Jwt jwt, int targetMilestone) {
        claimThrough(jwt, AttendIcsDefinition.ACTIVITY_ID, targetMilestone);
    }

    private void claimThrough(Jwt jwt, String activityId, int targetMilestone) {
        if (targetMilestone >= 30) {
            mutableClock.advanceSeconds(30);
            attendIcsService.claimMilestone(jwt, activityId, new AttendIcsMilestoneRequest(30));
        }
        if (targetMilestone >= 60) {
            mutableClock.advanceSeconds(30);
            attendIcsService.claimMilestone(jwt, activityId, new AttendIcsMilestoneRequest(60));
        }
        if (targetMilestone >= 90) {
            mutableClock.advanceSeconds(30);
            attendIcsService.claimMilestone(jwt, activityId, new AttendIcsMilestoneRequest(90));
        }
    }

    private void createCseStudent(Jwt jwt) {
        playerSaveService.createSave(
                jwt,
                new PlayerSaveCreateRequest(PlayerRole.STUDENT, "CSE Student", cse.getId(), "22119999")
        );
    }

    private static Jwt jwtWith(String subject) {
        return Jwt.withTokenValue("token-" + subject)
                .header("alg", "none")
                .subject(subject)
                .claim("email", subject + "@uiu.edu")
                .claim("username", subject)
                .build();
    }

    /** Test clock that can be advanced without waiting real time. */
    static final class MutableClock extends Clock {
        private final AtomicReference<Instant> instant =
                new AtomicReference<>(Instant.parse("2026-01-01T10:00:00Z"));

        void setInstant(Instant value) {
            instant.set(value);
        }

        void advanceSeconds(long seconds) {
            instant.updateAndGet(current -> current.plusSeconds(seconds));
        }

        @Override
        public ZoneOffset getZone() {
            return ZoneOffset.UTC;
        }

        @Override
        public Clock withZone(java.time.ZoneId zone) {
            return this;
        }

        @Override
        public Instant instant() {
            return instant.get();
        }
    }

    @TestConfiguration
    static class MutableClockConfig {
        @Bean
        @Primary
        MutableClock mutableClock() {
            return new MutableClock();
        }

        @Bean
        Clock clock(MutableClock mutableClock) {
            return mutableClock;
        }
    }
}
