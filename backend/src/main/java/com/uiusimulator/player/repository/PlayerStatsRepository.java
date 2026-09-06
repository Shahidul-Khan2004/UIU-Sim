package com.uiusimulator.player.repository;

import com.uiusimulator.player.entity.PlayerStats;
import jakarta.persistence.LockModeType;
import java.util.Optional;
import java.util.UUID;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Lock;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

public interface PlayerStatsRepository extends JpaRepository<PlayerStats, UUID> {

    Optional<PlayerStats> findByPlayerId(UUID playerId);

    @Lock(LockModeType.PESSIMISTIC_WRITE)
    @Query("SELECT ps FROM PlayerStats ps WHERE ps.playerId = :playerId")
    Optional<PlayerStats> findByPlayerIdWithLock(@Param("playerId") UUID playerId);

    @Lock(LockModeType.PESSIMISTIC_WRITE)
    @Query("SELECT ps FROM PlayerStats ps JOIN ps.player p WHERE p.clerkUserId = :clerkUserId")
    Optional<PlayerStats> findByClerkUserIdWithLock(@Param("clerkUserId") String clerkUserId);
}
