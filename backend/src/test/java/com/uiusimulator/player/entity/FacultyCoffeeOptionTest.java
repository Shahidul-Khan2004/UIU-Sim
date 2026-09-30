package com.uiusimulator.player.entity;

import static org.assertj.core.api.Assertions.assertThat;

import java.util.UUID;
import org.junit.jupiter.api.Test;

class FacultyCoffeeOptionTest {

    @Test
    void rewardsAreExclusiveTotals() {
        assertThat(FacultyCoffeeOption.CAPPUCCINO.reputationReward()).isEqualTo(1);
        assertThat(FacultyCoffeeOption.CAPPUCCINO_COOKIE.reputationReward()).isEqualTo(2);
        assertThat(FacultyCoffeeOption.CAPPUCCINO_COOKIE_BROWNIE.reputationReward()).isEqualTo(3);
    }

    @Test
    void claimCoffeeForDayIsIdempotentPerDay() {
        Player player = Player.createNew("user_coffee_entity", null, null);
        try {
            var idField = Player.class.getDeclaredField("id");
            idField.setAccessible(true);
            idField.set(player, UUID.randomUUID());
        } catch (ReflectiveOperationException ex) {
            throw new AssertionError(ex);
        }

        FacultyProgress progress = FacultyProgress.createDefault(player);
        assertThat(progress.claimCoffeeForDay(1, FacultyCoffeeOption.CAPPUCCINO)).isTrue();
        assertThat(progress.getReputation()).isEqualTo(51);
        assertThat(progress.getCoffeeClaimedDay()).isEqualTo(1);
        assertThat(progress.getCoffeeOption()).isEqualTo("CAPPUCCINO");

        assertThat(progress.claimCoffeeForDay(1, FacultyCoffeeOption.CAPPUCCINO_COOKIE_BROWNIE)).isFalse();
        assertThat(progress.getReputation()).isEqualTo(51);
        assertThat(progress.getCoffeeOption()).isEqualTo("CAPPUCCINO");

        assertThat(progress.claimCoffeeForDay(2, FacultyCoffeeOption.CAPPUCCINO_COOKIE)).isTrue();
        assertThat(progress.getReputation()).isEqualTo(53);
        assertThat(progress.getCoffeeClaimedDay()).isEqualTo(2);
        assertThat(progress.getCoffeeOption()).isEqualTo("CAPPUCCINO_COOKIE");
    }
}
