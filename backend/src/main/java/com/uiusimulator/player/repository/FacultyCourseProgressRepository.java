package com.uiusimulator.player.repository;

import com.uiusimulator.player.entity.FacultyCourseProgress;
import jakarta.persistence.LockModeType;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Lock;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

public interface FacultyCourseProgressRepository extends JpaRepository<FacultyCourseProgress, UUID> {

    List<FacultyCourseProgress> findByPlayerId(UUID playerId);

    Optional<FacultyCourseProgress> findByPlayerIdAndCourseCode(UUID playerId, String courseCode);

    void deleteByPlayerId(UUID playerId);

    @Lock(LockModeType.PESSIMISTIC_WRITE)
    @Query("SELECT p FROM FacultyCourseProgress p WHERE p.playerId = :playerId AND p.courseCode = :courseCode")
    Optional<FacultyCourseProgress> findByPlayerIdAndCourseCodeWithLock(
            @Param("playerId") UUID playerId,
            @Param("courseCode") String courseCode
    );
}
