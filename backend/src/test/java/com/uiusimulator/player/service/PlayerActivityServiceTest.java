package com.uiusimulator.player.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.uiusimulator.player.dto.ActivityListResponse;
import com.uiusimulator.player.dto.ActivityResolveRequest;
import com.uiusimulator.player.dto.ActivityResolveResponse;
import com.uiusimulator.player.dto.PlayerSaveCreateRequest;
import com.uiusimulator.player.entity.BreakfastOutcome;
import com.uiusimulator.player.entity.Department;
import com.uiusimulator.player.entity.Player;
import com.uiusimulator.player.entity.PlayerRole;
import com.uiusimulator.player.entity.PlayerStats;
import com.uiusimulator.player.exception.PlayerSaveNotFoundException;
import com.uiusimulator.player.repository.DepartmentRepository;
import com.uiusimulator.player.repository.PlayerDayActivityRepository;
import com.uiusimulator.player.repository.PlayerRepository;
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
@Import({PlayerService.class, PlayerSaveService.class, PlayerActivityService.class})
class PlayerActivityServiceTest {

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

    private Department cse;

    @BeforeEach
    void seedDepartments() {
        cse = departmentRepository.saveAndFlush(
                Department.createNew("CSE", "Computer Science and Engineering")
        );
    }

    @Test
    void resolve_getIdCard_completesWithZeroRewards() {
        Jwt jwt = jwtWith("user_act_idcard");
        createSave(jwt);

        // createSave already records GET_ID_CARD; resolve must be idempotent.
        ActivityResolveResponse response = playerActivityService.resolveActivity(
                jwt,
                new ActivityResolveRequest("GET_ID_CARD", "COMPLETED")
        );

        assertThat(response.status()).isEqualTo("COMPLETED");
        assertThat(response.outcome()).isEqualTo("COMPLETED");
        assertThat(response.auraDelta()).isEqualTo(0);
        assertThat(response.reputationDelta()).isEqualTo(0);
        assertThat(response.alreadyResolved()).isTrue();
        assertThat(response.aura()).isEqualTo(50);

        Player player = playerRepository.findByClerkUserId("user_act_idcard").orElseThrow();
        assertThat(playerDayActivityRepository.findByPlayer_IdAndActivityId(player.getId(), "GET_ID_CARD")).isPresent();
        assertThat(playerStatsRepository.findByPlayerId(player.getId()).orElseThrow().getAura()).isEqualTo(50);
    }

    @Test
    void resolve_getIdCard_duplicate_doesNotChangeState() {
        Jwt jwt = jwtWith("user_act_idcard_dup");
        createSave(jwt);

        ActivityResolveResponse first = playerActivityService.resolveActivity(
                jwt,
                new ActivityResolveRequest("GET_ID_CARD", "COMPLETED")
        );
        ActivityResolveResponse second = playerActivityService.resolveActivity(
                jwt,
                new ActivityResolveRequest("GET_ID_CARD", "COMPLETED")
        );

        assertThat(first.alreadyResolved()).isTrue();
        assertThat(second.alreadyResolved()).isTrue();
        assertThat(second.outcome()).isEqualTo("COMPLETED");

        Player player = playerRepository.findByClerkUserId("user_act_idcard_dup").orElseThrow();
        assertThat(playerDayActivityRepository.findByPlayer_IdAndActivityId(player.getId(), "GET_ID_CARD")).isPresent();
        assertThat(playerDayActivityRepository.findByPlayer_IdOrderByResolvedAtAsc(player.getId())
                .stream()
                .filter(a -> "GET_ID_CARD".equals(a.getActivityId()))
                .count()).isEqualTo(1);
    }

    @Test
    void createSave_seedsGetIdCardCompleted() {
        Jwt jwt = jwtWith("user_act_idcard_seed");
        createSave(jwt);

        ActivityListResponse listed = playerActivityService.listCurrentDayActivities(jwt);
        assertThat(listed.activities()).anySatisfy(activity -> {
            assertThat(activity.activityId()).isEqualTo("GET_ID_CARD");
            assertThat(activity.status()).isEqualTo("COMPLETED");
            assertThat(activity.outcome()).isEqualTo("COMPLETED");
            assertThat(activity.auraDelta()).isEqualTo(0);
        });
    }

    @Test
    void deleteSave_clearsGetIdCardActivity() {
        Jwt jwt = jwtWith("user_act_idcard_reset");
        createSave(jwt);

        Player player = playerRepository.findByClerkUserId("user_act_idcard_reset").orElseThrow();
        assertThat(playerDayActivityRepository.findByPlayer_IdAndActivityId(player.getId(), "GET_ID_CARD")).isPresent();

        playerSaveService.deleteSave(jwt);

        assertThat(playerDayActivityRepository.findByPlayer_IdAndActivityId(player.getId(), "GET_ID_CARD")).isEmpty();
    }

    @Test
    void resolve_rice_completesWithPlus3Aura() {
        Jwt jwt = jwtWith("user_act_rice");
        createSave(jwt);

        ActivityResolveResponse response = playerActivityService.resolveActivity(
                jwt,
                new ActivityResolveRequest("BREAKFAST", "RICE")
        );

        assertThat(response.status()).isEqualTo("COMPLETED");
        assertThat(response.outcome()).isEqualTo("RICE");
        assertThat(response.auraDelta()).isEqualTo(3);
        assertThat(response.alreadyResolved()).isFalse();
        assertThat(response.aura()).isEqualTo(53);

        Player player = playerRepository.findByClerkUserId("user_act_rice").orElseThrow();
        PlayerStats stats = playerStatsRepository.findByPlayerId(player.getId()).orElseThrow();
        assertThat(stats.getAura()).isEqualTo(53);
        assertThat(playerDayActivityRepository.findByPlayer_IdAndActivityId(player.getId(), "BREAKFAST")).isPresent();
    }

    @Test
    void resolve_porottaWait_completesWithZeroAura() {
        Jwt jwt = jwtWith("user_act_wait");
        createSave(jwt);

        ActivityResolveResponse response = playerActivityService.resolveActivity(
                jwt,
                new ActivityResolveRequest("BREAKFAST", "POROTTA_WAIT")
        );

        assertThat(response.status()).isEqualTo("COMPLETED");
        assertThat(response.auraDelta()).isEqualTo(0);
        assertThat(response.aura()).isEqualTo(50);
    }

    @Test
    void resolve_skipLine_completesWithMinus6Aura() {
        Jwt jwt = jwtWith("user_act_skip_line");
        createSave(jwt);

        ActivityResolveResponse response = playerActivityService.resolveActivity(
                jwt,
                new ActivityResolveRequest("BREAKFAST", "SKIP_LINE")
        );

        assertThat(response.status()).isEqualTo("COMPLETED");
        assertThat(response.auraDelta()).isEqualTo(-6);
        assertThat(response.aura()).isEqualTo(44);
    }

    @Test
    void resolve_skipBreakfast_missedWithMinus3Aura() {
        Jwt jwt = jwtWith("user_act_skip_bf");
        createSave(jwt);

        ActivityResolveResponse response = playerActivityService.resolveActivity(
                jwt,
                new ActivityResolveRequest("BREAKFAST", "SKIP_BREAKFAST")
        );

        assertThat(response.status()).isEqualTo("MISSED");
        assertThat(response.outcome()).isEqualTo("SKIP_BREAKFAST");
        assertThat(response.auraDelta()).isEqualTo(-3);
        assertThat(response.aura()).isEqualTo(47);
    }

    @Test
    void resolve_duplicate_doesNotReapplyAura() {
        Jwt jwt = jwtWith("user_act_dup");
        createSave(jwt);

        playerActivityService.resolveActivity(jwt, new ActivityResolveRequest("BREAKFAST", "RICE"));
        ActivityResolveResponse second = playerActivityService.resolveActivity(
                jwt,
                new ActivityResolveRequest("BREAKFAST", "SKIP_LINE")
        );

        assertThat(second.alreadyResolved()).isTrue();
        assertThat(second.outcome()).isEqualTo("RICE");
        assertThat(second.aura()).isEqualTo(53);

        Player player = playerRepository.findByClerkUserId("user_act_dup").orElseThrow();
        assertThat(playerDayActivityRepository.findByPlayer_IdAndActivityId(player.getId(), "BREAKFAST")).isPresent();
        assertThat(playerDayActivityRepository.findByPlayer_IdOrderByResolvedAtAsc(player.getId())
                .stream()
                .filter(a -> "BREAKFAST".equals(a.getActivityId()))
                .count()).isEqualTo(1);
        assertThat(playerStatsRepository.findByPlayerId(player.getId()).orElseThrow().getAura()).isEqualTo(53);
    }

    @Test
    void listActivities_returnsResolvedOnly() {
        Jwt jwt = jwtWith("user_act_list");
        createSave(jwt);

        ActivityListResponse before = playerActivityService.listCurrentDayActivities(jwt);
        assertThat(before.dayNumber()).isEqualTo(1);
        // Admission seeds GET_ID_CARD COMPLETED; breakfast remains pending (no row).
        assertThat(before.activities()).hasSize(1);
        assertThat(before.activities().get(0).activityId()).isEqualTo("GET_ID_CARD");

        playerActivityService.resolveActivity(jwt, new ActivityResolveRequest("breakfast", "rice"));

        ActivityListResponse after = playerActivityService.listCurrentDayActivities(jwt);
        assertThat(after.activities()).hasSize(2);
        assertThat(after.activities())
                .extracting(a -> a.activityId())
                .containsExactlyInAnyOrder("GET_ID_CARD", BreakfastOutcome.ACTIVITY_ID);
        assertThat(after.activities())
                .filteredOn(a -> BreakfastOutcome.ACTIVITY_ID.equals(a.activityId()))
                .first()
                .extracting(a -> a.status())
                .isEqualTo("COMPLETED");
    }

    @Test
    void resolve_withoutSave_throwsNotFound() {
        Jwt jwt = jwtWith("user_act_nosave");
        playerService.getOrProvisionPlayer(jwt);

        assertThatThrownBy(() -> playerActivityService.resolveActivity(
                jwt,
                new ActivityResolveRequest("BREAKFAST", "RICE")
        )).isInstanceOf(PlayerSaveNotFoundException.class);
    }

    @Test
    void deleteSave_clearsActivityState() {
        Jwt jwt = jwtWith("user_act_reset");
        createSave(jwt);
        playerActivityService.resolveActivity(jwt, new ActivityResolveRequest("BREAKFAST", "RICE"));

        Player player = playerRepository.findByClerkUserId("user_act_reset").orElseThrow();
        assertThat(playerDayActivityRepository.findByPlayer_IdAndActivityId(player.getId(), "BREAKFAST")).isPresent();

        playerSaveService.deleteSave(jwt);

        assertThat(playerDayActivityRepository.findByPlayer_IdAndActivityId(player.getId(), "BREAKFAST")).isEmpty();
        assertThat(playerStatsRepository.findByPlayerId(player.getId()).orElseThrow().getAura()).isEqualTo(50);
    }

    @Test
    void resolve_rice_storesActualAppliedDeltaWhenClamped() {
        Jwt jwt = jwtWith("user_act_rice_clamp");
        createSave(jwt);
        Player player = playerRepository.findByClerkUserId("user_act_rice_clamp").orElseThrow();
        PlayerStats stats = playerStatsRepository.findByPlayerId(player.getId()).orElseThrow();
        stats.modifyStats(49, 0);
        playerStatsRepository.saveAndFlush(stats);

        ActivityResolveResponse response = playerActivityService.resolveActivity(
                jwt,
                new ActivityResolveRequest("BREAKFAST", "RICE")
        );

        assertThat(response.auraDelta()).isEqualTo(1);
        assertThat(response.aura()).isEqualTo(100);
        assertThat(playerDayActivityRepository.findByPlayer_IdAndActivityId(player.getId(), "BREAKFAST")
                .orElseThrow().getAuraDelta()).isEqualTo(1);
        assertThat(response.aura()).isEqualTo(100);
    }

    @Test
    void resolve_skipBreakfast_storesActualAppliedDeltaWhenClamped() {
        Jwt jwt = jwtWith("user_act_skip_clamp");
        createSave(jwt);
        Player player = playerRepository.findByClerkUserId("user_act_skip_clamp").orElseThrow();
        PlayerStats stats = playerStatsRepository.findByPlayerId(player.getId()).orElseThrow();
        stats.modifyStats(-49, 0);
        playerStatsRepository.saveAndFlush(stats);

        ActivityResolveResponse response = playerActivityService.resolveActivity(
                jwt,
                new ActivityResolveRequest("BREAKFAST", "SKIP_BREAKFAST")
        );

        assertThat(response.auraDelta()).isEqualTo(-1);
        assertThat(response.aura()).isEqualTo(0);
        assertThat(playerDayActivityRepository.findByPlayer_IdAndActivityId(player.getId(), "BREAKFAST")
                .orElseThrow().getAuraDelta()).isEqualTo(-1);
    }

    private void createSave(Jwt jwt) {
        playerSaveService.createSave(
                jwt,
                new PlayerSaveCreateRequest(PlayerRole.STUDENT, "Activity Student", cse.getId(), "22110001")
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
