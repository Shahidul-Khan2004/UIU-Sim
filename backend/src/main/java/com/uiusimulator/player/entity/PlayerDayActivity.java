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
        this.resolvedAt = resolvedAt;
        this.createdAt = createdAt;
    }

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
                now,
                now
        );
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

    public Instant getResolvedAt() {
        return resolvedAt;
    }

    public Instant getCreatedAt() {
        return createdAt;
    }
}
