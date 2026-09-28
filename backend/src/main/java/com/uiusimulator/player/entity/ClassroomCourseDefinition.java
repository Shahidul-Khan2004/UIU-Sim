package com.uiusimulator.player.entity;

import java.util.List;

/**
 * Server-authoritative classroom courses that share one lecture ruleset.
 * Normal lecture days are centralized here; location assets only describe the rooms.
 * The server does not trust a client-submitted schedule.
 *
 * <p>Semester 1, days 1 and 4 (CSE): Introduction to Computer Science, English, Discrete Mathematics.
 * <p>Semester 1, days 1 and 4 (BBA): Introduction to Business, Principles of Accounting, English.
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
            1,
            "CSE"
    );

    public static final ClassroomCourseDefinition ENGLISH = new ClassroomCourseDefinition(
            "ATTEND_ENGLISH",
            "ENGLISH",
            "English",
            1,
            "CSE"
    );

    public static final ClassroomCourseDefinition DISCRETE_MATHEMATICS = new ClassroomCourseDefinition(
            "ATTEND_DM",
            "DM",
            "Discrete Mathematics",
            1,
            "CSE"
    );

    public static final ClassroomCourseDefinition IB = new ClassroomCourseDefinition(
            "ATTEND_IB",
            "IB",
            "Introduction to Business",
            1,
            "BBA"
    );

    public static final ClassroomCourseDefinition POA = new ClassroomCourseDefinition(
            "ATTEND_POA",
            "POA",
            "Principles of Accounting",
            1,
            "BBA"
    );

    public static final ClassroomCourseDefinition BBA_ENGLISH = new ClassroomCourseDefinition(
            "ATTEND_BBA_ENGLISH",
            "BBA-ENGLISH",
            "English",
            1,
            "BBA"
    );

    private static final List<ClassroomCourseDefinition> ALL = List.of(
            ICS, ENGLISH, DISCRETE_MATHEMATICS, IB, POA, BBA_ENGLISH
    );

    private final String activityId;
    private final String courseId;
    private final String courseName;
    private final int scheduledDay;
    private final String departmentCode;

    private ClassroomCourseDefinition(
            String activityId,
            String courseId,
            String courseName,
            int scheduledDay,
            String departmentCode
    ) {
        this.activityId = activityId;
        this.courseId = courseId;
        this.courseName = courseName;
        this.scheduledDay = scheduledDay;
        this.departmentCode = departmentCode;
    }

    public static List<ClassroomCourseDefinition> all() {
        return ALL;
    }

    public static List<ClassroomCourseDefinition> forDepartment(String code) {
        if (code == null || code.isBlank()) {
            return List.of();
        }
        String normalized = code.trim();
        return ALL.stream()
                .filter(course -> course.departmentCode.equalsIgnoreCase(normalized))
                .toList();
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

    public static ClassroomCourseDefinition byCourseId(String courseId) {
        if (courseId == null || courseId.isBlank()) {
            throw new IllegalArgumentException("Unknown course.");
        }
        String normalized = courseId.trim();
        for (ClassroomCourseDefinition course : ALL) {
            if (course.courseId.equals(normalized)) {
                return course;
            }
        }
        throw new IllegalArgumentException("Unknown course.");
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

    public String departmentCode() {
        return departmentCode;
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
                && departmentCode.equalsIgnoreCase(department.getCode())
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
