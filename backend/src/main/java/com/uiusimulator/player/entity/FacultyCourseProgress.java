package com.uiusimulator.player.entity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import jakarta.persistence.UniqueConstraint;
import java.time.Instant;
import java.util.Objects;
import java.util.UUID;

@Entity
@Table(
        name = "faculty_course_progress",
        uniqueConstraints = @UniqueConstraint(columnNames = {"player_id", "course_code"})
)
public class FacultyCourseProgress {

    @Id
    @Column(nullable = false, updatable = false)
    private UUID id;

    @Column(name = "player_id", nullable = false)
    private UUID playerId;

    @Column(name = "course_code", nullable = false, length = 32)
    private String courseCode;

    @Column(nullable = false)
    private boolean completed;

    @Column(name = "completed_at")
    private Instant completedAt;

    protected FacultyCourseProgress() {
    }

    public FacultyCourseProgress(
            UUID id,
            UUID playerId,
            String courseCode,
            boolean completed,
            Instant completedAt
    ) {
        this.id = Objects.requireNonNull(id, "id must not be null");
        this.playerId = Objects.requireNonNull(playerId, "playerId must not be null");
        this.courseCode = Objects.requireNonNull(courseCode, "courseCode must not be null");
        this.completed = completed;
        this.completedAt = completedAt;
    }

    public static FacultyCourseProgress start(UUID playerId, String courseCode) {
        return new FacultyCourseProgress(UUID.randomUUID(), playerId, courseCode, false, null);
    }

    public boolean markCompleted() {
        if (completed) {
            return false;
        }
        completed = true;
        completedAt = Instant.now();
        return true;
    }

    public UUID getId() {
        return id;
    }

    public UUID getPlayerId() {
        return playerId;
    }

    public String getCourseCode() {
        return courseCode;
    }

    public boolean isCompleted() {
        return completed;
    }

    public Instant getCompletedAt() {
        return completedAt;
    }
}
