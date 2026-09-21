package com.uiusimulator.player.entity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.FetchType;
import jakarta.persistence.Id;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.ManyToOne;
import jakarta.persistence.OneToOne;
import jakarta.persistence.Table;
import java.time.Instant;
import java.util.Objects;
import java.util.UUID;

@Entity
@Table(name = "player_saves")
public class PlayerSave {

    /** MVP semester length before semester rollover is implemented. */
    public static final int MAX_DAY_PER_SEMESTER = 6;

    @Id
    @Column(nullable = false, updatable = false)
    private UUID id;

    @OneToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "player_id", nullable = false, unique = true)
    private Player player;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false, length = 32)
    private PlayerRole role;

    @Column(name = "player_name", length = 128)
    private String playerName;

    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "department_id", nullable = false)
    private Department department;

    @Column(name = "university_id", length = 64)
    private String universityId;

    @Column(nullable = false)
    private int semester;

    @Column(name = "current_day", nullable = false)
    private int currentDay;

    @Column(name = "admission_completed", nullable = false)
    private boolean admissionCompleted;

    @Column(name = "id_card_issued", nullable = false)
    private boolean idCardIssued;

    @Column(name = "last_saved_at")
    private Instant lastSavedAt;

    @Column(name = "created_at", nullable = false, updatable = false)
    private Instant createdAt;

    @Column(name = "updated_at", nullable = false)
    private Instant updatedAt;

    protected PlayerSave() {
    }

    public PlayerSave(
            UUID id,
            Player player,
            PlayerRole role,
            String playerName,
            Department department,
            String universityId,
            int semester,
            int currentDay,
            boolean admissionCompleted,
            boolean idCardIssued,
            Instant lastSavedAt,
            Instant createdAt,
            Instant updatedAt
    ) {
        this.id = id;
        this.player = Objects.requireNonNull(player, "player must not be null");
        this.role = Objects.requireNonNull(role, "role must not be null");
        this.playerName = blankToNull(playerName);
        this.department = Objects.requireNonNull(department, "department must not be null");
        this.universityId = blankToNull(universityId);
        this.semester = semester;
        this.currentDay = currentDay;
        this.admissionCompleted = admissionCompleted;
        this.idCardIssued = idCardIssued;
        this.lastSavedAt = lastSavedAt;
        this.createdAt = createdAt;
        this.updatedAt = updatedAt;
    }

    /**
     * Creates a new university journey after admission.
     * Defaults: semester=1, currentDay=1, admissionCompleted=true, idCardIssued=true.
     */
    public static PlayerSave createAfterAdmission(
            Player player,
            PlayerRole role,
            String playerName,
            Department department,
            String universityId
    ) {
        Instant now = Instant.now();
        return new PlayerSave(
                UUID.randomUUID(),
                player,
                role,
                playerName,
                department,
                universityId,
                1,
                1,
                true,
                true,
                now,
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

    public UUID getPlayerId() {
        return player != null ? player.getId() : null;
    }

    public PlayerRole getRole() {
        return role;
    }

    public String getPlayerName() {
        return playerName;
    }

    public Department getDepartment() {
        return department;
    }

    public String getUniversityId() {
        return universityId;
    }

    public int getSemester() {
        return semester;
    }

    public int getCurrentDay() {
        return currentDay;
    }

    public boolean isAdmissionCompleted() {
        return admissionCompleted;
    }

    public boolean isIdCardIssued() {
        return idCardIssued;
    }

    public Instant getLastSavedAt() {
        return lastSavedAt;
    }

    public Instant getCreatedAt() {
        return createdAt;
    }

    public Instant getUpdatedAt() {
        return updatedAt;
    }

    /**
     * Advances to the next gameplay day within the current semester.
     * Refuses Day 7+ until semester rollover is implemented.
     */
    public void advanceToNextDay() {
        if (currentDay >= MAX_DAY_PER_SEMESTER) {
            throw new IllegalArgumentException("Semester progression is not available yet.");
        }
        this.currentDay += 1;
        touchTimestamps();
    }

    public void touchTimestamps() {
        Instant now = Instant.now();
        this.updatedAt = now;
        this.lastSavedAt = now;
    }

    private static String blankToNull(String value) {
        if (value == null || value.isBlank()) {
            return null;
        }
        return value.trim();
    }
}
