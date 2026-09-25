package com.uiusimulator.player.entity;

/**
 * ICS classroom constants. Shared lecture rules and the day-1 CSE schedule live on
 * {@link ClassroomCourseDefinition}; this type remains the ICS entry point.
 */
public final class AttendIcsDefinition {

    public static final String ACTIVITY_ID = ClassroomCourseDefinition.ICS.activityId();
    public static final String COURSE_ID = ClassroomCourseDefinition.ICS.courseId();
    public static final String COURSE_NAME = ClassroomCourseDefinition.ICS.courseName();
    public static final String REQUIRED_DEPARTMENT_CODE = "CSE";
    public static final PlayerRole REQUIRED_ROLE = PlayerRole.STUDENT;

    /** Gameplay day on which ICS is scheduled. Unity Inspector must match this. */
    public static final int SCHEDULED_DAY = ClassroomCourseDefinition.ICS.scheduledDay();

    public static final int LECTURE_DURATION_SECONDS = ClassroomCourseDefinition.LECTURE_DURATION_SECONDS;
    public static final int MILESTONE_30_SECONDS = ClassroomCourseDefinition.MILESTONE_30_SECONDS;
    public static final int MILESTONE_60_SECONDS = ClassroomCourseDefinition.MILESTONE_60_SECONDS;
    public static final int MILESTONE_90_SECONDS = ClassroomCourseDefinition.MILESTONE_90_SECONDS;
    public static final int MILESTONE_REPUTATION_REWARD = ClassroomCourseDefinition.MILESTONE_REPUTATION_REWARD;
    public static final int FULL_ATTENDANCE_REPUTATION = 6;
    public static final int EARLY_LEAVE_REPUTATION_PENALTY = ClassroomCourseDefinition.EARLY_LEAVE_REPUTATION_PENALTY;
    public static final int SKIPPED_REPUTATION_PENALTY = ClassroomCourseDefinition.SKIPPED_REPUTATION_PENALTY;
    public static final int PROXY_AURA_REWARD = ClassroomCourseDefinition.PROXY_AURA_REWARD;

    private AttendIcsDefinition() {
    }

    public static boolean isEligible(PlayerSave save) {
        return ClassroomCourseDefinition.ICS.isEligible(save);
    }

    public static int reputationRewardForMilestone(int milestoneSeconds) {
        return ClassroomCourseDefinition.reputationRewardForMilestone(milestoneSeconds);
    }

    public static int requiredPreviousMilestone(int milestoneSeconds) {
        return ClassroomCourseDefinition.requiredPreviousMilestone(milestoneSeconds);
    }
}
