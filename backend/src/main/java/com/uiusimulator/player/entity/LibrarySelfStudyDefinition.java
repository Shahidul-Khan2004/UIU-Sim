package com.uiusimulator.player.entity;

/**
 * Optional once-per-day Library Self Study (table / desk reading).
 * Academic Reputation is server-owned. Unity must not submit a reputation delta.
 * Independent from {@link LibraryStudyDefinition} (computer / Rocket Study).
 */
public final class LibrarySelfStudyDefinition {

    public static final String ACTIVITY_ID = "LIBRARY_SELF_STUDY";
    public static final int DURATION_SECONDS = 90;
    public static final int MILESTONE_30_SECONDS = 30;
    public static final int MILESTONE_60_SECONDS = 60;
    public static final int MILESTONE_90_SECONDS = 90;
    public static final int MILESTONE_REPUTATION_REWARD = 1;
    public static final int TOTAL_REPUTATION_REWARD = 3;
    public static final String OUTCOME_STARTED = "STARTED";
    public static final String OUTCOME_COMPLETED = "COMPLETED";
    public static final String OUTCOME_ABANDONED = "ABANDONED";

    private LibrarySelfStudyDefinition() {
    }

    public static int reputationRewardForMilestone(int milestoneSeconds) {
        if (milestoneSeconds == MILESTONE_30_SECONDS
                || milestoneSeconds == MILESTONE_60_SECONDS
                || milestoneSeconds == MILESTONE_90_SECONDS) {
            return MILESTONE_REPUTATION_REWARD;
        }
        throw new IllegalArgumentException("Unsupported library self-study milestone: " + milestoneSeconds);
    }

    public static int requiredPreviousMilestone(int milestoneSeconds) {
        return switch (milestoneSeconds) {
            case MILESTONE_30_SECONDS -> 0;
            case MILESTONE_60_SECONDS -> MILESTONE_30_SECONDS;
            case MILESTONE_90_SECONDS -> MILESTONE_60_SECONDS;
            default -> throw new IllegalArgumentException(
                    "Unsupported library self-study milestone: " + milestoneSeconds);
        };
    }
}
