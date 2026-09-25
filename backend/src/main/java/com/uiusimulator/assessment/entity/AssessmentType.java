package com.uiusimulator.assessment.entity;

public enum AssessmentType {
    QUIZ_1("Quiz 1", 3), MIDTERM("Midterm", 6), QUIZ_2("Quiz 2", 3), FINAL("Final Exam", 8);
    public static final int MARKS_PER_QUESTION = 5;
    private final String displayName;
    private final int questionCount;
    AssessmentType(String name, int count) { displayName = name; questionCount = count; }
    public String displayName() { return displayName; }
    public int questionCount() { return questionCount; }
    public int maxMarks() { return questionCount * MARKS_PER_QUESTION; }
}
