package com.uiusimulator.player.entity;

/**
 * Server-owned Library Study reward table. Unity submits a normalized score only.
 * Academic Reputation never comes from a client delta.
 */
public final class LibraryStudyDefinition {

    public static final String ACTIVITY_ID = "LIBRARY_STUDY";
    public static final int MIN_SCORE = 0;
    public static final int MAX_SCORE = 100;
    public static final int MAX_REPUTATION_REWARD = 5;

    private LibraryStudyDefinition() {
    }

    /**
     * Score 0–19 → 0, 20–39 → 1, 40–59 → 2, 60–79 → 3, 80–99 → 4, 100 → 5.
     */
    public static int reputationForScore(int score) {
        if (score < MIN_SCORE || score > MAX_SCORE) {
            throw new IllegalArgumentException("Library study score must be between 0 and 100.");
        }
        if (score >= 100) {
            return 5;
        }
        if (score >= 80) {
            return 4;
        }
        if (score >= 60) {
            return 3;
        }
        if (score >= 40) {
            return 2;
        }
        if (score >= 20) {
            return 1;
        }
        return 0;
    }
}
