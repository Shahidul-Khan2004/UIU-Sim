package com.uiusimulator.player.entity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.FetchType;
import jakarta.persistence.Id;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.MapsId;
import jakarta.persistence.OneToOne;
import jakarta.persistence.PostLoad;
import jakarta.persistence.PrePersist;
import jakarta.persistence.Table;
import jakarta.persistence.Transient;
import java.util.Objects;
import java.util.UUID;
import org.springframework.data.domain.Persistable;

@Entity
@Table(name = "player_stats")
public class PlayerStats implements Persistable<UUID> {

    @Id
    @Column(name = "player_id")
    private UUID playerId;

    @MapsId
    @OneToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "player_id", nullable = false)
    private Player player;

    @Column(nullable = false)
    private int aura;

    @Column(name = "academic_reputation", nullable = false)
    private int academicReputation;

    @Column(name = "initial_id_tutorial_pending", nullable = false)
    private boolean initialIdTutorialPending;

    @Transient
    private boolean isNew = true;

    protected PlayerStats() {
    }

    public PlayerStats(Player player, int aura, int academicReputation) {
        this(player, aura, academicReputation, false);
    }

    public PlayerStats(Player player, int aura, int academicReputation, boolean initialIdTutorialPending) {
        this.player = Objects.requireNonNull(player, "player must not be null");
        this.playerId = player.getId();
        this.aura = Math.max(0, Math.min(100, aura));
        this.academicReputation = Math.max(0, Math.min(100, academicReputation));
        this.initialIdTutorialPending = initialIdTutorialPending;
        this.isNew = true;
    }

    public static PlayerStats createDefault(Player player) {
        return new PlayerStats(player, 50, 50, true);
    }

    @Override
    public UUID getId() {
        return playerId;
    }

    @Override
    public boolean isNew() {
        return isNew;
    }

    @PostLoad
    @PrePersist
    void markNotNew() {
        this.isNew = false;
    }

    public void modifyStats(int auraDelta, int academicReputationDelta) {
        this.aura = Math.max(0, Math.min(100, this.aura + auraDelta));
        this.academicReputation = Math.max(0, Math.min(100, this.academicReputation + academicReputationDelta));
    }

    /**
     * Atomically consumes the one-time initial ID tutorial flag.
     * @return true if this call flipped pending from true to false; false if already consumed
     */
    public boolean consumeInitialIdTutorial() {
        if (!initialIdTutorialPending) {
            return false;
        }
        initialIdTutorialPending = false;
        return true;
    }

    /**
     * Resets mutable gameplay stats for New Game while keeping the player identity row.
     */
    public void resetToDefaults() {
        this.aura = 50;
        this.academicReputation = 50;
        this.initialIdTutorialPending = true;
    }

    public UUID getPlayerId() {
        return playerId;
    }

    public Player getPlayer() {
        return player;
    }

    public int getAura() {
        return aura;
    }

    public int getAcademicReputation() {
        return academicReputation;
    }

    public boolean isInitialIdTutorialPending() {
        return initialIdTutorialPending;
    }
}
