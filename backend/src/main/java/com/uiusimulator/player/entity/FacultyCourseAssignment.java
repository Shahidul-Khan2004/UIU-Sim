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
        name = "faculty_course_assignments",
        uniqueConstraints = @UniqueConstraint(columnNames = {"player_id", "course_id"})
)
public class FacultyCourseAssignment {

    @Id
    @Column(nullable = false, updatable = false)
    private UUID id;

    @Column(name = "player_id", nullable = false)
    private UUID playerId;

    @Column(name = "course_id", nullable = false, length = 32)
    private String courseId;

    @Column(name = "created_at", nullable = false, updatable = false)
    private Instant createdAt;

    protected FacultyCourseAssignment() {
    }

    public FacultyCourseAssignment(UUID id, UUID playerId, String courseId, Instant createdAt) {
        this.id = Objects.requireNonNull(id, "id must not be null");
        this.playerId = Objects.requireNonNull(playerId, "playerId must not be null");
        this.courseId = Objects.requireNonNull(courseId, "courseId must not be null");
        this.createdAt = Objects.requireNonNull(createdAt, "createdAt must not be null");
    }

    public static FacultyCourseAssignment assign(UUID playerId, String courseId) {
        return new FacultyCourseAssignment(UUID.randomUUID(), playerId, courseId, Instant.now());
    }

    public UUID getId() {
        return id;
    }

    public UUID getPlayerId() {
        return playerId;
    }

    public String getCourseId() {
        return courseId;
    }

    public Instant getCreatedAt() {
        return createdAt;
    }
}
