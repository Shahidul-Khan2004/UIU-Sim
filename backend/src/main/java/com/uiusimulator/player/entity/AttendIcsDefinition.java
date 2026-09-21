package com.uiusimulator.player.entity;

/**
 * Server-authoritative ICS classroom activity definition and reward table.
 * Client must not invent Aura / Academic Reputation amounts for these outcomes.
 */
public final class AttendIcsDefinition {

    public static final String ACTIVITY_ID = "ATTEND_ICS";
    public static final String COURSE_ID = "ICS";
    public static final String COURSE_NAME = "Introduction to Computer Science";
    public static final String REQUIRED_DEPARTMENT_CODE = "CSE";
    public static final PlayerRole REQUIRED_ROLE = PlayerRole.STUDENT;

    /** Gameplay day on which ICS is scheduled. Unity Inspector must match this. */
    public static final int SCHEDULED_DAY = 1;

    /** Compressed lecture length in real seconds (also displayed as minutes). */
    public static final int LECTURE_DURATION_SECONDS = 90;

    public static final int MILESTONE_30_SECONDS = 30;
    public static final int MILESTONE_60_SECONDS = 60;
    public static final int MILESTONE_90_SECONDS = 90;

    public static final int MILESTONE_REPUTATION_REWARD = 4;
    public static final int FULL_ATTENDANCE_REPUTATION = 12;
    public static final int EARLY_LEAVE_REPUTATION_PENALTY = -5;
    public static final int SKIPPED_REPUTATION_PENALTY = -5;
    public static final int PROXY_AURA_REWARD = 5;

    private AttendIcsDefinition() {
    }

    public static boolean isEligible(PlayerSave save) {
        if (save == null || save.getRole() != REQUIRED_ROLE) {
            return false;
        }
        Department department = save.getDepartment();
        return department != null
                && REQUIRED_DEPARTMENT_CODE.equalsIgnoreCase(department.getCode())
                && save.getCurrentDay() == SCHEDULED_DAY;
    }

    public static int reputationRewardForMilestone(int milestoneSeconds) {
        if (milestoneSeconds == MILESTONE_30_SECONDS
                || milestoneSeconds == MILESTONE_60_SECONDS
                || milestoneSeconds == MILESTONE_90_SECONDS) {
            return MILESTONE_REPUTATION_REWARD;
        }
        throw new IllegalArgumentException("Unsupported ICS milestone: " + milestoneSeconds);
    }

    public static int requiredPreviousMilestone(int milestoneSeconds) {
        return switch (milestoneSeconds) {
            case MILESTONE_30_SECONDS -> 0;
            case MILESTONE_60_SECONDS -> MILESTONE_30_SECONDS;
            case MILESTONE_90_SECONDS -> MILESTONE_60_SECONDS;
            default -> throw new IllegalArgumentException("Unsupported ICS milestone: " + milestoneSeconds);
        };
    }
}
