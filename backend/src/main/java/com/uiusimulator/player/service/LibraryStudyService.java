package com.uiusimulator.player.service;

import com.uiusimulator.player.dto.LibraryStudyCompleteRequest;
import com.uiusimulator.player.dto.LibraryStudyResponse;
import com.uiusimulator.player.entity.ActivityStatus;
import com.uiusimulator.player.entity.LibraryStudyDefinition;
import com.uiusimulator.player.entity.Player;
import com.uiusimulator.player.entity.PlayerDayActivity;
import com.uiusimulator.player.entity.PlayerSave;
import com.uiusimulator.player.entity.PlayerStats;
import com.uiusimulator.player.exception.PlayerSaveNotFoundException;
import com.uiusimulator.player.exception.PlayerStatsNotFoundException;
import com.uiusimulator.player.repository.PlayerDayActivityRepository;
import com.uiusimulator.player.repository.PlayerSaveRepository;
import com.uiusimulator.player.repository.PlayerStatsRepository;
import java.time.Instant;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/**
 * Optional once-per-day Library Study. Start is idempotent and grants nothing.
 * Complete applies Academic Reputation from the server score table exactly once.
 * End Day does not miss or penalize this activity.
 */
@Service
public class LibraryStudyService {

    private static final Logger log = LoggerFactory.getLogger(LibraryStudyService.class);

    private final PlayerService playerService;
    private final PlayerSaveRepository playerSaveRepository;
    private final PlayerDayActivityRepository playerDayActivityRepository;
    private final PlayerStatsRepository playerStatsRepository;

    public LibraryStudyService(
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

    @Transactional
    public LibraryStudyResponse start(Jwt jwt) {
        Player player = playerService.getOrProvisionPlayer(jwt);
        PlayerSave save = requireActiveSave(player);
        PlayerStats lockedStats = lockStats(player);

        var existing = playerDayActivityRepository.findByPlayerIdAndActivityIdWithLock(
                player.getId(),
                LibraryStudyDefinition.ACTIVITY_ID
        );
        if (existing.isPresent()) {
            PlayerDayActivity activity = existing.get();
            boolean completed = activity.getStatus() == ActivityStatus.COMPLETED;
            log.info(
                    "Library study start reused for clerkUserId={} day={} status={}",
                    player.getClerkUserId(),
                    save.getCurrentDay(),
                    activity.getStatus()
            );
            return LibraryStudyResponse.of(activity, lockedStats, 0, true, completed);
        }

        PlayerDayActivity created = PlayerDayActivity.startLibraryStudy(player, save.getCurrentDay(), Instant.now());
        playerDayActivityRepository.saveAndFlush(created);
        log.info(
                "Library study started for clerkUserId={} day={}",
                player.getClerkUserId(),
                save.getCurrentDay()
        );
        return LibraryStudyResponse.of(created, lockedStats, 0, false, false);
    }

    @Transactional
    public LibraryStudyResponse complete(Jwt jwt, LibraryStudyCompleteRequest request) {
        int score = request.score();
        int reward = LibraryStudyDefinition.reputationForScore(score);

        Player player = playerService.getOrProvisionPlayer(jwt);
        requireActiveSave(player);
        PlayerStats lockedStats = lockStats(player);

        PlayerDayActivity activity = playerDayActivityRepository
                .findByPlayerIdAndActivityIdWithLock(player.getId(), LibraryStudyDefinition.ACTIVITY_ID)
                .orElseThrow(() -> new IllegalArgumentException("Library study has not been started."));

        if (activity.getStatus() == ActivityStatus.COMPLETED) {
            log.info(
                    "Library study already completed for clerkUserId={} score={} reputationDelta={}",
                    player.getClerkUserId(),
                    activity.getNormalizedScore(),
                    activity.getReputationDelta()
            );
            return LibraryStudyResponse.of(activity, lockedStats, 0, true, true);
        }

        if (activity.getStatus() != ActivityStatus.IN_PROGRESS) {
            throw new IllegalArgumentException("Library study is not an active attempt.");
        }

        int auraBefore = lockedStats.getAura();
        activity.completeLibraryStudy(score, reward, Instant.now());
        playerDayActivityRepository.saveAndFlush(activity);

        if (reward != 0) {
            lockedStats.modifyStats(0, reward);
            playerStatsRepository.saveAndFlush(lockedStats);
        }

        if (lockedStats.getAura() != auraBefore) {
            throw new IllegalStateException("Library study must not change Aura.");
        }

        log.info(
                "Library study completed for clerkUserId={} score={} reputationDelta={} reputation={}",
                player.getClerkUserId(),
                score,
                activity.getReputationDelta(),
                lockedStats.getAcademicReputation()
        );
        return LibraryStudyResponse.of(activity, lockedStats, reward, true, false);
    }

    private PlayerSave requireActiveSave(Player player) {
        PlayerSave save = playerSaveRepository.findByPlayerId(player.getId())
                .orElseThrow(PlayerSaveNotFoundException::new);
        if (!save.isAdmissionCompleted()) {
            throw new IllegalArgumentException("Complete admission before studying at the library.");
        }
        if (save.getCurrentDay() < 1 || save.getCurrentDay() > PlayerSave.MAX_DAY_PER_SEMESTER) {
            throw new IllegalArgumentException("Library study requires a valid gameplay day.");
        }
        return save;
    }

    private PlayerStats lockStats(Player player) {
        return playerStatsRepository.findByPlayerIdWithLock(player.getId())
                .orElseThrow(() -> new PlayerStatsNotFoundException(player.getId()));
    }
}
