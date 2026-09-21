package com.uiusimulator.player.service;

import com.uiusimulator.player.dto.DayAdvanceRequest;
import com.uiusimulator.player.dto.DayAdvanceResponse;
import com.uiusimulator.player.dto.DayFinalizeResponse;
import com.uiusimulator.player.dto.DaySummaryActivityResponse;
import com.uiusimulator.player.entity.BreakfastOutcome;
import com.uiusimulator.player.entity.Player;
import com.uiusimulator.player.entity.PlayerDayActivity;
import com.uiusimulator.player.entity.PlayerSave;
import com.uiusimulator.player.entity.PlayerStats;
import com.uiusimulator.player.exception.PlayerSaveNotFoundException;
import com.uiusimulator.player.exception.PlayerStatsNotFoundException;
import com.uiusimulator.player.repository.PlayerDayActivityRepository;
import com.uiusimulator.player.repository.PlayerSaveRepository;
import com.uiusimulator.player.repository.PlayerStatsRepository;
import java.util.List;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/**
 * Authoritative day finalization and advancement.
 * Finalize closes unresolved required activities (breakfast → SKIP_BREAKFAST) exactly once.
 * Advance increments current_day with expected-day idempotency and clears current-day activity rows.
 */
@Service
public class PlayerDayService {

    private static final Logger log = LoggerFactory.getLogger(PlayerDayService.class);

    private final PlayerService playerService;
    private final PlayerSaveRepository playerSaveRepository;
    private final PlayerDayActivityRepository playerDayActivityRepository;
    private final PlayerStatsRepository playerStatsRepository;

    public PlayerDayService(
            PlayerService playerService,
            PlayerSaveRepository playerSaveRepository,
            PlayerDayActivityRepository playerDayActivityRepository,
            PlayerStatsRepository playerStatsRepository
    ) {
        this.playerService = playerService;
        this.playerSaveRepository = playerSaveRepository;
        this.playerDayActivityRepository = playerDayActivityRepository;
        this.playerStatsRepository = playerStatsRepository;
    }

    /**
     * Ensures the current day is closed for required activities and returns a summary.
     * Idempotent: if breakfast is already resolved, returns the same persisted outcomes
     * without re-applying Aura.
     */
    @Transactional
    public DayFinalizeResponse finalizeCurrentDay(Jwt jwt) {
        Player player = playerService.getOrProvisionPlayer(jwt);

        PlayerStats lockedStats = playerStatsRepository.findByPlayerIdWithLock(player.getId())
                .orElseThrow(() -> new PlayerStatsNotFoundException(player.getId()));

        PlayerSave lockedSave = playerSaveRepository.findByPlayerIdWithLock(player.getId())
                .orElseThrow(PlayerSaveNotFoundException::new);

        ensureBreakfastResolved(player, lockedSave, lockedStats);

        List<DaySummaryActivityResponse> activities = playerDayActivityRepository
                .findByPlayer_IdOrderByResolvedAtAsc(player.getId())
                .stream()
                .map(DaySummaryActivityResponse::from)
                .toList();

        lockedSave.touchTimestamps();
        playerSaveRepository.saveAndFlush(lockedSave);

        log.info(
                "Day finalized for clerkUserId={} semester={} day={} activities={}",
                player.getClerkUserId(),
                lockedSave.getSemester(),
                lockedSave.getCurrentDay(),
                activities.size()
        );

        return DayFinalizeResponse.of(
                lockedSave.getSemester(),
                lockedSave.getCurrentDay(),
                activities,
                lockedStats.getAura(),
                lockedStats.getAcademicReputation()
        );
    }

    /**
     * Advances from expectedSemester/expectedDay to the next day.
     * Duplicate requests with the same expected day after a successful advance return the
     * already-advanced state and do not increment again.
     */
    @Transactional
    public DayAdvanceResponse advanceDay(Jwt jwt, DayAdvanceRequest request) {
        Player player = playerService.getOrProvisionPlayer(jwt);

        PlayerStats lockedStats = playerStatsRepository.findByPlayerIdWithLock(player.getId())
                .orElseThrow(() -> new PlayerStatsNotFoundException(player.getId()));

        PlayerSave lockedSave = playerSaveRepository.findByPlayerIdWithLock(player.getId())
                .orElseThrow(PlayerSaveNotFoundException::new);

        int expectedSemester = request.expectedSemester();
        int expectedDay = request.expectedDay();

        // Idempotent retry: Day N already became Day N+1.
        if (lockedSave.getSemester() == expectedSemester
                && lockedSave.getCurrentDay() == expectedDay + 1) {
            log.info(
                    "Day advance already applied for clerkUserId={} expectedDay={} currentDay={}",
                    player.getClerkUserId(),
                    expectedDay,
                    lockedSave.getCurrentDay()
            );
            return DayAdvanceResponse.of(lockedSave, lockedStats, true);
        }

        if (lockedSave.getSemester() != expectedSemester || lockedSave.getCurrentDay() != expectedDay) {
            throw new IllegalArgumentException(
                    "Day state mismatch. Expected semester "
                            + expectedSemester
                            + " day "
                            + expectedDay
                            + " but save is semester "
                            + lockedSave.getSemester()
                            + " day "
                            + lockedSave.getCurrentDay()
                            + "."
            );
        }

        if (lockedSave.getCurrentDay() >= PlayerSave.MAX_DAY_PER_SEMESTER) {
            throw new IllegalArgumentException("Semester progression is not available yet.");
        }

        // Safety: close unresolved breakfast before clearing rows.
        ensureBreakfastResolved(player, lockedSave, lockedStats);

        playerDayActivityRepository.deleteByPlayer_Id(player.getId());
        playerDayActivityRepository.flush();

        lockedSave.advanceToNextDay();
        playerSaveRepository.saveAndFlush(lockedSave);

        log.info(
                "Day advanced for clerkUserId={} semester={} newDay={} idCardIssued={}",
                player.getClerkUserId(),
                lockedSave.getSemester(),
                lockedSave.getCurrentDay(),
                lockedSave.isIdCardIssued()
        );

        return DayAdvanceResponse.of(lockedSave, lockedStats, false);
    }

    /**
     * Uses the same BreakfastOutcome.SKIP_BREAKFAST definition as the canteen resolve path.
     * Does not re-apply Aura when a BREAKFAST row already exists.
     */
    private void ensureBreakfastResolved(Player player, PlayerSave save, PlayerStats lockedStats) {
        var existing = playerDayActivityRepository.findByPlayerIdAndActivityIdWithLock(
                player.getId(),
                BreakfastOutcome.ACTIVITY_ID
        );
        if (existing.isPresent()) {
            return;
        }

        BreakfastOutcome missed = BreakfastOutcome.SKIP_BREAKFAST;
        PlayerDayActivity created = PlayerDayActivity.resolve(
                player,
                BreakfastOutcome.ACTIVITY_ID,
                save.getCurrentDay(),
                missed.status(),
                missed.name(),
                missed.auraDelta(),
                missed.reputationDelta()
        );
        playerDayActivityRepository.saveAndFlush(created);

        if (missed.auraDelta() != 0 || missed.reputationDelta() != 0) {
            lockedStats.modifyStats(missed.auraDelta(), missed.reputationDelta());
            playerStatsRepository.saveAndFlush(lockedStats);
        }

        log.info(
                "Auto-missed breakfast on day finalize for clerkUserId={} day={} auraDelta={}",
                player.getClerkUserId(),
                save.getCurrentDay(),
                missed.auraDelta()
        );
    }
}
