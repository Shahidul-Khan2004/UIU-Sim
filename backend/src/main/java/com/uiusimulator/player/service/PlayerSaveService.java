package com.uiusimulator.player.service;

import com.uiusimulator.player.dto.PlayerSaveCreateRequest;
import com.uiusimulator.player.dto.PlayerSaveResponse;
import com.uiusimulator.player.dto.PlayerSaveStatusResponse;
import com.uiusimulator.player.entity.Department;
import com.uiusimulator.player.entity.Player;
import com.uiusimulator.player.entity.PlayerSave;
import com.uiusimulator.player.entity.PlayerStats;
import com.uiusimulator.player.exception.PlayerSaveAlreadyExistsException;
import com.uiusimulator.player.exception.PlayerStatsNotFoundException;
import com.uiusimulator.player.repository.DepartmentRepository;
import com.uiusimulator.player.repository.PlayerSaveRepository;
import com.uiusimulator.player.repository.PlayerStatsRepository;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.dao.DataIntegrityViolationException;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
public class PlayerSaveService {

    private static final Logger log = LoggerFactory.getLogger(PlayerSaveService.class);

    private final PlayerService playerService;
    private final PlayerSaveRepository playerSaveRepository;
    private final DepartmentRepository departmentRepository;
    private final PlayerStatsRepository playerStatsRepository;

    public PlayerSaveService(
            PlayerService playerService,
            PlayerSaveRepository playerSaveRepository,
            DepartmentRepository departmentRepository,
            PlayerStatsRepository playerStatsRepository
    ) {
        this.playerService = playerService;
        this.playerSaveRepository = playerSaveRepository;
        this.departmentRepository = departmentRepository;
        this.playerStatsRepository = playerStatsRepository;
    }

    @Transactional(readOnly = true)
    public PlayerSaveStatusResponse getSaveStatus(Jwt jwt) {
        Player player = playerService.getOrProvisionPlayer(jwt);
        return playerSaveRepository.findByPlayerId(player.getId())
                .map(save -> PlayerSaveStatusResponse.of(PlayerSaveResponse.from(save)))
                .orElseGet(PlayerSaveStatusResponse::none);
    }

    @Transactional
    public PlayerSaveStatusResponse createSave(Jwt jwt, PlayerSaveCreateRequest request) {
        if (request.role() == null) {
            throw new IllegalArgumentException("role must not be null");
        }
        if (request.departmentId() == null) {
            throw new IllegalArgumentException("departmentId must not be null");
        }

        Player player = playerService.getOrProvisionPlayer(jwt);

        if (playerSaveRepository.existsByPlayer_Id(player.getId())) {
            throw new PlayerSaveAlreadyExistsException();
        }

        Department department = departmentRepository.findById(request.departmentId())
                .orElseThrow(() -> new IllegalArgumentException("Unknown departmentId"));

        PlayerSave created = PlayerSave.createAfterAdmission(
                player,
                request.role(),
                department,
                request.universityId()
        );

        try {
            PlayerSave saved = playerSaveRepository.saveAndFlush(created);
            log.info(
                    "Player save created for clerkUserId={} role={} department={}",
                    player.getClerkUserId(),
                    saved.getRole(),
                    saved.getDepartment().getCode()
            );
            return PlayerSaveStatusResponse.of(PlayerSaveResponse.from(saved));
        } catch (DataIntegrityViolationException ex) {
            log.warn("Concurrent save creation rejected for clerkUserId={}", player.getClerkUserId());
            throw new PlayerSaveAlreadyExistsException();
        }
    }

    /**
     * New Game reset: deletes the active journey and resets stats.
     * Does not delete the players identity row or Clerk binding.
     */
    @Transactional
    public PlayerSaveStatusResponse deleteSave(Jwt jwt) {
        Player player = playerService.getOrProvisionPlayer(jwt);

        playerSaveRepository.deleteByPlayer_Id(player.getId());
        playerSaveRepository.flush();

        PlayerStats lockedStats = playerStatsRepository.findByPlayerIdWithLock(player.getId())
                .orElseThrow(() -> new PlayerStatsNotFoundException(player.getId()));
        lockedStats.resetToDefaults();
        playerStatsRepository.saveAndFlush(lockedStats);

        log.info("Player save deleted and stats reset for clerkUserId={}", player.getClerkUserId());
        return PlayerSaveStatusResponse.none();
    }
}
