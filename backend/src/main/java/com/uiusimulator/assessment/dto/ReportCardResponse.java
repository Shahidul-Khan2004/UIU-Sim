package com.uiusimulator.assessment.dto;
import java.util.List;
public record ReportCardResponse(int semester, int day, String scheduledAssessment, List<Course> courses) {
    public record Component(String assessmentType, String displayName, String state, Integer marksObtained, int maxMarks, int questionCount) {}
    public record Course(String courseId, String courseName, String status, int total, String grade,
            Double gradePoint, String gradeDescription, List<Component> components) {}
}
