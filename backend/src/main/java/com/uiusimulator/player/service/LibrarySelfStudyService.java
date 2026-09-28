package com.uiusimulator.player.service;

import com.uiusimulator.player.dto.LibrarySelfStudyMilestoneRequest;
import com.uiusimulator.player.dto.LibrarySelfStudySessionResponse;
import com.uiusimulator.player.entity.ActivityStatus;
import com.uiusimulator.player.entity.LibrarySelfStudyDefinition;
import com.uiusimulator.player.entity.Player;
import com.uiusimulator.player.entity.PlayerDayActivity;
import com.uiusimulator.player.entity.PlayerRole;
import com.uiusimulator.player.entity.PlayerSave;
import com.uiusimulator.player.entity.PlayerStats;
import com.uiusimulator.player.exception.PlayerSaveNotFoundException;
import com.uiusimulator.player.exception.PlayerStatsNotFoundException;
import com.uiusimulator.player.repository.PlayerDayActivityRepository;
import com.uiusimulator.player.repository.PlayerSaveRepository;
import com.uiusimulator.player.repository.PlayerStatsRepository;
import java.time.Clock;
import java.time.Instant;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/**
 * Optional once-per-day Library Self Study. Start grants nothing.
 * Milestones at 30/60/90 active seconds each add +1 Academic Reputation.
 * End Day does not miss or penalize this activity. Independent from LIBRARY_STUDY.
 */
@Service
public class LibrarySelfStudyService {

    private static final Logger log = LoggerFactory.getLogger(LibrarySelfStudyService.class);

    private final PlayerService playerService;
    private final PlayerSaveRepository playerSaveRepository;
    private final PlayerDayActivityRepository playerDayActivityRepository;
    private final PlayerStatsRepository playerStatsRepository;
    private final Clock clock;

    public LibrarySelfStudyService(
            PlayerService playerService,
            PlayerSaveRepository playerSaveRepository,
            PlayerDayActivityRepository playerDayActivityRepository,
            PlayerStatsRepository playerStatsRepository,
            Clock clock
    ) {
        this.playerService = playerService;
        this.playerSaveRepository = playerSaveRepository;
        this.playerDayActivityRepository = playerDayActivityRepository;
        this.playerStatsRepository = playerStatsRepository;
        this.clock = clock;
    }

    @Transactional
    public LibrarySelfStudySessionResponse start(Jwt jwt) {
        Instant now = clock.instant();
        Player player = playerService.getOrProvisionPlayer(jwt);
        PlayerSave save = requireEligibleSave(player);
        PlayerStats lockedStats = lockStats(player);

        var existing = playerDayActivityRepository.findByPlayerIdAndActivityIdWithLock(
                player.getId(),
                LibrarySelfStudyDefinition.ACTIVITY_ID
        );
        if (existing.isPresent()) {
            PlayerDayActivity activity = existing.get();
            freezeStaleActiveSession(activity, now);
            log.info(
                    "Library self-study start reused for clerkUserId={} day={} status={}",
                    player.getClerkUserId(),
                    save.getCurrentDay(),
                    activity.getStatus()
            );
            return LibrarySelfStudySessionResponse.of(activity, lockedStats, now, 0, 0, true, true);
        }

        PlayerDayActivity created = PlayerDayActivity.startLibrarySelfStudy(player, save.getCurrentDay(), now);
        playerDayActivityRepository.saveAndFlush(created);
        log.info(
                "Library self-study started for clerkUserId={} day={}",
                player.getClerkUserId(),
                save.getCurrentDay()
        );
        return LibrarySelfStudySessionResponse.of(created, lockedStats, now, 0, 0, false);
    }

    @Transactional
    public LibrarySelfStudySessionResponse pause(Jwt jwt) {
        Instant now = clock.instant();
        SessionContext ctx = loadSession(jwt, now, false);
        if (ctx.activity().getStatus() == ActivityStatus.COMPLETED) {
            return LibrarySelfStudySessionResponse.of(ctx.activity(), ctx.stats(), now, 0, 0, true);
        }
        if (!ctx.activity().isSessionActive()) {
            return LibrarySelfStudySessionResponse.of(ctx.activity(), ctx.stats(), now, 0, 0, true);
        }
        ctx.activity().pauseSession(now);
        playerDayActivityRepository.saveAndFlush(ctx.activity());
        return LibrarySelfStudySessionResponse.of(ctx.activity(), ctx.stats(), now, 0, 0, false);
    }

    @Transactional
    public LibrarySelfStudySessionResponse abandon(Jwt jwt) {
        Instant now = clock.instant();
        SessionContext ctx = loadSession(jwt, now, false);
        if (ctx.activity().getStatus() == ActivityStatus.COMPLETED) {
            return LibrarySelfStudySessionResponse.of(ctx.activity(), ctx.stats(), now, 0, 0, true);
        }
        ctx.activity().abandonLibrarySelfStudy(now);
        playerDayActivityRepository.saveAndFlush(ctx.activity());
        log.info(
                "Library self-study abandoned for clerkUserId={} milestone={} reputationDelta={}",
                ctx.player().getClerkUserId(),
                ctx.activity().getMilestoneSeconds(),
                ctx.activity().getReputationDelta()
        );
        return LibrarySelfStudySessionResponse.of(ctx.activity(), ctx.stats(), now, 0, 0, false);
    }

    @Transactional
    public LibrarySelfStudySessionResponse resume(Jwt jwt) {
        Instant now = clock.instant();
        SessionContext ctx = loadInProgressSession(jwt, now);
        if (ctx.activity().isSessionActive()) {
            return LibrarySelfStudySessionResponse.of(ctx.activity(), ctx.stats(), now, 0, 0, true);
        }
        ctx.activity().resumeSession(now);
        playerDayActivityRepository.saveAndFlush(ctx.activity());
        return LibrarySelfStudySessionResponse.of(ctx.activity(), ctx.stats(), now, 0, 0, false);
    }

    @Transactional
    public LibrarySelfStudySessionResponse claimMilestone(Jwt jwt, LibrarySelfStudyMilestoneRequest request) {
        Instant now = clock.instant();
        int milestoneSeconds = request.milestoneSeconds();
        int reputationReward = LibrarySelfStudyDefinition.reputationRewardForMilestone(milestoneSeconds);
        int requiredPrevious = LibrarySelfStudyDefinition.requiredPreviousMilestone(milestoneSeconds);

        SessionContext ctx = loadInProgressSession(jwt, now);
        PlayerDayActivity activity = ctx.activity();
        PlayerStats lockedStats = ctx.stats();

        if (activity.getMilestoneSeconds() >= milestoneSeconds) {
            return LibrarySelfStudySessionResponse.of(activity, lockedStats, now, reputationReward, 0, true);
        }

        if (activity.getMilestoneSeconds() != requiredPrevious) {
            throw new IllegalArgumentException(
                    "Library self-study milestones must be claimed sequentially. Current="
                            + activity.getMilestoneSeconds()
                            + " requested="
                            + milestoneSeconds
            );
        }

        if (!activity.isSessionActive()) {
            throw new IllegalArgumentException("Library self-study is paused or inactive; cannot claim milestones.");
        }

        long requiredMs = milestoneSeconds * 1000L;
        long activeMs = activity.computeActiveElapsedMs(now);
        if (activeMs < requiredMs) {
            throw new IllegalArgumentException(
                    "Library self-study milestone not yet earned. Active elapsed ms="
                            + activeMs
                            + " required="
                            + requiredMs
            );
        }

        int auraBefore = lockedStats.getAura();
        int beforeRep = lockedStats.getAcademicReputation();
        lockedStats.modifyStats(0, reputationReward);
        int appliedRep = lockedStats.getAcademicReputation() - beforeRep;
        if (lockedStats.getAura() != auraBefore) {
            throw new IllegalStateException("Library self-study must not change Aura.");
        }

        activity.applyMilestone(milestoneSeconds, appliedRep, now);
        playerDayActivityRepository.saveAndFlush(activity);
        playerStatsRepository.saveAndFlush(lockedStats);

        log.info(
                "Library self-study milestone claimed clerkUserId={} milestone={} requestedRep={} appliedRep={} reputation={}",
                ctx.player().getClerkUserId(),
                milestoneSeconds,
                reputationReward,
                appliedRep,
                lockedStats.getAcademicReputation()
        );

        return LibrarySelfStudySessionResponse.of(activity, lockedStats, now, reputationReward, appliedRep, false);
    }

    private PlayerSave requireEligibleSave(Player player) {
        PlayerSave save = playerSaveRepository.findByPlayerId(player.getId())
                .orElseThrow(PlayerSaveNotFoundException::new);
        if (!save.isAdmissionCompleted()) {
            throw new IllegalArgumentException("Complete admission before studying at the library.");
        }
        if (save.getRole() != PlayerRole.STUDENT) {
            throw new IllegalArgumentException("Only students can study at library tables.");
        }
        if (save.getCurrentDay() < 1 || save.getCurrentDay() > PlayerSave.MAX_DAY_PER_SEMESTER) {
            throw new IllegalArgumentException("Library self-study requires a valid gameplay day.");
        }
        return save;
    }

    private SessionContext loadInProgressSession(Jwt jwt, Instant now) {
        return loadSession(jwt, now, true);
    }

    private SessionContext loadSession(Jwt jwt, Instant now, boolean requireInProgress) {
        Player player = playerService.getOrProvisionPlayer(jwt);
        requireEligibleSave(player);
        PlayerStats lockedStats = lockStats(player);
        PlayerDayActivity activity = playerDayActivityRepository
                .findByPlayerIdAndActivityIdWithLock(player.getId(), LibrarySelfStudyDefinition.ACTIVITY_ID)
                .orElseThrow(() -> new IllegalArgumentException("Library self-study has not been started."));

        if (requireInProgress && activity.getStatus() == ActivityStatus.COMPLETED) {
            throw new IllegalArgumentException("Library self-study is already completed for today.");
        }
        if (requireInProgress && activity.isTerminal()) {
            throw new IllegalArgumentException(
                    "Library self-study activity already resolved as " + activity.getOutcome() + "."
            );
        }
        return new SessionContext(player, activity, lockedStats);
    }

    private PlayerStats lockStats(Player player) {
        return playerStatsRepository.findByPlayerIdWithLock(player.getId())
                .orElseThrow(() -> new PlayerStatsNotFoundException(player.getId()));
    }

    /**
     * If a session was left active (game closed without pause), freeze the open segment
     * so additional wall-clock after this request cannot keep accruing until resume.
     */
    private void freezeStaleActiveSession(PlayerDayActivity activity, Instant now) {
        if (activity.isSessionActive()) {
            activity.pauseSession(now);
            playerDayActivityRepository.saveAndFlush(activity);
        }
    }

    private record SessionContext(Player player, PlayerDayActivity activity, PlayerStats stats) {
    }
}
