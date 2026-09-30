package com.uiusimulator.player.entity;

/**
 * Exclusive faculty coffee combinations. Each option awards exactly its total reputation
 * reward — values are not cumulative across options.
 */
public enum FacultyCoffeeOption {
    CAPPUCCINO(1),
    CAPPUCCINO_COOKIE(2),
    CAPPUCCINO_COOKIE_BROWNIE(3);

    private final int reputationReward;

    FacultyCoffeeOption(int reputationReward) {
        this.reputationReward = reputationReward;
    }

    public int reputationReward() {
        return reputationReward;
    }
}
