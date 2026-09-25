package com.uiusimulator.player.entity;

import java.util.List;

/**
 * Server-authoritative CSE classroom courses that share one lecture ruleset.
 * Normal lecture days are centralized here; location assets only describe the rooms.
 * The server does not trust a client-submitted schedule.
 *
 * <p>Semester 1, days 1 and 4: Introduction to Computer Science, English, Discrete Mathematics.
 */
public final class ClassroomCourseDefinition {

    public static final int LECTURE_DURATION_SECONDS = 90;
    public static final int MILESTONE_30_SECONDS = 30;
    public static final int MILESTONE_60_SECONDS = 60;
    public static final int MILESTONE_90_SECONDS = 90;
    public static final int MILESTONE_REPUTATION_REWARD = 2;
    public static final int EARLY_LEAVE_REPUTATION_PENALTY = -4;
    public static final int SKIPPED_REPUTATION_PENALTY = -4;
    public static final int PROXY_AURA_REWARD = 3;

    public static final ClassroomCourseDefinition ICS = new ClassroomCourseDefinition(
            "ATTEND_ICS",
            "ICS",
            "Introduction to Computer Science",
            1
    );

    public static final ClassroomCourseDefinition ENGLISH = new ClassroomCourseDefinition(
            "ATTEND_ENGLISH",
            "ENGLISH",
            "English",
            1
    );

    public static final ClassroomCourseDefinition DISCRETE_MATHEMATICS = new ClassroomCourseDefinition(
            "ATTEND_DM",
            "DM",
            "Discrete Mathematics",
            1
    );

    private static final List<ClassroomCourseDefinition> ALL = List.of(ICS, ENGLISH, DISCRETE_MATHEMATICS);

    private final String activityId;
    private final String courseId;
    private final String courseName;
    private final int scheduledDay;

    private ClassroomCourseDefinition(String activityId, String courseId, String courseName, int scheduledDay) {
        this.activityId = activityId;
        this.courseId = courseId;
        this.courseName = courseName;
        this.scheduledDay = scheduledDay;
    }

    public static List<ClassroomCourseDefinition> all() {
        return ALL;
    }

    public static ClassroomCourseDefinition require(String activityId) {
        if (activityId == null) {
            throw new IllegalArgumentException("Unknown classroom activity.");
        }
        for (ClassroomCourseDefinition course : ALL) {
            if (course.activityId.equals(activityId)) {
                return course;
            }
        }
        throw new IllegalArgumentException("Unknown classroom activity.");
    }

    public String activityId() {
        return activityId;
    }

    public String courseId() {
        return courseId;
    }

    public String courseName() {
        return courseName;
    }

    public int scheduledDay() {
        return scheduledDay;
    }

    public static boolean isNormalClassDay(int semester, int day) {
        return semester == 1 && (day == 1 || day == 4);
    }

    public boolean isEligible(PlayerSave save) {
        if (save == null || save.getRole() != PlayerRole.STUDENT) {
            return false;
        }
        Department department = save.getDepartment();
        return department != null
                && "CSE".equalsIgnoreCase(department.getCode())
                && isNormalClassDay(save.getSemester(), save.getCurrentDay());
    }

    public static int reputationRewardForMilestone(int milestoneSeconds) {
        if (milestoneSeconds == MILESTONE_30_SECONDS
                || milestoneSeconds == MILESTONE_60_SECONDS
                || milestoneSeconds == MILESTONE_90_SECONDS) {
            return MILESTONE_REPUTATION_REWARD;
        }
        throw new IllegalArgumentException("Unsupported classroom milestone: " + milestoneSeconds);
    }

    public static int requiredPreviousMilestone(int milestoneSeconds) {
        return switch (milestoneSeconds) {
            case MILESTONE_30_SECONDS -> 0;
            case MILESTONE_60_SECONDS -> MILESTONE_30_SECONDS;
            case MILESTONE_90_SECONDS -> MILESTONE_60_SECONDS;
            default -> throw new IllegalArgumentException("Unsupported classroom milestone: " + milestoneSeconds);
        };
    }
}
