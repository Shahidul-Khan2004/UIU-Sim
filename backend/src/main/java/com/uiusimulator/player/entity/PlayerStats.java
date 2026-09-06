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

    @Transient
    private boolean isNew = true;

    protected PlayerStats() {
    }

    public PlayerStats(Player player, int aura, int academicReputation) {
        this.player = Objects.requireNonNull(player, "player must not be null");
        this.playerId = player.getId();
        this.aura = Math.max(0, Math.min(100, aura));
        this.academicReputation = Math.max(0, Math.min(100, academicReputation));
        this.isNew = true;
    }

    public static PlayerStats createDefault(Player player) {
        return new PlayerStats(player, 50, 50);
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
}
