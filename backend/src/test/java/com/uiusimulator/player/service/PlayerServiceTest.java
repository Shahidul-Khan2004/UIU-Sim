package com.uiusimulator.player.service;

import static org.assertj.core.api.Assertions.assertThat;

import com.uiusimulator.player.dto.PlayerResponse;
import com.uiusimulator.player.entity.Player;
import com.uiusimulator.player.repository.PlayerRepository;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.orm.jpa.DataJpaTest;
import org.springframework.context.annotation.Import;
import org.springframework.test.context.ActiveProfiles;

@DataJpaTest
@ActiveProfiles("test")
@Import(PlayerService.class)
class PlayerServiceTest {

    @Autowired
    private PlayerService playerService;

    @Autowired
    private PlayerRepository playerRepository;

    @Test
    void findOrCreate_createsNewPlayer() {
        PlayerResponse response = playerService.findOrCreateFromClerk(
                "user_new",
                "new@uiu.edu",
                "newbie"
        );

        assertThat(response.clerkUserId()).isEqualTo("user_new");
        assertThat(response.email()).isEqualTo("new@uiu.edu");
        assertThat(playerRepository.findByClerkUserId("user_new")).isPresent();
    }

    @Test
    void findOrCreate_existingPlayer_updatesLastLogin() {
        Player first = Player.createNew("user_old", "old@uiu.edu", "veteran");
        playerRepository.save(first);

        PlayerResponse response = playerService.findOrCreateFromClerk(
                "user_old",
                "old@uiu.edu",
                "veteran"
        );

        assertThat(response.id()).isEqualTo(first.getId());
        assertThat(response.lastLogin()).isNotNull();
        assertThat(playerRepository.count()).isEqualTo(1);
    }
}
