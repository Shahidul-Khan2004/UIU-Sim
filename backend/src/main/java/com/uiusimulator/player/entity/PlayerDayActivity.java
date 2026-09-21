package com.uiusimulator.player.entity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.FetchType;
import jakarta.persistence.Id;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.ManyToOne;
import jakarta.persistence.Table;
import java.time.Instant;
import java.util.Objects;
import java.util.UUID;

@Entity
@Table(name = "player_day_activities")
public class PlayerDayActivity {

    @Id
    @Column(nullable = false, updatable = false)
    private UUID id;

    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "player_id", nullable = false)
    private Player player;

    @Column(name = "activity_id", nullable = false, length = 64)
    private String activityId;

    @Column(name = "day_number", nullable = false)
    private int dayNumber;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false, length = 32)
    private ActivityStatus status;

    @Column(nullable = false, length = 64)
    private String outcome;

    @Column(name = "aura_delta", nullable = false)
    private int auraDelta;

    @Column(name = "reputation_delta", nullable = false)
    private int reputationDelta;

    @Column(name = "milestone_seconds", nullable = false)
    private int milestoneSeconds;

    @Column(name = "session_started_at")
    private Instant sessionStartedAt;

    @Column(name = "session_paused_at")
    private Instant sessionPausedAt;

    @Column(name = "accumulated_active_ms", nullable = false)
    private long accumulatedActiveMs;

    @Column(name = "early_leave_penalty_applied", nullable = false)
    private boolean earlyLeavePenaltyApplied;

    @Column(name = "resolved_at", nullable = false)
    private Instant resolvedAt;

    @Column(name = "created_at", nullable = false, updatable = false)
    private Instant createdAt;

    protected PlayerDayActivity() {
    }

    public PlayerDayActivity(
            UUID id,
            Player player,
            String activityId,
            int dayNumber,
            ActivityStatus status,
            String outcome,
            int auraDelta,
            int reputationDelta,
            int milestoneSeconds,
            Instant sessionStartedAt,
            Instant sessionPausedAt,
            long accumulatedActiveMs,
            boolean earlyLeavePenaltyApplied,
            Instant resolvedAt,
            Instant createdAt
    ) {
        this.id = id;
        this.player = Objects.requireNonNull(player, "player must not be null");
        this.activityId = Objects.requireNonNull(activityId, "activityId must not be null");
        this.dayNumber = dayNumber;
        this.status = Objects.requireNonNull(status, "status must not be null");
        this.outcome = Objects.requireNonNull(outcome, "outcome must not be null");
        this.auraDelta = auraDelta;
        this.reputationDelta = reputationDelta;
        this.milestoneSeconds = milestoneSeconds;
        this.sessionStartedAt = sessionStartedAt;
        this.sessionPausedAt = sessionPausedAt;
        this.accumulatedActiveMs = accumulatedActiveMs;
        this.earlyLeavePenaltyApplied = earlyLeavePenaltyApplied;
        this.resolvedAt = resolvedAt;
        this.createdAt = createdAt;
    }

    /**
     * Terminal resolve factory for breakfast / ID card / ICS skip / proxy style outcomes.
     */
    public static PlayerDayActivity resolve(
            Player player,
            String activityId,
            int dayNumber,
            ActivityStatus status,
            String outcome,
            int auraDelta,
            int reputationDelta
    ) {
        Instant now = Instant.now();
        return new PlayerDayActivity(
                UUID.randomUUID(),
                player,
                activityId,
                dayNumber,
                status,
                outcome,
                auraDelta,
                reputationDelta,
                0,
                null,
                null,
                0L,
                false,
                now,
                now
        );
    }

    public static PlayerDayActivity startClassroomSession(
            Player player,
            String activityId,
            int dayNumber,
            Instant now
    ) {
        return new PlayerDayActivity(
                UUID.randomUUID(),
                player,
                activityId,
                dayNumber,
                AttendIcsOutcome.ATTENDING.status(),
                AttendIcsOutcome.ATTENDING.name(),
                0,
                0,
                0,
                now,
                null,
                0L,
                false,
                now,
                now
        );
    }

    public static PlayerDayActivity startAttendIcsSession(Player player, int dayNumber, Instant now) {
        return startClassroomSession(player, AttendIcsDefinition.ACTIVITY_ID, dayNumber, now);
    }

    public boolean isTerminal() {
        return status != ActivityStatus.IN_PROGRESS;
    }

    public boolean isSessionActive() {
        return status == ActivityStatus.IN_PROGRESS && sessionPausedAt == null;
    }

    /**
     * Active lecture time: accumulated paused segments plus open segment if not paused.
     */
    public long computeActiveElapsedMs(Instant now) {
        long total = accumulatedActiveMs;
        if (status == ActivityStatus.IN_PROGRESS && sessionPausedAt == null && sessionStartedAt != null) {
            Instant segmentStart = sessionStartedAt;
            // After a resume, sessionStartedAt is updated to the resume instant.
            total += Math.max(0L, now.toEpochMilli() - segmentStart.toEpochMilli());
        }
        return total;
    }

    public void pauseSession(Instant now) {
        if (status != ActivityStatus.IN_PROGRESS || sessionPausedAt != null) {
            return;
        }
        if (sessionStartedAt != null) {
            accumulatedActiveMs += Math.max(0L, now.toEpochMilli() - sessionStartedAt.toEpochMilli());
        }
        sessionPausedAt = now;
        resolvedAt = now;
    }

    public void resumeSession(Instant now) {
        if (status != ActivityStatus.IN_PROGRESS || sessionPausedAt == null) {
            return;
        }
        sessionPausedAt = null;
        sessionStartedAt = now;
        resolvedAt = now;
    }

    public void applyMilestone(int milestoneSeconds, int reputationReward, Instant now) {
        this.milestoneSeconds = milestoneSeconds;
        this.reputationDelta += reputationReward;
        this.resolvedAt = now;
        if (milestoneSeconds >= AttendIcsDefinition.MILESTONE_90_SECONDS) {
            this.status = AttendIcsOutcome.COMPLETED.status();
            this.outcome = AttendIcsOutcome.COMPLETED.name();
            // Freeze timing so reconnects cannot continue accruing.
            if (sessionPausedAt == null && sessionStartedAt != null) {
                accumulatedActiveMs += Math.max(0L, now.toEpochMilli() - sessionStartedAt.toEpochMilli());
            }
            sessionPausedAt = now;
        }
    }

    public void applyEarlyLeave(int reputationPenalty, Instant now) {
        pauseSession(now);
        this.status = AttendIcsOutcome.LEFT_EARLY.status();
        this.outcome = AttendIcsOutcome.LEFT_EARLY.name();
        this.reputationDelta += reputationPenalty;
        this.earlyLeavePenaltyApplied = true;
        this.resolvedAt = now;
    }

    public void applyProxy(int auraReward, Instant now) {
        this.status = AttendIcsOutcome.PROXY.status();
        this.outcome = AttendIcsOutcome.PROXY.name();
        this.auraDelta += auraReward;
        this.resolvedAt = now;
    }

    public void applySkipped(int reputationPenalty, Instant now) {
        this.status = AttendIcsOutcome.SKIPPED.status();
        this.outcome = AttendIcsOutcome.SKIPPED.name();
        this.reputationDelta += reputationPenalty;
        this.resolvedAt = now;
    }

    public UUID getId() {
        return id;
    }

    public Player getPlayer() {
        return player;
    }

    public String getActivityId() {
        return activityId;
    }

    public int getDayNumber() {
        return dayNumber;
    }

    public ActivityStatus getStatus() {
        return status;
    }

    public String getOutcome() {
        return outcome;
    }

    public int getAuraDelta() {
        return auraDelta;
    }

    public int getReputationDelta() {
        return reputationDelta;
    }

    public int getMilestoneSeconds() {
        return milestoneSeconds;
    }

    public Instant getSessionStartedAt() {
        return sessionStartedAt;
    }

    public Instant getSessionPausedAt() {
        return sessionPausedAt;
    }

    public long getAccumulatedActiveMs() {
        return accumulatedActiveMs;
    }

    public boolean isEarlyLeavePenaltyApplied() {
        return earlyLeavePenaltyApplied;
    }

    public Instant getResolvedAt() {
        return resolvedAt;
    }

    public Instant getCreatedAt() {
        return createdAt;
    }
}
