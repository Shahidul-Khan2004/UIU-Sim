package com.uiusimulator.player.repository;

import com.uiusimulator.player.entity.PlayerSave;
import jakarta.persistence.LockModeType;
import java.util.Optional;
import java.util.UUID;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Lock;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

public interface PlayerSaveRepository extends JpaRepository<PlayerSave, UUID> {

    @Query("SELECT ps FROM PlayerSave ps JOIN FETCH ps.department WHERE ps.player.id = :playerId")
    Optional<PlayerSave> findByPlayerId(@Param("playerId") UUID playerId);

    @Lock(LockModeType.PESSIMISTIC_WRITE)
    @Query("SELECT ps FROM PlayerSave ps WHERE ps.player.id = :playerId")
    Optional<PlayerSave> findByPlayerIdWithLock(@Param("playerId") UUID playerId);

    boolean existsByPlayer_Id(UUID playerId);

    void deleteByPlayer_Id(UUID playerId);
}
