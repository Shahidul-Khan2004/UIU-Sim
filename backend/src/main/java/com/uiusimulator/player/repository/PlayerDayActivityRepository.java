package com.uiusimulator.player.repository;

import com.uiusimulator.player.entity.PlayerDayActivity;
import jakarta.persistence.LockModeType;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Lock;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

public interface PlayerDayActivityRepository extends JpaRepository<PlayerDayActivity, UUID> {

    List<PlayerDayActivity> findByPlayer_IdOrderByResolvedAtAsc(UUID playerId);

    Optional<PlayerDayActivity> findByPlayer_IdAndActivityId(UUID playerId, String activityId);

    @Lock(LockModeType.PESSIMISTIC_WRITE)
    @Query("SELECT a FROM PlayerDayActivity a WHERE a.player.id = :playerId AND a.activityId = :activityId")
    Optional<PlayerDayActivity> findByPlayerIdAndActivityIdWithLock(
            @Param("playerId") UUID playerId,
            @Param("activityId") String activityId
    );

    void deleteByPlayer_Id(UUID playerId);
}
