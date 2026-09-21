package com.uiusimulator.player.entity;

/**
 * Server-authoritative GET_ID_CARD outcomes and their Aura/Reputation deltas.
 * One completion only; client must not invent reward amounts.
 */
public enum GetIdCardOutcome {
    COMPLETED(ActivityStatus.COMPLETED, 0, 0);

    public static final String ACTIVITY_ID = "GET_ID_CARD";

    private final ActivityStatus status;
    private final int auraDelta;
    private final int reputationDelta;

    GetIdCardOutcome(ActivityStatus status, int auraDelta, int reputationDelta) {
        this.status = status;
        this.auraDelta = auraDelta;
        this.reputationDelta = reputationDelta;
    }

    public ActivityStatus status() {
        return status;
    }

    public int auraDelta() {
        return auraDelta;
    }

    public int reputationDelta() {
        return reputationDelta;
    }
}
