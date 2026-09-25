package com.uiusimulator.assessment.entity;

import jakarta.persistence.*;
import java.time.Instant;
import java.util.UUID;

@Entity
@Table(name = "player_course_enrollments", uniqueConstraints = @UniqueConstraint(columnNames = {"player_id", "semester", "course_id"}))
public class PlayerCourseEnrollment {
    @Id private UUID id;
    @Column(name = "player_id", nullable = false) private UUID playerId;
    @Column(nullable = false) private int semester;
    @Column(name = "course_id", nullable = false, length = 32) private String courseId;
    @Enumerated(EnumType.STRING) @Column(nullable = false, length = 32) private CourseStatus status;
    @Column(name = "drop_reason", length = 64) private String dropReason;
    @Column(name = "dropped_at") private Instant droppedAt;
    @Column(name = "created_at", nullable = false) private Instant createdAt;
    @Column(name = "updated_at", nullable = false) private Instant updatedAt;
    protected PlayerCourseEnrollment() {}
    public static PlayerCourseEnrollment active(UUID player, int semester, String course, Instant now) {
        var e = new PlayerCourseEnrollment();
        e.id = UUID.randomUUID(); e.playerId = player; e.semester = semester; e.courseId = course;
        e.status = CourseStatus.ACTIVE; e.createdAt = now; e.updatedAt = now;
        return e;
    }
    public void drop(Instant now) {
        if (status == CourseStatus.DROPPED_CHEATING) return;
        status = CourseStatus.DROPPED_CHEATING; dropReason = "ACADEMIC_MISCONDUCT";
        droppedAt = now; updatedAt = now;
    }
    public CourseStatus getStatus() { return status; }
    public String getCourseId() { return courseId; }
}
