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
    void resolve_rice_completesWithPlus5Aura() {
        Jwt jwt = jwtWith("user_act_rice");
        createSave(jwt);

        ActivityResolveResponse response = playerActivityService.resolveActivity(
                jwt,
                new ActivityResolveRequest("BREAKFAST", "RICE")
        );

        assertThat(response.status()).isEqualTo("COMPLETED");
        assertThat(response.outcome()).isEqualTo("RICE");
        assertThat(response.auraDelta()).isEqualTo(5);
        assertThat(response.alreadyResolved()).isFalse();
        assertThat(response.aura()).isEqualTo(55);

        Player player = playerRepository.findByClerkUserId("user_act_rice").orElseThrow();
        PlayerStats stats = playerStatsRepository.findByPlayerId(player.getId()).orElseThrow();
        assertThat(stats.getAura()).isEqualTo(55);
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
    void resolve_skipLine_completesWithMinus10Aura() {
        Jwt jwt = jwtWith("user_act_skip_line");
        createSave(jwt);

        ActivityResolveResponse response = playerActivityService.resolveActivity(
                jwt,
                new ActivityResolveRequest("BREAKFAST", "SKIP_LINE")
        );

        assertThat(response.status()).isEqualTo("COMPLETED");
        assertThat(response.auraDelta()).isEqualTo(-10);
        assertThat(response.aura()).isEqualTo(40);
    }

    @Test
    void resolve_skipBreakfast_missedWithMinus5Aura() {
        Jwt jwt = jwtWith("user_act_skip_bf");
        createSave(jwt);

        ActivityResolveResponse response = playerActivityService.resolveActivity(
                jwt,
                new ActivityResolveRequest("BREAKFAST", "SKIP_BREAKFAST")
        );

        assertThat(response.status()).isEqualTo("MISSED");
        assertThat(response.outcome()).isEqualTo("SKIP_BREAKFAST");
        assertThat(response.auraDelta()).isEqualTo(-5);
        assertThat(response.aura()).isEqualTo(45);
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
        assertThat(second.aura()).isEqualTo(55);

        Player player = playerRepository.findByClerkUserId("user_act_dup").orElseThrow();
        assertThat(playerDayActivityRepository.findByPlayer_IdOrderByResolvedAtAsc(player.getId())).hasSize(1);
        assertThat(playerStatsRepository.findByPlayerId(player.getId()).orElseThrow().getAura()).isEqualTo(55);
    }

    @Test
    void listActivities_returnsResolvedOnly() {
        Jwt jwt = jwtWith("user_act_list");
        createSave(jwt);

        ActivityListResponse before = playerActivityService.listCurrentDayActivities(jwt);
        assertThat(before.dayNumber()).isEqualTo(1);
        assertThat(before.activities()).isEmpty();

        playerActivityService.resolveActivity(jwt, new ActivityResolveRequest("breakfast", "rice"));

        ActivityListResponse after = playerActivityService.listCurrentDayActivities(jwt);
        assertThat(after.activities()).hasSize(1);
        assertThat(after.activities().get(0).activityId()).isEqualTo(BreakfastOutcome.ACTIVITY_ID);
        assertThat(after.activities().get(0).status()).isEqualTo("COMPLETED");
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
