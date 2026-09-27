package com.uiusimulator.player.repository;

import com.uiusimulator.player.entity.CourseMaterial;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import org.springframework.data.jpa.repository.JpaRepository;

public interface CourseMaterialRepository extends JpaRepository<CourseMaterial, UUID> {

    List<CourseMaterial> findByCourseIdOrderByCreatedAtAsc(String courseId);

    Optional<CourseMaterial> findByIdAndCourseId(UUID id, String courseId);

    long countByCourseIdAndUploadedBy(String courseId, UUID uploadedBy);

    void deleteByUploadedBy(UUID uploadedBy);
}
