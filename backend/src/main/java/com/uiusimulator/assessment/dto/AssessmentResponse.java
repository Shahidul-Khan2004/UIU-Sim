package com.uiusimulator.assessment.dto;
import java.util.*;
public record AssessmentResponse(UUID attemptId, String courseId, String courseName, String assessmentType,
        String displayName, String state, int questionCount, int questionIndex, int marksObtained, int maxMarks,
        String difficulty, double secondsRemaining, Question question, int auraDelta, int academicReputationDelta,
        int aura, int academicReputation, ReportCardResponse reportCard) {
    public record Option(int index, String text) {}
    public record Question(String id, String text, List<Option> options) {}
}
