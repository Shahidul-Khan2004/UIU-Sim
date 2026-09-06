package com.uiusimulator.player.repository;

import com.uiusimulator.player.entity.Player;
import jakarta.persistence.LockModeType;
import java.util.Optional;
import java.util.UUID;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Lock;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

public interface PlayerRepository extends JpaRepository<Player, UUID> {

    Optional<Player> findByClerkUserId(String clerkUserId);

    @Lock(LockModeType.PESSIMISTIC_WRITE)
    @Query("SELECT p FROM Player p WHERE p.clerkUserId = :clerkUserId")
    Optional<Player> findByClerkUserIdWithLock(@Param("clerkUserId") String clerkUserId);
}
