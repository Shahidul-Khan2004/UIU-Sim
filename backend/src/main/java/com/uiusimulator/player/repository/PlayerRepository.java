package com.uiusimulator.player.repository;

import com.uiusimulator.player.entity.Player;
import java.util.Optional;
import java.util.UUID;
import org.springframework.data.jpa.repository.JpaRepository;

public interface PlayerRepository extends JpaRepository<Player, UUID> {

    Optional<Player> findByClerkUserId(String clerkUserId);
}
