package com.uiusimulator.player.service;

import com.uiusimulator.player.dto.AttendIcsMilestoneRequest;
import com.uiusimulator.player.dto.AttendIcsSessionResponse;
import com.uiusimulator.player.entity.AttendIcsDefinition;
import com.uiusimulator.player.entity.AttendIcsOutcome;
import com.uiusimulator.player.entity.ClassroomCourseDefinition;
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
 * Server-authoritative classroom lecture sessions. ICS, English, and Discrete Mathematics
 * share this path and are stored as independent activity rows. Timing uses pause-aware
 * active milliseconds and never trusts client-submitted elapsed times or reputation deltas.
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
        return startLecture(jwt, AttendIcsDefinition.ACTIVITY_ID);
    }

    @Transactional
    public AttendIcsSessionResponse startLecture(Jwt jwt, String activityId) {
        return startLecture(jwt, ClassroomCourseDefinition.require(activityId));
    }

    @Transactional
    public AttendIcsSessionResponse pauseLecture(Jwt jwt) {
        return pauseLecture(jwt, AttendIcsDefinition.ACTIVITY_ID);
    }

    @Transactional
    public AttendIcsSessionResponse pauseLecture(Jwt jwt, String activityId) {
        return pauseLecture(jwt, ClassroomCourseDefinition.require(activityId));
    }

    @Transactional
    public AttendIcsSessionResponse resumeLecture(Jwt jwt) {
        return resumeLecture(jwt, AttendIcsDefinition.ACTIVITY_ID);
    }

    @Transactional
    public AttendIcsSessionResponse resumeLecture(Jwt jwt, String activityId) {
        return resumeLecture(jwt, ClassroomCourseDefinition.require(activityId));
    }

    @Transactional
    public AttendIcsSessionResponse claimMilestone(Jwt jwt, AttendIcsMilestoneRequest request) {
        return claimMilestone(jwt, AttendIcsDefinition.ACTIVITY_ID, request);
    }

    @Transactional
    public AttendIcsSessionResponse claimMilestone(Jwt jwt, String activityId, AttendIcsMilestoneRequest request) {
        return claimMilestone(jwt, ClassroomCourseDefinition.require(activityId), request);
    }

    @Transactional
    public AttendIcsSessionResponse leaveEarly(Jwt jwt) {
        return leaveEarly(jwt, AttendIcsDefinition.ACTIVITY_ID);
    }

    @Transactional
    public AttendIcsSessionResponse leaveEarly(Jwt jwt, String activityId) {
        return leaveEarly(jwt, ClassroomCourseDefinition.require(activityId));
    }

    @Transactional
    public AttendIcsSessionResponse punchProxy(Jwt jwt) {
        return punchProxy(jwt, AttendIcsDefinition.ACTIVITY_ID);
    }

    @Transactional
    public AttendIcsSessionResponse punchProxy(Jwt jwt, String activityId) {
        return punchProxy(jwt, ClassroomCourseDefinition.require(activityId));
    }

    private AttendIcsSessionResponse startLecture(Jwt jwt, ClassroomCourseDefinition course) {
        Instant now = clock.instant();
        Player player = playerService.getOrProvisionPlayer(jwt);
        PlayerSave save = requireEligibleSave(player, course);
        PlayerStats lockedStats = lockStats(player);

        var existing = playerDayActivityRepository.findByPlayerIdAndActivityIdWithLock(
                player.getId(),
                course.activityId()
        );
        if (existing.isPresent()) {
            PlayerDayActivity activity = existing.get();
            freezeStaleActiveSession(activity, now);
            return AttendIcsSessionResponse.of(activity, lockedStats, now, 0, 0, 0, 0, true);
        }

        PlayerDayActivity created = PlayerDayActivity.startClassroomSession(
                player,
                course.activityId(),
                save.getCurrentDay(),
                now
        );
        playerDayActivityRepository.saveAndFlush(created);

        log.info(
                "Classroom lecture started activityId={} clerkUserId={} day={}",
                course.activityId(),
                player.getClerkUserId(),
                save.getCurrentDay()
        );
        return AttendIcsSessionResponse.of(created, lockedStats, now, 0, 0, 0, 0, false);
    }

    private AttendIcsSessionResponse pauseLecture(Jwt jwt, ClassroomCourseDefinition course) {
        Instant now = clock.instant();
        SessionContext ctx = loadInProgressSession(jwt, course, now);
        if (!ctx.activity().isSessionActive()) {
            return AttendIcsSessionResponse.of(ctx.activity(), ctx.stats(), now, 0, 0, 0, 0, true);
        }
        ctx.activity().pauseSession(now);
        playerDayActivityRepository.saveAndFlush(ctx.activity());
        return AttendIcsSessionResponse.of(ctx.activity(), ctx.stats(), now, 0, 0, 0, 0, false);
    }

    private AttendIcsSessionResponse resumeLecture(Jwt jwt, ClassroomCourseDefinition course) {
        Instant now = clock.instant();
        SessionContext ctx = loadInProgressSession(jwt, course, now);
        if (ctx.activity().isSessionActive()) {
            return AttendIcsSessionResponse.of(ctx.activity(), ctx.stats(), now, 0, 0, 0, 0, true);
        }
        ctx.activity().resumeSession(now);
        playerDayActivityRepository.saveAndFlush(ctx.activity());
        return AttendIcsSessionResponse.of(ctx.activity(), ctx.stats(), now, 0, 0, 0, 0, false);
    }

    private AttendIcsSessionResponse claimMilestone(
            Jwt jwt,
            ClassroomCourseDefinition course,
            AttendIcsMilestoneRequest request
    ) {
        Instant now = clock.instant();
        int milestoneSeconds = request.milestoneSeconds();
        int reputationReward = ClassroomCourseDefinition.reputationRewardForMilestone(milestoneSeconds);
        int requiredPrevious = ClassroomCourseDefinition.requiredPreviousMilestone(milestoneSeconds);

        SessionContext ctx = loadInProgressSession(jwt, course, now);
        PlayerDayActivity activity = ctx.activity();
        PlayerStats lockedStats = ctx.stats();

        if (activity.getMilestoneSeconds() >= milestoneSeconds) {
            return AttendIcsSessionResponse.of(activity, lockedStats, now, 0, reputationReward, 0, 0, true);
        }

        if (activity.getMilestoneSeconds() != requiredPrevious) {
            throw new IllegalArgumentException(
                    course.courseName()
                            + " milestones must be claimed sequentially. Current="
                            + activity.getMilestoneSeconds()
                            + " requested="
                            + milestoneSeconds
            );
        }

        if (!activity.isSessionActive()) {
            throw new IllegalArgumentException(course.courseName() + " lecture is paused or inactive; cannot claim milestones.");
        }

        long requiredMs = milestoneSeconds * 1000L;
        long activeMs = activity.computeActiveElapsedMs(now);
        if (activeMs < requiredMs) {
            throw new IllegalArgumentException(
                    course.courseName()
                            + " milestone not yet earned. Active elapsed ms="
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
                "Classroom milestone claimed activityId={} clerkUserId={} milestone={} requestedRep={} appliedRep={} cumulativeRep={}",
                course.activityId(),
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

    private AttendIcsSessionResponse leaveEarly(Jwt jwt, ClassroomCourseDefinition course) {
        Instant now = clock.instant();
        Player player = playerService.getOrProvisionPlayer(jwt);
        requireEligibleSave(player, course);
        PlayerStats lockedStats = lockStats(player);

        PlayerDayActivity activity = playerDayActivityRepository
                .findByPlayerIdAndActivityIdWithLock(player.getId(), course.activityId())
                .orElseThrow(() -> new IllegalArgumentException(course.courseName() + " lecture has not been started."));

        if (activity.isEarlyLeavePenaltyApplied()
                || AttendIcsOutcome.LEFT_EARLY.name().equals(activity.getOutcome())) {
            return AttendIcsSessionResponse.of(
                    activity,
                    lockedStats,
                    now,
                    0,
                    ClassroomCourseDefinition.EARLY_LEAVE_REPUTATION_PENALTY,
                    0,
                    0,
                    true
            );
        }

        if (AttendIcsOutcome.COMPLETED.name().equals(activity.getOutcome())) {
            throw new IllegalArgumentException(course.courseName() + " lecture already completed; cannot leave early.");
        }

        if (activity.isTerminal()) {
            throw new IllegalArgumentException(
                    course.courseName() + " activity already resolved as " + activity.getOutcome() + "."
            );
        }

        int penalty = ClassroomCourseDefinition.EARLY_LEAVE_REPUTATION_PENALTY;
        int beforeRep = lockedStats.getAcademicReputation();
        lockedStats.modifyStats(0, penalty);
        int appliedRep = lockedStats.getAcademicReputation() - beforeRep;

        activity.applyEarlyLeave(penalty, now);
        playerDayActivityRepository.saveAndFlush(activity);
        playerStatsRepository.saveAndFlush(lockedStats);

        log.info(
                "Classroom left early activityId={} clerkUserId={} milestone={} cumulativeRep={} appliedPenalty={}",
                course.activityId(),
                player.getClerkUserId(),
                activity.getMilestoneSeconds(),
                activity.getReputationDelta(),
                appliedRep
        );

        return AttendIcsSessionResponse.of(activity, lockedStats, now, 0, penalty, 0, appliedRep, false);
    }

    private AttendIcsSessionResponse punchProxy(Jwt jwt, ClassroomCourseDefinition course) {
        Instant now = clock.instant();
        Player player = playerService.getOrProvisionPlayer(jwt);
        PlayerSave save = requireEligibleSave(player, course);
        if (!save.isIdCardIssued()) {
            throw new IllegalArgumentException("An issued university ID card is required for proxy attendance.");
        }

        PlayerStats lockedStats = lockStats(player);
        var existing = playerDayActivityRepository.findByPlayerIdAndActivityIdWithLock(
                player.getId(),
                course.activityId()
        );
        if (existing.isPresent()) {
            PlayerDayActivity activity = existing.get();
            freezeStaleActiveSession(activity, now);
            return AttendIcsSessionResponse.of(
                    activity,
                    lockedStats,
                    now,
                    ClassroomCourseDefinition.PROXY_AURA_REWARD,
                    0,
                    0,
                    0,
                    true
            );
        }

        int auraReward = ClassroomCourseDefinition.PROXY_AURA_REWARD;
        int beforeAura = lockedStats.getAura();
        lockedStats.modifyStats(auraReward, 0);
        int appliedAura = lockedStats.getAura() - beforeAura;

        PlayerDayActivity created = PlayerDayActivity.resolve(
                player,
                course.activityId(),
                save.getCurrentDay(),
                AttendIcsOutcome.PROXY.status(),
                AttendIcsOutcome.PROXY.name(),
                auraReward,
                0
        );
        playerDayActivityRepository.saveAndFlush(created);
        playerStatsRepository.saveAndFlush(lockedStats);

        log.info(
                "Classroom proxy attendance activityId={} clerkUserId={} appliedAura={}",
                course.activityId(),
                player.getClerkUserId(),
                appliedAura
        );

        return AttendIcsSessionResponse.of(created, lockedStats, now, auraReward, 0, appliedAura, 0, false);
    }

    /**
     * Auto-resolves every scheduled CSE classroom independently.
     * Unresolved lectures become SKIPPED (−5). Abandoned in-progress lectures become LEFT_EARLY.
     * Terminal outcomes are not penalized again.
     */
    @Transactional
    public void ensureResolvedOnFinalize(Player player, PlayerSave save, PlayerStats lockedStats) {
        for (ClassroomCourseDefinition course : ClassroomCourseDefinition.all()) {
            ensureCourseResolvedOnFinalize(player, save, lockedStats, course);
        }
    }

    private void ensureCourseResolvedOnFinalize(
            Player player,
            PlayerSave save,
            PlayerStats lockedStats,
            ClassroomCourseDefinition course
    ) {
        Instant now = clock.instant();

        PlayerSave eligibleProbe = playerSaveRepository.findByPlayerId(player.getId()).orElse(save);
        if (!course.isEligible(eligibleProbe)) {
            return;
        }

        var existing = playerDayActivityRepository.findByPlayerIdAndActivityIdWithLock(
                player.getId(),
                course.activityId()
        );

        if (existing.isEmpty()) {
            int penalty = ClassroomCourseDefinition.SKIPPED_REPUTATION_PENALTY;
            PlayerDayActivity created = PlayerDayActivity.resolve(
                    player,
                    course.activityId(),
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
                    "Auto-skipped classroom on day finalize activityId={} clerkUserId={} day={} reputationDelta={}",
                    course.activityId(),
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

        if (!activity.isEarlyLeavePenaltyApplied()) {
            int penalty = ClassroomCourseDefinition.EARLY_LEAVE_REPUTATION_PENALTY;
            lockedStats.modifyStats(0, penalty);
            activity.applyEarlyLeave(penalty, now);
            playerDayActivityRepository.saveAndFlush(activity);
            playerStatsRepository.saveAndFlush(lockedStats);
            log.info(
                    "Auto left-early classroom on day finalize activityId={} clerkUserId={} milestone={} cumulativeRep={}",
                    course.activityId(),
                    player.getClerkUserId(),
                    activity.getMilestoneSeconds(),
                    activity.getReputationDelta()
            );
        }
    }

    private SessionContext loadInProgressSession(Jwt jwt, ClassroomCourseDefinition course, Instant now) {
        Player player = playerService.getOrProvisionPlayer(jwt);
        requireEligibleSave(player, course);
        PlayerStats lockedStats = lockStats(player);

        PlayerDayActivity activity = playerDayActivityRepository
                .findByPlayerIdAndActivityIdWithLock(player.getId(), course.activityId())
                .orElseThrow(() -> new IllegalArgumentException(course.courseName() + " lecture has not been started."));

        if (activity.isTerminal()) {
            throw new IllegalArgumentException(
                    course.courseName() + " activity already resolved as " + activity.getOutcome() + "."
            );
        }

        return new SessionContext(player, activity, lockedStats);
    }

    private PlayerSave requireEligibleSave(Player player, ClassroomCourseDefinition course) {
        PlayerSave save = playerSaveRepository.findByPlayerId(player.getId())
                .orElseThrow(PlayerSaveNotFoundException::new);

        if (save.getRole() != PlayerRole.STUDENT) {
            throw new IllegalArgumentException("Only CSE students can resolve " + course.courseName() + ".");
        }
        if (save.getDepartment() == null || !"CSE".equalsIgnoreCase(save.getDepartment().getCode())) {
            throw new IllegalArgumentException("Only CSE students can resolve " + course.courseName() + ".");
        }
        if (save.getCurrentDay() != course.scheduledDay()) {
            throw new IllegalArgumentException(
                    course.courseName() + " is not scheduled on day " + save.getCurrentDay() + "."
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
