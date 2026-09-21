package com.uiusimulator.player.service;

import com.uiusimulator.player.dto.ActivityListResponse;
import com.uiusimulator.player.dto.ActivityResolveRequest;
import com.uiusimulator.player.dto.ActivityResolveResponse;
import com.uiusimulator.player.dto.ActivityStateResponse;
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
import java.util.Locale;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
public class PlayerActivityService {

    private static final Logger log = LoggerFactory.getLogger(PlayerActivityService.class);

    private final PlayerService playerService;
    private final PlayerSaveRepository playerSaveRepository;
    private final PlayerDayActivityRepository playerDayActivityRepository;
    private final PlayerStatsRepository playerStatsRepository;

    public PlayerActivityService(
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

    @Transactional(readOnly = true)
    public ActivityListResponse listCurrentDayActivities(Jwt jwt) {
        Player player = playerService.getOrProvisionPlayer(jwt);
        return playerSaveRepository.findByPlayerId(player.getId())
                .map(save -> {
                    List<ActivityStateResponse> activities = playerDayActivityRepository
                            .findByPlayer_IdOrderByResolvedAtAsc(player.getId())
                            .stream()
                            .map(ActivityStateResponse::from)
                            .toList();
                    return ActivityListResponse.of(save.getCurrentDay(), activities);
                })
                .orElseGet(ActivityListResponse::empty);
    }

    /**
     * Atomically records a day activity outcome and applies its Aura/Reputation delta exactly once.
     * Duplicate resolves for the same activity return the existing row without re-applying deltas.
     */
    @Transactional
    public ActivityResolveResponse resolveActivity(Jwt jwt, ActivityResolveRequest request) {
        String activityId = normalize(request.activityId());
        String outcomeRaw = normalize(request.outcome());

        Player player = playerService.getOrProvisionPlayer(jwt);
        PlayerSave save = playerSaveRepository.findByPlayerId(player.getId())
                .orElseThrow(PlayerSaveNotFoundException::new);

        PlayerStats lockedStats = playerStatsRepository.findByPlayerIdWithLock(player.getId())
                .orElseThrow(() -> new PlayerStatsNotFoundException(player.getId()));

        // Serialize on the stats row; then check for an existing activity under the same transaction.
        var existing = playerDayActivityRepository.findByPlayerIdAndActivityIdWithLock(player.getId(), activityId);
        if (existing.isPresent()) {
            log.info(
                    "Activity already resolved for clerkUserId={} activityId={} outcome={}",
                    player.getClerkUserId(),
                    activityId,
                    existing.get().getOutcome()
            );
            return ActivityResolveResponse.of(existing.get(), lockedStats, true);
        }

        BreakfastOutcome breakfastOutcome = resolveBreakfastOutcome(activityId, outcomeRaw);

        PlayerDayActivity created = PlayerDayActivity.resolve(
                player,
                activityId,
                save.getCurrentDay(),
                breakfastOutcome.status(),
                breakfastOutcome.name(),
                breakfastOutcome.auraDelta(),
                breakfastOutcome.reputationDelta()
        );

        // player_stats row lock above serializes concurrent resolves for this player,
        // so the unique (player_id, activity_id) insert cannot double-apply Aura.
        playerDayActivityRepository.saveAndFlush(created);

        if (breakfastOutcome.auraDelta() != 0 || breakfastOutcome.reputationDelta() != 0) {
            lockedStats.modifyStats(breakfastOutcome.auraDelta(), breakfastOutcome.reputationDelta());
            playerStatsRepository.saveAndFlush(lockedStats);
        }

        log.info(
                "Activity resolved for clerkUserId={} activityId={} outcome={} status={} auraDelta={} aura={}",
                player.getClerkUserId(),
                activityId,
                breakfastOutcome.name(),
                breakfastOutcome.status(),
                breakfastOutcome.auraDelta(),
                lockedStats.getAura()
        );

        return ActivityResolveResponse.of(created, lockedStats, false);
    }

    private static BreakfastOutcome resolveBreakfastOutcome(String activityId, String outcomeRaw) {
        if (!BreakfastOutcome.ACTIVITY_ID.equals(activityId)) {
            throw new IllegalArgumentException("Unsupported activityId: " + activityId);
        }

        try {
            return BreakfastOutcome.valueOf(outcomeRaw);
        } catch (IllegalArgumentException ex) {
            throw new IllegalArgumentException("Unsupported breakfast outcome: " + outcomeRaw);
        }
    }

    private static String normalize(String value) {
        if (value == null) {
            return "";
        }
        return value.trim().toUpperCase(Locale.ROOT);
    }
}
