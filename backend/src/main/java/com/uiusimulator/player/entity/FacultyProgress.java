package com.uiusimulator.player.entity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.FetchType;
import jakarta.persistence.Id;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.MapsId;
import jakarta.persistence.OneToOne;
import jakarta.persistence.PostLoad;
import jakarta.persistence.PrePersist;
import jakarta.persistence.PreUpdate;
import jakarta.persistence.Table;
import jakarta.persistence.Transient;
import java.time.Instant;
import java.util.Objects;
import java.util.UUID;
import org.springframework.data.domain.Persistable;

@Entity
@Table(name = "faculty_progress")
public class FacultyProgress implements Persistable<UUID> {

    public static final int DEFAULT_REPUTATION = 50;
    public static final int COMPUTER_REWARD = 5;
    public static final int MATERIAL_REWARD = 5;
    public static final int CLASSROOM_SCAN_REWARD = 10;
    public static final int LECTURE_COMPLETE_REWARD = 5;
    public static final int LECTURE_LEAVE_PENALTY = -10;
    public static final int WRONG_CLASSROOM_PENALTY = -5;
    public static final int QUESTION_PREPARE_REWARD = 3;
    public static final int EXAM_UNPREPARED_PENALTY = -3;

    @Id
    @Column(name = "player_id")
    private UUID playerId;

    @MapsId
    @OneToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "player_id", nullable = false)
    private Player player;

    @Column(nullable = false)
    private int reputation;

    @Column(name = "computer_used", nullable = false)
    private boolean computerUsed;

    @Column(name = "office_entered", nullable = false)
    private boolean officeEntered;

    @Column(name = "classroom_scanned", nullable = false)
    private boolean classroomScanned;

    @Column(name = "lecture_completed", nullable = false)
    private boolean lectureCompleted;

    @Column(name = "lecture_left", nullable = false)
    private boolean lectureLeft;

    @Column(name = "active_course_code", length = 32)
    private String activeCourseCode;

    /** Game day (player_saves.current_day) for which coffee was last successfully claimed. */
    @Column(name = "coffee_claimed_day")
    private Integer coffeeClaimedDay;

    /** Last successful coffee option name; meaningful only when claimed for the compared day. */
    @Column(name = "coffee_option", length = 64)
    private String coffeeOption;

    /** Game day for which exam questions were prepared (day-scoped like coffee_claimed_day). */
    @Column(name = "questions_prepared_day")
    private Integer questionsPreparedDay;

    /** Game day for which the exam unprepared classroom-scan penalty was applied. */
    @Column(name = "exam_unprepared_penalty_day")
    private Integer examUnpreparedPenaltyDay;

    @Column(name = "updated_at", nullable = false)
    private Instant updatedAt;

    @Transient
    private boolean isNew = true;

    protected FacultyProgress() {
    }

    public FacultyProgress(Player player, int reputation) {
        this.player = Objects.requireNonNull(player, "player must not be null");
        this.playerId = player.getId();
        this.reputation = clamp(reputation);
        this.updatedAt = Instant.now();
        this.isNew = true;
    }

    public static FacultyProgress createDefault(Player player) {
        return new FacultyProgress(player, DEFAULT_REPUTATION);
    }

    @Override
    public UUID getId() {
        return playerId;
    }

    @Override
    public boolean isNew() {
        return isNew;
    }

    @PostLoad
    void markNotNew() {
        this.isNew = false;
    }

    @PrePersist
    void onPersist() {
        this.isNew = false;
        this.updatedAt = Instant.now();
    }

    @PreUpdate
    void touchUpdatedAt() {
        this.updatedAt = Instant.now();
    }

    public boolean markComputerUsed() {
        if (computerUsed) {
            return false;
        }
        computerUsed = true;
        officeEntered = true;
        addReputation(COMPUTER_REWARD);
        return true;
    }

    public boolean markClassroomScanned() {
        if (classroomScanned) {
            return false;
        }
        classroomScanned = true;
        addReputation(CLASSROOM_SCAN_REWARD);
        return true;
    }

    public boolean markLectureCompleted() {
        if (lectureCompleted || lectureLeft) {
            return false;
        }
        lectureCompleted = true;
        addReputation(LECTURE_COMPLETE_REWARD);
        return true;
    }

    public boolean markLectureLeft() {
        if (lectureCompleted || lectureLeft) {
            return false;
        }
        lectureLeft = true;
        addReputation(LECTURE_LEAVE_PENALTY);
        return true;
    }

    public boolean beginLectureSession(String courseCode) {
        this.activeCourseCode = Objects.requireNonNull(courseCode, "courseCode must not be null");
        this.lectureLeft = false;
        return markClassroomScanned();
    }

    public void applyWrongClassroomPenalty() {
        addReputation(WRONG_CLASSROOM_PENALTY);
    }

    public boolean leaveActiveLecture() {
        if (activeCourseCode == null || activeCourseCode.isBlank()) {
            return false;
        }
        this.activeCourseCode = null;
        this.lectureLeft = true;
        addReputation(LECTURE_LEAVE_PENALTY);
        return true;
    }

    public void clearActiveLecture() {
        this.activeCourseCode = null;
    }

    public void markAllAssignedLecturesCompleted() {
        this.lectureCompleted = true;
        this.lectureLeft = false;
        this.activeCourseCode = null;
    }

    /**
     * Claims coffee for the given game day and awards the option's exclusive reputation reward.
     * Returns false when coffee was already claimed for that day (idempotent no-op).
     */
    public boolean claimCoffeeForDay(int currentDay, FacultyCoffeeOption option) {
        if (option == null) {
            throw new IllegalArgumentException("Coffee option is required.");
        }
        if (isCoffeeClaimedForDay(currentDay)) {
            return false;
        }
        this.coffeeClaimedDay = currentDay;
        this.coffeeOption = option.name();
        addReputation(option.reputationReward());
        return true;
    }

    public boolean isCoffeeClaimedForDay(int currentDay) {
        return coffeeClaimedDay != null && coffeeClaimedDay == currentDay;
    }

    /**
     * Marks questions prepared for the given game day and awards +3 reputation once.
     * Returns false when already prepared for that day (idempotent no-op).
     */
    public boolean prepareQuestionsForDay(int currentDay) {
        if (isQuestionsPreparedForDay(currentDay)) {
            return false;
        }
        this.questionsPreparedDay = currentDay;
        addReputation(QUESTION_PREPARE_REWARD);
        return true;
    }

    public boolean isQuestionsPreparedForDay(int currentDay) {
        return questionsPreparedDay != null && questionsPreparedDay == currentDay;
    }

    /**
     * Applies the once-per-day −3 unprepared exam-day penalty when questions were not prepared.
     * Returns false when already prepared or already penalized for that day.
     */
    public boolean applyExamUnpreparedPenaltyForDay(int currentDay) {
        if (isQuestionsPreparedForDay(currentDay)) {
            return false;
        }
        if (isExamUnpreparedPenaltyAppliedForDay(currentDay)) {
            return false;
        }
        this.examUnpreparedPenaltyDay = currentDay;
        addReputation(EXAM_UNPREPARED_PENALTY);
        return true;
    }

    public boolean isExamUnpreparedPenaltyAppliedForDay(int currentDay) {
        return examUnpreparedPenaltyDay != null && examUnpreparedPenaltyDay == currentDay;
    }

    /**
     * Clears daily lecture-session flags so both classes can be taught again after day advance.
     * Does not touch reputation, office/computer setup, coffee, or question/penalty day columns.
     */
    public void resetDailyTeachingState() {
        this.classroomScanned = false;
        this.lectureCompleted = false;
        this.lectureLeft = false;
        this.activeCourseCode = null;
    }

    public void addReputation(int delta) {
        this.reputation = clamp(this.reputation + delta);
    }

    public void resetToDefaults() {
        this.reputation = DEFAULT_REPUTATION;
        this.computerUsed = false;
        this.officeEntered = false;
        this.classroomScanned = false;
        this.lectureCompleted = false;
        this.lectureLeft = false;
        this.activeCourseCode = null;
        this.coffeeClaimedDay = null;
        this.coffeeOption = null;
        this.questionsPreparedDay = null;
        this.examUnpreparedPenaltyDay = null;
        this.updatedAt = Instant.now();
    }

    private static int clamp(int value) {
        return Math.max(0, Math.min(100, value));
    }

    public UUID getPlayerId() {
        return playerId;
    }

    public Player getPlayer() {
        return player;
    }

    public int getReputation() {
        return reputation;
    }

    public boolean isComputerUsed() {
        return computerUsed;
    }

    public boolean isOfficeEntered() {
        return officeEntered;
    }

    public boolean isClassroomScanned() {
        return classroomScanned;
    }

    public boolean isLectureCompleted() {
        return lectureCompleted;
    }

    public boolean isLectureLeft() {
        return lectureLeft;
    }

    public String getActiveCourseCode() {
        return activeCourseCode;
    }

    public Integer getCoffeeClaimedDay() {
        return coffeeClaimedDay;
    }

    public String getCoffeeOption() {
        return coffeeOption;
    }

    public Integer getQuestionsPreparedDay() {
        return questionsPreparedDay;
    }

    public Integer getExamUnpreparedPenaltyDay() {
        return examUnpreparedPenaltyDay;
    }

    public Instant getUpdatedAt() {
        return updatedAt;
    }
}
