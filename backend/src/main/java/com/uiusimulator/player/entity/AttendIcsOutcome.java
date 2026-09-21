package com.uiusimulator.player.entity;

/**
 * ICS attendance outcomes persisted on {@code player_day_activities.outcome}.
 * Terminal outcomes: COMPLETED, LEFT_EARLY, PROXY, SKIPPED.
 * In-session outcome: ATTENDING.
 */
public enum AttendIcsOutcome {
    ATTENDING(ActivityStatus.IN_PROGRESS),
    COMPLETED(ActivityStatus.COMPLETED),
    LEFT_EARLY(ActivityStatus.MISSED),
    PROXY(ActivityStatus.COMPLETED),
    SKIPPED(ActivityStatus.MISSED);

    private final ActivityStatus status;

    AttendIcsOutcome(ActivityStatus status) {
        this.status = status;
    }

    public ActivityStatus status() {
        return status;
    }

    public boolean isTerminal() {
        return this != ATTENDING;
    }
}
