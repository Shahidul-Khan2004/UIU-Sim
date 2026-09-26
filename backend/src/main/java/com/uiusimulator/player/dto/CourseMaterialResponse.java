package com.uiusimulator.player.dto;

import com.uiusimulator.player.entity.CourseMaterial;
import java.time.Instant;
import java.util.UUID;

public record CourseMaterialResponse(
        UUID id,
        String courseId,
        String title,
        String url,
        UUID uploadedBy,
        Instant createdAt
) {
    public static CourseMaterialResponse from(CourseMaterial material) {
        return new CourseMaterialResponse(
                material.getId(),
                material.getCourseId(),
                material.getTitle(),
                material.getUrl(),
                material.getUploadedBy(),
                material.getCreatedAt()
        );
    }
}
