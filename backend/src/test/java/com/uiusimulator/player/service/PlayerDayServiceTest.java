package com.uiusimulator.player.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.uiusimulator.player.dto.ActivityResolveRequest;
import com.uiusimulator.player.dto.DayAdvanceRequest;
import com.uiusimulator.player.dto.DayAdvanceResponse;
import com.uiusimulator.player.dto.DayFinalizeResponse;
import com.uiusimulator.player.dto.PlayerSaveCreateRequest;
import com.uiusimulator.player.entity.Department;
import com.uiusimulator.player.entity.Player;
import com.uiusimulator.player.entity.PlayerRole;
import com.uiusimulator.player.entity.PlayerSave;
import com.uiusimulator.player.repository.DepartmentRepository;
import com.uiusimulator.player.repository.PlayerDayActivityRepository;
import com.uiusimulator.player.repository.PlayerRepository;
import com.uiusimulator.player.repository.PlayerSaveRepository;
import com.uiusimulator.player.repository.PlayerStatsRepository;
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
        PlayerSaveService.class,
        PlayerActivityService.class,
        PlayerDayService.class,
        AttendIcsService.class,
        com.uiusimulator.config.TimeConfig.class
})
class PlayerDayServiceTest {

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
    private PlayerSaveRepository playerSaveRepository;

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
    void finalize_withBreakfastCompleted_noExtraPenalty() {
        Jwt jwt = jwtWith("user_day_rice");
        createSave(jwt);
        playerActivityService.resolveActivity(jwt, new ActivityResolveRequest("BREAKFAST", "RICE"));

        DayFinalizeResponse summary = playerDayService.finalizeCurrentDay(jwt);

        assertThat(summary.semester()).isEqualTo(1);
        assertThat(summary.day()).isEqualTo(1);
        assertThat(summary.totalAuraDelta()).isEqualTo(3);
        assertThat(summary.aura()).isEqualTo(53);
        assertThat(summary.activities())
                .filteredOn(a -> "BREAKFAST".equals(a.activityId()))
                .first()
                .satisfies(a -> {
                    assertThat(a.status()).isEqualTo("COMPLETED");
                    assertThat(a.auraDelta()).isEqualTo(3);
                });
        assertThat(summary.activities())
                .filteredOn(a -> "GET_ID_CARD".equals(a.activityId()))
                .first()
                .extracting(a -> a.status())
                .isEqualTo("COMPLETED");
    }

    @Test
    void finalize_withBreakfastAlreadyMissed_noExtraPenalty() {
        Jwt jwt = jwtWith("user_day_missed");
        createSave(jwt);
        playerActivityService.resolveActivity(jwt, new ActivityResolveRequest("BREAKFAST", "SKIP_BREAKFAST"));

        DayFinalizeResponse first = playerDayService.finalizeCurrentDay(jwt);
        DayFinalizeResponse second = playerDayService.finalizeCurrentDay(jwt);

        assertThat(first.aura()).isEqualTo(47);
        assertThat(second.aura()).isEqualTo(47);
        assertThat(second.totalAuraDelta()).isEqualTo(-3);
        assertThat(playerStatsRepository.findByPlayerId(
                playerRepository.findByClerkUserId("user_day_missed").orElseThrow().getId()
        ).orElseThrow().getAura()).isEqualTo(47);
    }

    @Test
    void finalize_withBreakfastPending_marksMissedOnce() {
        Jwt jwt = jwtWith("user_day_pending");
        createSave(jwt);

        DayFinalizeResponse summary = playerDayService.finalizeCurrentDay(jwt);

        assertThat(summary.activities())
                .filteredOn(a -> "BREAKFAST".equals(a.activityId()))
                .first()
                .satisfies(a -> {
                    assertThat(a.status()).isEqualTo("MISSED");
                    assertThat(a.outcome()).isEqualTo("SKIP_BREAKFAST");
                    assertThat(a.auraDelta()).isEqualTo(-3);
                });
        assertThat(summary.totalAuraDelta()).isEqualTo(-3);
        assertThat(summary.aura()).isEqualTo(47);
    }

    @Test
    void finalize_riceClamp_summaryUsesActualAppliedAura() {
        Jwt jwt = jwtWith("user_day_rice_clamp");
        createSave(jwt);
        Player player = playerRepository.findByClerkUserId("user_day_rice_clamp").orElseThrow();
        var stats = playerStatsRepository.findByPlayerId(player.getId()).orElseThrow();
        stats.modifyStats(49, 0);
        playerStatsRepository.saveAndFlush(stats);

        playerActivityService.resolveActivity(jwt, new ActivityResolveRequest("BREAKFAST", "RICE"));
        DayFinalizeResponse summary = playerDayService.finalizeCurrentDay(jwt);

        assertThat(summary.activities())
                .filteredOn(a -> "BREAKFAST".equals(a.activityId()))
                .first()
                .satisfies(a -> assertThat(a.auraDelta()).isEqualTo(1));
        assertThat(summary.totalAuraDelta()).isEqualTo(1);
        assertThat(summary.aura()).isEqualTo(100);
    }

    @Test
    void finalize_skipBreakfastClamp_summaryUsesActualAppliedAura() {
        Jwt jwt = jwtWith("user_day_skip_clamp");
        createSave(jwt);
        Player player = playerRepository.findByClerkUserId("user_day_skip_clamp").orElseThrow();
        var stats = playerStatsRepository.findByPlayerId(player.getId()).orElseThrow();
        stats.modifyStats(-49, 0);
        playerStatsRepository.saveAndFlush(stats);

        playerActivityService.resolveActivity(jwt, new ActivityResolveRequest("BREAKFAST", "SKIP_BREAKFAST"));
        DayFinalizeResponse summary = playerDayService.finalizeCurrentDay(jwt);

        assertThat(summary.activities())
                .filteredOn(a -> "BREAKFAST".equals(a.activityId()))
                .first()
                .satisfies(a -> assertThat(a.auraDelta()).isEqualTo(-1));
        assertThat(summary.totalAuraDelta()).isEqualTo(-1);
        assertThat(summary.aura()).isEqualTo(0);
    }

    @Test
    void finalize_twice_doesNotReapplyAura() {
        Jwt jwt = jwtWith("user_day_dup_final");
        createSave(jwt);

        DayFinalizeResponse first = playerDayService.finalizeCurrentDay(jwt);
        DayFinalizeResponse second = playerDayService.finalizeCurrentDay(jwt);

        assertThat(first.aura()).isEqualTo(47);
        assertThat(second.aura()).isEqualTo(47);
        assertThat(second.activities())
                .filteredOn(a -> "BREAKFAST".equals(a.activityId()))
                .hasSize(1);

        Player player = playerRepository.findByClerkUserId("user_day_dup_final").orElseThrow();
        assertThat(playerDayActivityRepository.findByPlayer_IdAndActivityId(player.getId(), "BREAKFAST")).isPresent();
        assertThat(playerStatsRepository.findByPlayerId(player.getId()).orElseThrow().getAura()).isEqualTo(47);
    }

    @Test
    void advance_day1ToDay2() {
        Jwt jwt = jwtWith("user_day_adv");
        createSave(jwt);
        playerActivityService.resolveActivity(jwt, new ActivityResolveRequest("BREAKFAST", "RICE"));
        playerDayService.finalizeCurrentDay(jwt);

        DayAdvanceResponse advanced = playerDayService.advanceDay(jwt, new DayAdvanceRequest(1, 1));

        assertThat(advanced.alreadyAdvanced()).isFalse();
        assertThat(advanced.semester()).isEqualTo(1);
        assertThat(advanced.currentDay()).isEqualTo(2);
        assertThat(advanced.idCardIssued()).isTrue();
        assertThat(advanced.aura()).isEqualTo(53);

        Player player = playerRepository.findByClerkUserId("user_day_adv").orElseThrow();
        PlayerSave save = playerSaveRepository.findByPlayerId(player.getId()).orElseThrow();
        assertThat(save.getCurrentDay()).isEqualTo(2);
        assertThat(save.isIdCardIssued()).isTrue();
        assertThat(playerDayActivityRepository.findByPlayer_IdOrderByResolvedAtAsc(player.getId())).isEmpty();
    }

    @Test
    void advance_duplicateExpectedDay1_doesNotAdvanceToDay3() {
        Jwt jwt = jwtWith("user_day_adv_dup");
        createSave(jwt);
        playerDayService.finalizeCurrentDay(jwt);

        DayAdvanceResponse first = playerDayService.advanceDay(jwt, new DayAdvanceRequest(1, 1));
        DayAdvanceResponse second = playerDayService.advanceDay(jwt, new DayAdvanceRequest(1, 1));

        assertThat(first.currentDay()).isEqualTo(2);
        assertThat(first.alreadyAdvanced()).isFalse();
        assertThat(second.currentDay()).isEqualTo(2);
        assertThat(second.alreadyAdvanced()).isTrue();

        Player player = playerRepository.findByClerkUserId("user_day_adv_dup").orElseThrow();
        assertThat(playerSaveRepository.findByPlayerId(player.getId()).orElseThrow().getCurrentDay()).isEqualTo(2);
    }

    @Test
    void day2_breakfastStartsPending_andGetIdCardNotRequired() {
        Jwt jwt = jwtWith("user_day2_pending");
        createSave(jwt);
        playerActivityService.resolveActivity(jwt, new ActivityResolveRequest("BREAKFAST", "RICE"));
        playerDayService.finalizeCurrentDay(jwt);
        playerDayService.advanceDay(jwt, new DayAdvanceRequest(1, 1));

        Player player = playerRepository.findByClerkUserId("user_day2_pending").orElseThrow();
        assertThat(playerDayActivityRepository.findByPlayer_IdAndActivityId(player.getId(), "BREAKFAST")).isEmpty();
        assertThat(playerDayActivityRepository.findByPlayer_IdAndActivityId(player.getId(), "GET_ID_CARD")).isEmpty();
        assertThat(playerSaveRepository.findByPlayerId(player.getId()).orElseThrow().isIdCardIssued()).isTrue();

        var listed = playerActivityService.listCurrentDayActivities(jwt);
        assertThat(listed.dayNumber()).isEqualTo(2);
        assertThat(listed.activities()).isEmpty();
    }

    @Test
    void day6_cannotAdvanceToDay7() {
        Jwt jwt = jwtWith("user_day6");
        createSave(jwt);

        // Walk from day 1 → 6 via real advances.
        for (int day = 1; day < PlayerSave.MAX_DAY_PER_SEMESTER; day++) {
            playerDayService.finalizeCurrentDay(jwt);
            playerDayService.advanceDay(jwt, new DayAdvanceRequest(1, day));
        }

        playerDayService.finalizeCurrentDay(jwt);
        assertThatThrownBy(() -> playerDayService.advanceDay(jwt, new DayAdvanceRequest(1, 6)))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessageContaining("Semester progression is not available yet");

        Player player = playerRepository.findByClerkUserId("user_day6").orElseThrow();
        PlayerSave save = playerSaveRepository.findByPlayerId(player.getId()).orElseThrow();
        assertThat(save.getCurrentDay()).isEqualTo(6);
        assertThat(save.isIdCardIssued()).isTrue();
    }

    @Test
    void newGame_resetsProgression() {
        Jwt jwt = jwtWith("user_day_newgame");
        createSave(jwt);
        playerDayService.finalizeCurrentDay(jwt);
        playerDayService.advanceDay(jwt, new DayAdvanceRequest(1, 1));

        playerSaveService.deleteSave(jwt);
        createSave(jwt);

        Player player = playerRepository.findByClerkUserId("user_day_newgame").orElseThrow();
        PlayerSave save = playerSaveRepository.findByPlayerId(player.getId()).orElseThrow();
        assertThat(save.getSemester()).isEqualTo(1);
        assertThat(save.getCurrentDay()).isEqualTo(1);
        assertThat(save.isIdCardIssued()).isTrue();
        assertThat(playerDayActivityRepository.findByPlayer_IdAndActivityId(player.getId(), "GET_ID_CARD")).isPresent();
        assertThat(playerDayActivityRepository.findByPlayer_IdAndActivityId(player.getId(), "BREAKFAST")).isEmpty();
        assertThat(playerStatsRepository.findByPlayerId(player.getId()).orElseThrow().getAura()).isEqualTo(50);
    }

    @Test
    void advance_withoutSave_throwsNotFound() {
        Jwt jwt = jwtWith("user_day_nosave");
        playerService.getOrProvisionPlayer(jwt);

        assertThatThrownBy(() -> playerDayService.advanceDay(jwt, new DayAdvanceRequest(1, 1)))
                .isInstanceOf(com.uiusimulator.player.exception.PlayerSaveNotFoundException.class);
    }

    private void createSave(Jwt jwt) {
        playerSaveService.createSave(
                jwt,
                new PlayerSaveCreateRequest(PlayerRole.STUDENT, "Day Student", cse.getId(), "22119999")
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
}
