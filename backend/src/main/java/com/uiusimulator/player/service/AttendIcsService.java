package com.uiusimulator.player.service;

import com.uiusimulator.player.dto.AttendIcsMilestoneRequest;
import com.uiusimulator.player.dto.AttendIcsSessionResponse;
import com.uiusimulator.player.entity.AttendIcsDefinition;
import com.uiusimulator.player.entity.AttendIcsOutcome;
import com.uiusimulator.player.entity.Player;
import com.uiusimulator.player.entity.PlayerDayActivity;
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
 * Server-authoritative ICS lecture session: start, pause/resume, sequential milestones,
 * early leave, and proxy attendance. Timing uses pause-aware active milliseconds — never
 * trusts client-submitted elapsed times.
 */
@Service
public class AttendIcsService {

    private static final Logger log = LoggerFactory.getLogger(AttendIcsService.class);

    private final PlayerService playerService;
    private final PlayerSaveRepository playerSaveRepository;
    private final PlayerDayActivityRepository playerDayActivityRepository;
    private final PlayerStatsRepository playerStatsRepository;
    private final Clock clock;

    public AttendIcsService(
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
    public AttendIcsSessionResponse startLecture(Jwt jwt) {
        Instant now = clock.instant();
        Player player = playerService.getOrProvisionPlayer(jwt);
        PlayerSave save = requireEligibleSave(player);
        PlayerStats lockedStats = lockStats(player);

        var existing = playerDayActivityRepository.findByPlayerIdAndActivityIdWithLock(
                player.getId(),
                AttendIcsDefinition.ACTIVITY_ID
        );
        if (existing.isPresent()) {
            PlayerDayActivity activity = existing.get();
            freezeStaleActiveSession(activity, now);
            return AttendIcsSessionResponse.of(activity, lockedStats, now, 0, 0, 0, 0, true);
        }

        PlayerDayActivity created = PlayerDayActivity.startAttendIcsSession(player, save.getCurrentDay(), now);
        playerDayActivityRepository.saveAndFlush(created);

        log.info(
                "ICS lecture started for clerkUserId={} day={}",
                player.getClerkUserId(),
                save.getCurrentDay()
        );
        return AttendIcsSessionResponse.of(created, lockedStats, now, 0, 0, 0, 0, false);
    }

    @Transactional
    public AttendIcsSessionResponse pauseLecture(Jwt jwt) {
        Instant now = clock.instant();
        SessionContext ctx = loadInProgressSession(jwt, now);
        if (!ctx.activity().isSessionActive()) {
            return AttendIcsSessionResponse.of(ctx.activity(), ctx.stats(), now, 0, 0, 0, 0, true);
        }
        ctx.activity().pauseSession(now);
        playerDayActivityRepository.saveAndFlush(ctx.activity());
        return AttendIcsSessionResponse.of(ctx.activity(), ctx.stats(), now, 0, 0, 0, 0, false);
    }

    @Transactional
    public AttendIcsSessionResponse resumeLecture(Jwt jwt) {
        Instant now = clock.instant();
        SessionContext ctx = loadInProgressSession(jwt, now);
        if (ctx.activity().isSessionActive()) {
            return AttendIcsSessionResponse.of(ctx.activity(), ctx.stats(), now, 0, 0, 0, 0, true);
        }
        ctx.activity().resumeSession(now);
        playerDayActivityRepository.saveAndFlush(ctx.activity());
        return AttendIcsSessionResponse.of(ctx.activity(), ctx.stats(), now, 0, 0, 0, 0, false);
    }

    @Transactional
    public AttendIcsSessionResponse claimMilestone(Jwt jwt, AttendIcsMilestoneRequest request) {
        Instant now = clock.instant();
        int milestoneSeconds = request.milestoneSeconds();
        int reputationReward = AttendIcsDefinition.reputationRewardForMilestone(milestoneSeconds);
        int requiredPrevious = AttendIcsDefinition.requiredPreviousMilestone(milestoneSeconds);

        SessionContext ctx = loadInProgressSession(jwt, now);
        PlayerDayActivity activity = ctx.activity();
        PlayerStats lockedStats = ctx.stats();

        if (activity.getMilestoneSeconds() >= milestoneSeconds) {
            return AttendIcsSessionResponse.of(activity, lockedStats, now, 0, reputationReward, 0, 0, true);
        }

        if (activity.getMilestoneSeconds() != requiredPrevious) {
            throw new IllegalArgumentException(
                    "ICS milestones must be claimed sequentially. Current="
                            + activity.getMilestoneSeconds()
                            + " requested="
                            + milestoneSeconds
            );
        }

        if (!activity.isSessionActive()) {
            throw new IllegalArgumentException("ICS lecture is paused or inactive; cannot claim milestones.");
        }

        long requiredMs = milestoneSeconds * 1000L;
        long activeMs = activity.computeActiveElapsedMs(now);
        if (activeMs < requiredMs) {
            throw new IllegalArgumentException(
                    "ICS milestone not yet earned. Active elapsed ms="
                            + activeMs
                            + " required="
                            + requiredMs
            );
        }

        int beforeRep = lockedStats.getAcademicReputation();
        lockedStats.modifyStats(0, reputationReward);
        int appliedRep = lockedStats.getAcademicReputation() - beforeRep;

        activity.applyMilestone(milestoneSeconds, reputationReward, now);
        playerDayActivityRepository.saveAndFlush(activity);
        playerStatsRepository.saveAndFlush(lockedStats);

        log.info(
                "ICS milestone claimed clerkUserId={} milestone={} requestedRep={} appliedRep={} cumulativeRep={}",
                ctx.player().getClerkUserId(),
                milestoneSeconds,
                reputationReward,
                appliedRep,
                activity.getReputationDelta()
        );

        return AttendIcsSessionResponse.of(
                activity,
                lockedStats,
                now,
                0,
                reputationReward,
                0,
                appliedRep,
                false
        );
    }

    @Transactional
    public AttendIcsSessionResponse leaveEarly(Jwt jwt) {
        Instant now = clock.instant();
        Player player = playerService.getOrProvisionPlayer(jwt);
        requireEligibleSave(player);
        PlayerStats lockedStats = lockStats(player);

        PlayerDayActivity activity = playerDayActivityRepository
                .findByPlayerIdAndActivityIdWithLock(player.getId(), AttendIcsDefinition.ACTIVITY_ID)
                .orElseThrow(() -> new IllegalArgumentException("ICS lecture has not been started."));

        if (activity.isEarlyLeavePenaltyApplied()
                || AttendIcsOutcome.LEFT_EARLY.name().equals(activity.getOutcome())) {
            return AttendIcsSessionResponse.of(
                    activity,
                    lockedStats,
                    now,
                    0,
                    AttendIcsDefinition.EARLY_LEAVE_REPUTATION_PENALTY,
                    0,
                    0,
                    true
            );
        }

        // Full completion already won the race at exactly 90s.
        if (AttendIcsOutcome.COMPLETED.name().equals(activity.getOutcome())) {
            throw new IllegalArgumentException("ICS lecture already completed; cannot leave early.");
        }

        if (activity.isTerminal()) {
            throw new IllegalArgumentException(
                    "ICS activity already resolved as " + activity.getOutcome() + "."
            );
        }

        int penalty = AttendIcsDefinition.EARLY_LEAVE_REPUTATION_PENALTY;
        int beforeRep = lockedStats.getAcademicReputation();
        lockedStats.modifyStats(0, penalty);
        int appliedRep = lockedStats.getAcademicReputation() - beforeRep;

        activity.applyEarlyLeave(penalty, now);
        playerDayActivityRepository.saveAndFlush(activity);
        playerStatsRepository.saveAndFlush(lockedStats);

        log.info(
                "ICS left early clerkUserId={} milestone={} cumulativeRep={} appliedPenalty={}",
                player.getClerkUserId(),
                activity.getMilestoneSeconds(),
                activity.getReputationDelta(),
                appliedRep
        );

        return AttendIcsSessionResponse.of(activity, lockedStats, now, 0, penalty, 0, appliedRep, false);
    }

    @Transactional
    public AttendIcsSessionResponse punchProxy(Jwt jwt) {
        Instant now = clock.instant();
        Player player = playerService.getOrProvisionPlayer(jwt);
        PlayerSave save = requireEligibleSave(player);
        if (!save.isIdCardIssued()) {
            throw new IllegalArgumentException("An issued university ID card is required for proxy attendance.");
        }

        PlayerStats lockedStats = lockStats(player);
        var existing = playerDayActivityRepository.findByPlayerIdAndActivityIdWithLock(
                player.getId(),
                AttendIcsDefinition.ACTIVITY_ID
        );
        if (existing.isPresent()) {
            PlayerDayActivity activity = existing.get();
            freezeStaleActiveSession(activity, now);
            return AttendIcsSessionResponse.of(
                    activity,
                    lockedStats,
                    now,
                    AttendIcsDefinition.PROXY_AURA_REWARD,
                    0,
                    0,
                    0,
                    true
            );
        }

        int auraReward = AttendIcsDefinition.PROXY_AURA_REWARD;
        int beforeAura = lockedStats.getAura();
        lockedStats.modifyStats(auraReward, 0);
        int appliedAura = lockedStats.getAura() - beforeAura;

        PlayerDayActivity created = PlayerDayActivity.resolve(
                player,
                AttendIcsDefinition.ACTIVITY_ID,
                save.getCurrentDay(),
                AttendIcsOutcome.PROXY.status(),
                AttendIcsOutcome.PROXY.name(),
                auraReward,
                0
        );
        playerDayActivityRepository.saveAndFlush(created);
        playerStatsRepository.saveAndFlush(lockedStats);

        log.info(
                "ICS proxy attendance clerkUserId={} appliedAura={}",
                player.getClerkUserId(),
                appliedAura
        );

        return AttendIcsSessionResponse.of(created, lockedStats, now, auraReward, 0, appliedAura, 0, false);
    }

    /**
     * Auto-resolves ATTEND_ICS as SKIPPED (−5 Academic Reputation) exactly once when eligible
     * and still unresolved. Does not penalize COMPLETED / LEFT_EARLY / PROXY / IN_PROGRESS
     * (IN_PROGRESS is closed as LEFT_EARLY by {@link #ensureResolvedOnFinalize}).
     */
    @Transactional
    public void ensureResolvedOnFinalize(Player player, PlayerSave save, PlayerStats lockedStats) {
        Instant now = clock.instant();

        // Eligibility uses department — load via join-fetch when save.department may be lazy.
        PlayerSave eligibleProbe = playerSaveRepository.findByPlayerId(player.getId()).orElse(save);
        if (!AttendIcsDefinition.isEligible(eligibleProbe)) {
            return;
        }

        var existing = playerDayActivityRepository.findByPlayerIdAndActivityIdWithLock(
                player.getId(),
                AttendIcsDefinition.ACTIVITY_ID
        );

        if (existing.isEmpty()) {
            int penalty = AttendIcsDefinition.SKIPPED_REPUTATION_PENALTY;
            PlayerDayActivity created = PlayerDayActivity.resolve(
                    player,
                    AttendIcsDefinition.ACTIVITY_ID,
                    save.getCurrentDay(),
                    AttendIcsOutcome.SKIPPED.status(),
                    AttendIcsOutcome.SKIPPED.name(),
                    0,
                    penalty
            );
            playerDayActivityRepository.saveAndFlush(created);
            lockedStats.modifyStats(0, penalty);
            playerStatsRepository.saveAndFlush(lockedStats);
            log.info(
                    "Auto-skipped ICS on day finalize for clerkUserId={} day={} reputationDelta={}",
                    player.getClerkUserId(),
                    save.getCurrentDay(),
                    penalty
            );
            return;
        }

        PlayerDayActivity activity = existing.get();
        if (activity.isTerminal()) {
            return;
        }

        // Abandoned mid-lecture: keep milestones, apply early-leave penalty once.
        if (!activity.isEarlyLeavePenaltyApplied()) {
            int penalty = AttendIcsDefinition.EARLY_LEAVE_REPUTATION_PENALTY;
            lockedStats.modifyStats(0, penalty);
            activity.applyEarlyLeave(penalty, now);
            playerDayActivityRepository.saveAndFlush(activity);
            playerStatsRepository.saveAndFlush(lockedStats);
            log.info(
                    "Auto left-early ICS on day finalize for clerkUserId={} milestone={} cumulativeRep={}",
                    player.getClerkUserId(),
                    activity.getMilestoneSeconds(),
                    activity.getReputationDelta()
            );
        }
    }

    private SessionContext loadInProgressSession(Jwt jwt, Instant now) {
        Player player = playerService.getOrProvisionPlayer(jwt);
        requireEligibleSave(player);
        PlayerStats lockedStats = lockStats(player);

        PlayerDayActivity activity = playerDayActivityRepository
                .findByPlayerIdAndActivityIdWithLock(player.getId(), AttendIcsDefinition.ACTIVITY_ID)
                .orElseThrow(() -> new IllegalArgumentException("ICS lecture has not been started."));

        if (activity.isTerminal()) {
            throw new IllegalArgumentException(
                    "ICS activity already resolved as " + activity.getOutcome() + "."
            );
        }

        return new SessionContext(player, activity, lockedStats);
    }

    private PlayerSave requireEligibleSave(Player player) {
        PlayerSave save = playerSaveRepository.findByPlayerId(player.getId())
                .orElseThrow(PlayerSaveNotFoundException::new);

        if (save.getRole() != AttendIcsDefinition.REQUIRED_ROLE) {
            throw new IllegalArgumentException("Only CSE students can resolve Introduction to Computer Science.");
        }
        if (save.getDepartment() == null
                || !AttendIcsDefinition.REQUIRED_DEPARTMENT_CODE.equalsIgnoreCase(save.getDepartment().getCode())) {
            throw new IllegalArgumentException("Only CSE students can resolve Introduction to Computer Science.");
        }
        if (save.getCurrentDay() != AttendIcsDefinition.SCHEDULED_DAY) {
            throw new IllegalArgumentException(
                    "Introduction to Computer Science is not scheduled on day " + save.getCurrentDay() + "."
            );
        }
        return save;
    }

    private PlayerStats lockStats(Player player) {
        return playerStatsRepository.findByPlayerIdWithLock(player.getId())
                .orElseThrow(() -> new PlayerStatsNotFoundException(player.getId()));
    }

    /**
     * If a session was left active (game closed without pause), freeze the open segment
     * so wall-clock while offline cannot earn milestones.
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
