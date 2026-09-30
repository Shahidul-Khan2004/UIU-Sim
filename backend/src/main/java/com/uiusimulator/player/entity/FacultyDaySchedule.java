package com.uiusimulator.player.entity;

/**
 * Semester-1 faculty exam-day schedule used by prepare-questions and classroom scan.
 * Days 2 and 5 are Exam, day 3 Midterm, day 6 Final. Days 1 and 4 are not exam days.
 */
public final class FacultyDaySchedule {

    private FacultyDaySchedule() {
    }

    public static boolean isExamDay(int semester, int day) {
        if (semester != 1) {
            return false;
        }
        return day == 2 || day == 3 || day == 5 || day == 6;
    }
}
