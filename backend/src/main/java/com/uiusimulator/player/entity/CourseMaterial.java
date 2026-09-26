package com.uiusimulator.player.entity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import java.time.Instant;
import java.util.Objects;
import java.util.UUID;

@Entity
@Table(name = "course_materials")
public class CourseMaterial {

    @Id
    @Column(nullable = false, updatable = false)
    private UUID id;

    @Column(name = "course_id", nullable = false, length = 32)
    private String courseId;

    @Column(nullable = false, length = 255)
    private String title;

    @Column(nullable = false, columnDefinition = "TEXT")
    private String url;

    @Column(name = "uploaded_by", nullable = false)
    private UUID uploadedBy;

    @Column(name = "created_at", nullable = false, updatable = false)
    private Instant createdAt;

    protected CourseMaterial() {
    }

    public CourseMaterial(
            UUID id,
            String courseId,
            String title,
            String url,
            UUID uploadedBy,
            Instant createdAt
    ) {
        this.id = Objects.requireNonNull(id, "id must not be null");
        this.courseId = Objects.requireNonNull(courseId, "courseId must not be null");
        this.title = Objects.requireNonNull(title, "title must not be null");
        this.url = Objects.requireNonNull(url, "url must not be null");
        this.uploadedBy = Objects.requireNonNull(uploadedBy, "uploadedBy must not be null");
        this.createdAt = Objects.requireNonNull(createdAt, "createdAt must not be null");
    }

    public static CourseMaterial create(String courseId, String title, String url, UUID uploadedBy) {
        return new CourseMaterial(
                UUID.randomUUID(),
                courseId,
                title,
                url,
                uploadedBy,
                Instant.now()
        );
    }

    public UUID getId() {
        return id;
    }

    public String getCourseId() {
        return courseId;
    }

    public String getTitle() {
        return title;
    }

    public String getUrl() {
        return url;
    }

    public UUID getUploadedBy() {
        return uploadedBy;
    }

    public Instant getCreatedAt() {
        return createdAt;
    }
}
