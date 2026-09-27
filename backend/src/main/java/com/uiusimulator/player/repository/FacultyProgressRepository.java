package com.uiusimulator.player.repository;

import com.uiusimulator.player.entity.FacultyProgress;
import jakarta.persistence.LockModeType;
import java.util.Optional;
import java.util.UUID;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Lock;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

public interface FacultyProgressRepository extends JpaRepository<FacultyProgress, UUID> {

    Optional<FacultyProgress> findByPlayerId(UUID playerId);

    @Lock(LockModeType.PESSIMISTIC_WRITE)
    @Query("SELECT fp FROM FacultyProgress fp WHERE fp.playerId = :playerId")
    Optional<FacultyProgress> findByPlayerIdWithLock(@Param("playerId") UUID playerId);

    void deleteByPlayerId(UUID playerId);
}
