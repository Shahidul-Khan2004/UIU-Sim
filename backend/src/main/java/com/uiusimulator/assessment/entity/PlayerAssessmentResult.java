package com.uiusimulator.assessment.entity;

import jakarta.persistence.*;
import java.time.Instant;
import java.util.UUID;

/** Attempt snapshot contains server-only answer keys; never serialize this entity to clients. */
@Entity
@Table(name = "player_assessment_results", uniqueConstraints = @UniqueConstraint(columnNames = {"player_id", "semester", "course_id", "assessment_type"}))
public class PlayerAssessmentResult {
    @Id private UUID id;
    @Column(name = "player_id", nullable = false) private UUID playerId;
    @Column(nullable = false) private int semester;
    @Column(name = "day_number", nullable = false) private int dayNumber;
    @Column(name = "course_id", nullable = false, length = 32) private String courseId;
    @Enumerated(EnumType.STRING) @Column(name = "assessment_type", nullable = false, length = 32) private AssessmentType assessmentType;
    @Enumerated(EnumType.STRING) @Column(nullable = false, length = 32) private AssessmentState state;
    @Column(name = "marks_obtained", nullable = false) private int marksObtained;
    @Column(name = "max_marks", nullable = false) private int maxMarks;
    @Column(name = "aura_delta", nullable = false) private int auraDelta;
    @Column(name = "academic_reputation_delta", nullable = false) private int academicReputationDelta;
    @Column(name = "question_index", nullable = false) private int questionIndex;
    @Column(name = "attempt_snapshot", columnDefinition = "text") private String attemptSnapshot;
    @Column(name = "question_deadline") private Instant questionDeadline;
    @Column(name = "started_at") private Instant startedAt;
    @Column(name = "completed_at") private Instant completedAt;
    @Column(name = "created_at", nullable = false) private Instant createdAt;
    @Column(name = "updated_at", nullable = false) private Instant updatedAt;
    protected PlayerAssessmentResult() {}
    public static PlayerAssessmentResult start(UUID player, int semester, int day, String course,
            AssessmentType type, String snapshot, int seconds, Instant now) {
        var a = new PlayerAssessmentResult();
        a.id = UUID.randomUUID(); a.playerId = player; a.semester = semester; a.dayNumber = day;
        a.courseId = course; a.assessmentType = type; a.maxMarks = type.maxMarks();
        a.state = AssessmentState.STARTED; a.attemptSnapshot = snapshot;
        a.startedAt = now; a.createdAt = now; a.updatedAt = now;
        a.questionDeadline = now.plusSeconds(seconds); return a;
    }
    public void answer(boolean correct, int seconds, Instant now) {
        if (isTerminal()) throw new IllegalArgumentException("Assessment has ended.");
        if (correct) marksObtained += AssessmentType.MARKS_PER_QUESTION;
        questionIndex++; updatedAt = now;
        if (questionIndex == assessmentType.questionCount()) finish(AssessmentState.COMPLETED, marksObtained, 0, 0, now);
        else questionDeadline = now.plusSeconds(seconds);
    }
    public void finish(AssessmentState outcome, int marks, int aura, int academic, Instant now) {
        if (isTerminal()) return;
        if (outcome == AssessmentState.STARTED || marks < 0 || marks > maxMarks || marks % 5 != 0)
            throw new IllegalArgumentException("Invalid assessment result.");
        state = outcome; marksObtained = marks; auraDelta = aura; academicReputationDelta = academic;
        completedAt = now; updatedAt = now; questionDeadline = null;
        if (outcome == AssessmentState.MISSED && attemptSnapshot == null) startedAt = null;
    }
    public boolean isTerminal() { return state != AssessmentState.STARTED; }
    public UUID getId() { return id; }
    public String getCourseId() { return courseId; }
    public AssessmentType getAssessmentType() { return assessmentType; }
    public AssessmentState getState() { return state; }
    public int getMarksObtained() { return marksObtained; }
    public int getMaxMarks() { return maxMarks; }
    public int getAuraDelta() { return auraDelta; }
    public int getAcademicReputationDelta() { return academicReputationDelta; }
    public int getQuestionIndex() { return questionIndex; }
    public String getAttemptSnapshot() { return attemptSnapshot; }
    public Instant getQuestionDeadline() { return questionDeadline; }
    public int getDayNumber() { return dayNumber; }
    public Instant getStartedAt() { return startedAt; }
    public Instant getCompletedAt() { return completedAt; }
}
