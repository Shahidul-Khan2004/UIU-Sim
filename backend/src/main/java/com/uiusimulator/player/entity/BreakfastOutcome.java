package com.uiusimulator.player.entity;

/**
 * Server-authoritative breakfast outcomes and their Aura deltas.
 * Client must not invent Aura amounts for these outcomes.
 */
public enum BreakfastOutcome {
    RICE(ActivityStatus.COMPLETED, 5, 0),
    POROTTA_WAIT(ActivityStatus.COMPLETED, 0, 0),
    SKIP_LINE(ActivityStatus.COMPLETED, -10, 0),
    SKIP_BREAKFAST(ActivityStatus.MISSED, -5, 0);

    public static final String ACTIVITY_ID = "BREAKFAST";

    private final ActivityStatus status;
    private final int auraDelta;
    private final int reputationDelta;

    BreakfastOutcome(ActivityStatus status, int auraDelta, int reputationDelta) {
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
