package com.uiusimulator.assessment.dto;
import java.util.List;
public record ReportCardResponse(int semester, int day, String scheduledAssessment, List<Course> courses, Double cgpa) {
    // JsonUtility cannot distinguish nullable numbers; expose their status explicitly.
    @com.fasterxml.jackson.annotation.JsonProperty("cgpaStatus")
    public String cgpaStatus() { return cgpa == null ? "PENDING" : "FINAL"; }
    public record Component(String assessmentType, String displayName, String state, Integer marksObtained, int maxMarks, int questionCount) {}
    public record Course(String courseId, String courseName, String status, int total, String grade,
            Double gradePoint, String gradeDescription, List<Component> components) {}
}
