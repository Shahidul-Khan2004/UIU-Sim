package com.uiusimulator.player.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.uiusimulator.player.dto.PlayerSaveCreateRequest;
import com.uiusimulator.player.dto.PlayerSaveStatusResponse;
import com.uiusimulator.player.entity.Department;
import com.uiusimulator.player.entity.Player;
import com.uiusimulator.player.entity.PlayerRole;
import com.uiusimulator.player.entity.PlayerStats;
import com.uiusimulator.player.exception.PlayerSaveAlreadyExistsException;
import com.uiusimulator.player.repository.DepartmentRepository;
import com.uiusimulator.player.repository.PlayerRepository;
import com.uiusimulator.player.repository.PlayerSaveRepository;
import com.uiusimulator.player.repository.PlayerStatsRepository;
import java.util.UUID;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.orm.jpa.DataJpaTest;
import org.springframework.context.annotation.Import;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.test.context.ActiveProfiles;

@DataJpaTest
@ActiveProfiles("test")
@Import({PlayerService.class, PlayerSaveService.class})
class PlayerSaveServiceTest {

    @Autowired
    private PlayerSaveService playerSaveService;

    @Autowired
    private PlayerService playerService;

    @Autowired
    private PlayerRepository playerRepository;

    @Autowired
    private PlayerStatsRepository playerStatsRepository;

    @Autowired
    private PlayerSaveRepository playerSaveRepository;

    @Autowired
    private DepartmentRepository departmentRepository;

    private Department cse;

    @BeforeEach
    void seedDepartments() {
        cse = departmentRepository.saveAndFlush(
                Department.createNew("CSE", "Computer Science and Engineering")
        );
        departmentRepository.saveAndFlush(
                Department.createNew("BBA", "Business Administration")
        );
    }

    @Test
    void getSaveStatus_newPlayer_hasNoSave() {
        Jwt jwt = jwtWith("user_save_none");

        PlayerSaveStatusResponse status = playerSaveService.getSaveStatus(jwt);

        assertThat(status.hasSave()).isFalse();
        assertThat(status.save()).isNull();
        assertThat(playerRepository.findByClerkUserId("user_save_none")).isPresent();
    }

    @Test
    void createSave_persistsJourneyWithDefaults() {
        Jwt jwt = jwtWith("user_save_create");
        PlayerSaveCreateRequest request = new PlayerSaveCreateRequest(
                PlayerRole.STUDENT,
                cse.getId(),
                "22112345"
        );

        PlayerSaveStatusResponse status = playerSaveService.createSave(jwt, request);

        assertThat(status.hasSave()).isTrue();
        assertThat(status.save().role()).isEqualTo("STUDENT");
        assertThat(status.save().department()).isEqualTo("CSE");
        assertThat(status.save().universityId()).isEqualTo("22112345");
        assertThat(status.save().semester()).isEqualTo(1);
        assertThat(status.save().currentDay()).isEqualTo(1);
        assertThat(status.save().admissionCompleted()).isTrue();

        Player player = playerRepository.findByClerkUserId("user_save_create").orElseThrow();
        assertThat(playerSaveRepository.findByPlayerId(player.getId())).isPresent();
    }

    @Test
    void getSaveStatus_afterCreate_returnsSave() {
        Jwt jwt = jwtWith("user_save_get");
        playerSaveService.createSave(
                jwt,
                new PlayerSaveCreateRequest(PlayerRole.FACULTY, cse.getId(), null)
        );

        PlayerSaveStatusResponse status = playerSaveService.getSaveStatus(jwt);

        assertThat(status.hasSave()).isTrue();
        assertThat(status.save().role()).isEqualTo("FACULTY");
        assertThat(status.save().department()).isEqualTo("CSE");
        assertThat(status.save().universityId()).isNull();
    }

    @Test
    void createSave_duplicate_throwsConflict() {
        Jwt jwt = jwtWith("user_save_dup");
        PlayerSaveCreateRequest request = new PlayerSaveCreateRequest(
                PlayerRole.STUDENT,
                cse.getId(),
                "111"
        );
        playerSaveService.createSave(jwt, request);

        assertThatThrownBy(() -> playerSaveService.createSave(jwt, request))
                .isInstanceOf(PlayerSaveAlreadyExistsException.class);
    }

    @Test
    void createSave_unknownDepartment_rejects() {
        Jwt jwt = jwtWith("user_save_bad_dept");

        assertThatThrownBy(() -> playerSaveService.createSave(
                jwt,
                new PlayerSaveCreateRequest(PlayerRole.STUDENT, UUID.randomUUID(), "1")
        )).isInstanceOf(IllegalArgumentException.class)
                .hasMessageContaining("Unknown departmentId");
    }

    @Test
    void deleteSave_removesJourneyKeepsIdentityAndResetsStats() {
        Jwt jwt = jwtWith("user_save_delete");
        playerService.getOrProvisionPlayer(jwt);

        Player player = playerRepository.findByClerkUserId("user_save_delete").orElseThrow();
        PlayerStats stats = playerStatsRepository.findByPlayerId(player.getId()).orElseThrow();
        stats.modifyStats(20, -10);
        stats.consumeInitialIdTutorial();
        playerStatsRepository.saveAndFlush(stats);

        playerSaveService.createSave(
                jwt,
                new PlayerSaveCreateRequest(PlayerRole.STUDENT, cse.getId(), "999")
        );

        PlayerSaveStatusResponse deleted = playerSaveService.deleteSave(jwt);

        assertThat(deleted.hasSave()).isFalse();
        assertThat(playerSaveRepository.findByPlayerId(player.getId())).isEmpty();
        assertThat(playerRepository.findByClerkUserId("user_save_delete")).isPresent();

        PlayerStats reset = playerStatsRepository.findByPlayerId(player.getId()).orElseThrow();
        assertThat(reset.getAura()).isEqualTo(50);
        assertThat(reset.getAcademicReputation()).isEqualTo(50);
        assertThat(reset.isInitialIdTutorialPending()).isTrue();
    }

    @Test
    void deleteSave_whenNoSave_stillResetsStats() {
        Jwt jwt = jwtWith("user_save_delete_empty");
        playerService.getOrProvisionPlayer(jwt);
        Player player = playerRepository.findByClerkUserId("user_save_delete_empty").orElseThrow();
        PlayerStats stats = playerStatsRepository.findByPlayerId(player.getId()).orElseThrow();
        stats.modifyStats(30, 20);
        playerStatsRepository.saveAndFlush(stats);

        PlayerSaveStatusResponse deleted = playerSaveService.deleteSave(jwt);

        assertThat(deleted.hasSave()).isFalse();
        PlayerStats reset = playerStatsRepository.findByPlayerId(player.getId()).orElseThrow();
        assertThat(reset.getAura()).isEqualTo(50);
        assertThat(reset.getAcademicReputation()).isEqualTo(50);
        assertThat(reset.isInitialIdTutorialPending()).isTrue();
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
