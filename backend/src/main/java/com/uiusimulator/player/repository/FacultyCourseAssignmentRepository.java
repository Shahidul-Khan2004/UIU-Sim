package com.uiusimulator.player.repository;

import com.uiusimulator.player.entity.FacultyCourseAssignment;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import org.springframework.data.jpa.repository.JpaRepository;

public interface FacultyCourseAssignmentRepository extends JpaRepository<FacultyCourseAssignment, UUID> {

    List<FacultyCourseAssignment> findByPlayerIdOrderByCreatedAtAsc(UUID playerId);

    Optional<FacultyCourseAssignment> findByPlayerIdAndCourseId(UUID playerId, String courseId);

    boolean existsByPlayerIdAndCourseId(UUID playerId, String courseId);

    void deleteByPlayerIdAndCourseId(UUID playerId, String courseId);
}
