package com.uiusimulator.advisor.service;

import com.uiusimulator.advisor.model.AdvisorStudentContext;
import com.uiusimulator.advisor.model.StudentStandingBand;
import com.uiusimulator.player.entity.Player;
import com.uiusimulator.player.entity.PlayerStats;
import com.uiusimulator.player.exception.PlayerStatsNotFoundException;
import com.uiusimulator.player.repository.PlayerStatsRepository;
import com.uiusimulator.player.service.PlayerService;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
public class AdvisorStudentContextResolver {

    private final PlayerService playerService;
    private final PlayerStatsRepository playerStatsRepository;

    public AdvisorStudentContextResolver(PlayerService playerService, PlayerStatsRepository playerStatsRepository) {
        this.playerService = playerService;
        this.playerStatsRepository = playerStatsRepository;
    }

    /**
     * Short-lived database read transaction that loads player and stats,
     * constructs the immutable AdvisorStudentContext, and completes before any network calls.
     */
    @Transactional(readOnly = true)
    public AdvisorStudentContext resolveStudentContext(Jwt jwt) {
        if (jwt == null || jwt.getSubject() == null || jwt.getSubject().isBlank()) {
            throw new IllegalArgumentException("Authenticated user context is missing.");
        }

        Player player = playerService.getOrProvisionPlayer(jwt);
        PlayerStats stats = playerStatsRepository.findByPlayerId(player.getId())
                .orElseThrow(() -> new PlayerStatsNotFoundException(player.getId()));

        int aura = stats.getAura();
        int academicReputation = stats.getAcademicReputation();

        StudentStandingBand auraBand = StudentStandingBand.fromStat(aura);
        StudentStandingBand academicBand = StudentStandingBand.fromStat(academicReputation);

        return new AdvisorStudentContext(aura, academicReputation, auraBand, academicBand);
    }
}
