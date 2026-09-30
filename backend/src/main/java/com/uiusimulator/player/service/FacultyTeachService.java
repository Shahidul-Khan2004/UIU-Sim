package com.uiusimulator.player.service;

import com.uiusimulator.player.dto.FacultyProgressResponse;
import com.uiusimulator.player.entity.ClassroomCourseDefinition;
import com.uiusimulator.player.entity.ClassroomLocationCatalog;
import com.uiusimulator.player.entity.FacultyCourseAssignment;
import com.uiusimulator.player.entity.FacultyCourseAssignmentCatalog;
import com.uiusimulator.player.entity.FacultyCourseProgress;
import com.uiusimulator.player.entity.FacultyDaySchedule;
import com.uiusimulator.player.entity.FacultyProgress;
import com.uiusimulator.player.repository.FacultyCourseAssignmentRepository;
import com.uiusimulator.player.repository.FacultyCourseProgressRepository;
import com.uiusimulator.player.repository.FacultyProgressRepository;
import com.uiusimulator.player.service.FacultyProgressService.FacultyContext;
import java.util.List;
import java.util.UUID;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.dao.DataIntegrityViolationException;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
public class FacultyTeachService {

    public static final String NOT_ASSIGNED_MESSAGE = "You are not assigned to this course.";
    public static final String SCAN_REQUIRED_MESSAGE = "Scan your faculty ID before teaching.";
    public static final String SESSION_RESOLVED_MESSAGE = "This teaching session is already resolved.";
    public static final String ALREADY_TAKEN_MESSAGE = "You have already taken this class.";

    private static final String DISCRETE_MATHEMATICS_ALIAS = "DISCRETE_MATHEMATICS";

    private static final Logger log = LoggerFactory.getLogger(FacultyTeachService.class);

    private final FacultyProgressService facultyProgressService;
    private final FacultyProgressRepository facultyProgressRepository;
    private final FacultyCourseAssignmentRepository assignmentRepository;
    private final FacultyCourseProgressRepository courseProgressRepository;

    public FacultyTeachService(
            FacultyProgressService facultyProgressService,
            FacultyProgressRepository facultyProgressRepository,
            FacultyCourseAssignmentRepository assignmentRepository,
            FacultyCourseProgressRepository courseProgressRepository
    ) {
        this.facultyProgressService = facultyProgressService;
        this.facultyProgressRepository = facultyProgressRepository;
        this.assignmentRepository = assignmentRepository;
        this.courseProgressRepository = courseProgressRepository;
    }

    @Transactional
    public FacultyProgressResponse scanClassroom(Jwt jwt, String courseId) {
        FacultyContext context = facultyProgressService.requireFaculty(jwt);
        UUID playerId = context.player().getId();
        String requestedCourse = requireAssignedCourse(playerId, courseId);

        FacultyProgress locked = facultyProgressService.getOrCreateWithLock(context.player());
        FacultyCourseProgress requestedProgress = getOrCreateCourseProgress(playerId, requestedCourse);
        if (requestedProgress.isCompleted()) {
            FacultyProgress saved = facultyProgressRepository.saveAndFlush(locked);
            return facultyProgressService.toResponse(saved, ALREADY_TAKEN_MESSAGE);
        }

        ClassroomCourseDefinition nextRequired = nextIncompleteCourse(playerId);
        if (nextRequired == null) {
            FacultyProgress saved = facultyProgressRepository.saveAndFlush(locked);
            return facultyProgressService.toResponse(saved, ALREADY_TAKEN_MESSAGE);
        }

        if (!nextRequired.courseId().equals(requestedCourse)) {
            locked.applyWrongClassroomPenalty();
            FacultyProgress saved = facultyProgressRepository.saveAndFlush(locked);
            log.info(
                    "Faculty wrong-classroom penalty applied for clerkUserId={} requested={} next={} reputation={}",
                    jwt.getSubject(),
                    requestedCourse,
                    nextRequired.courseId(),
                    saved.getReputation()
            );
            return facultyProgressService.toResponse(saved, wrongClassroomMessage(nextRequired));
        }

        boolean firstScanReward = locked.beginLectureSession(requestedCourse);
        int currentDay = context.save().getCurrentDay();
        boolean examPenaltyApplied = false;
        if (FacultyDaySchedule.isExamDay(context.save().getSemester(), currentDay)) {
            examPenaltyApplied = locked.applyExamUnpreparedPenaltyForDay(currentDay);
        }
        FacultyProgress saved = facultyProgressRepository.saveAndFlush(locked);
        if (firstScanReward) {
            log.info(
                    "Faculty classroom scan reward applied for clerkUserId={} courseId={} reputation={}",
                    jwt.getSubject(),
                    requestedCourse,
                    saved.getReputation()
            );
        }
        if (examPenaltyApplied) {
            log.info(
                    "Faculty exam unprepared penalty applied for clerkUserId={} day={} reputation={}",
                    jwt.getSubject(),
                    currentDay,
                    saved.getReputation()
            );
        }
        return facultyProgressService.toResponse(saved);
    }

    @Transactional
    public FacultyProgressResponse completeLecture(Jwt jwt) {
        FacultyContext context = facultyProgressService.requireFaculty(jwt);
        FacultyProgress locked = facultyProgressService.getOrCreateWithLock(context.player());
        String activeCourse = locked.getActiveCourseCode();
        if (activeCourse == null || activeCourse.isBlank()) {
            throw new IllegalArgumentException(SCAN_REQUIRED_MESSAGE);
        }

        FacultyCourseProgress courseProgress = getOrCreateCourseProgress(context.player().getId(), activeCourse);
        boolean rewarded = courseProgress.markCompleted();
        courseProgressRepository.saveAndFlush(courseProgress);
        if (rewarded) {
            locked.addReputation(FacultyProgress.LECTURE_COMPLETE_REWARD);
            log.info(
                    "Faculty lecture complete reward applied for clerkUserId={} courseId={} reputation={}",
                    jwt.getSubject(),
                    activeCourse,
                    locked.getReputation()
            );
        }

        locked.clearActiveLecture();
        if (allAssignedCoursesCompleted(context.player().getId())) {
            locked.markAllAssignedLecturesCompleted();
        }
        FacultyProgress saved = facultyProgressRepository.saveAndFlush(locked);
        return facultyProgressService.toResponse(saved);
    }

    @Transactional
    public FacultyProgressResponse leaveLecture(Jwt jwt) {
        FacultyContext context = facultyProgressService.requireFaculty(jwt);
        FacultyProgress locked = facultyProgressService.getOrCreateWithLock(context.player());
        if (!locked.leaveActiveLecture()) {
            throw new IllegalArgumentException(SCAN_REQUIRED_MESSAGE);
        }
        FacultyProgress saved = facultyProgressRepository.saveAndFlush(locked);
        log.info(
                "Faculty lecture leave penalty applied for clerkUserId={} reputation={}",
                jwt.getSubject(),
                saved.getReputation()
        );
        return facultyProgressService.toResponse(saved);
    }

    private ClassroomCourseDefinition nextIncompleteCourse(UUID playerId) {
        for (ClassroomCourseDefinition course : FacultyCourseAssignmentCatalog.defaultAssignedCourses()) {
            FacultyCourseProgress progress = courseProgressRepository
                    .findByPlayerIdAndCourseCode(playerId, course.courseId())
                    .orElse(null);
            if (progress == null || !progress.isCompleted()) {
                return course;
            }
        }
        return null;
    }

    private boolean allAssignedCoursesCompleted(UUID playerId) {
        return nextIncompleteCourse(playerId) == null;
    }

    private FacultyCourseProgress getOrCreateCourseProgress(UUID playerId, String courseCode) {
        return courseProgressRepository.findByPlayerIdAndCourseCodeWithLock(playerId, courseCode)
                .orElseGet(() -> createCourseProgress(playerId, courseCode));
    }

    private FacultyCourseProgress createCourseProgress(UUID playerId, String courseCode) {
        try {
            return courseProgressRepository.saveAndFlush(FacultyCourseProgress.start(playerId, courseCode));
        } catch (DataIntegrityViolationException ex) {
            return courseProgressRepository.findByPlayerIdAndCourseCodeWithLock(playerId, courseCode)
                    .or(() -> courseProgressRepository.findByPlayerIdAndCourseCode(playerId, courseCode))
                    .orElseThrow(() -> ex);
        }
    }

    private String requireAssignedCourse(UUID playerId, String courseId) {
        String normalized = normalizeCourseCode(courseId);
        ClassroomCourseDefinition.byCourseId(normalized);
        ensureDefaultAssignments(playerId);
        if (!assignmentRepository.existsByPlayerIdAndCourseId(playerId, normalized)) {
            throw new IllegalArgumentException(NOT_ASSIGNED_MESSAGE);
        }
        return normalized;
    }

    static String normalizeCourseCode(String courseId) {
        if (courseId == null || courseId.isBlank()) {
            throw new IllegalArgumentException("Unknown course.");
        }
        String normalized = courseId.trim();
        if (DISCRETE_MATHEMATICS_ALIAS.equalsIgnoreCase(normalized)) {
            return ClassroomCourseDefinition.DISCRETE_MATHEMATICS.courseId();
        }
        return ClassroomCourseDefinition.byCourseId(normalized).courseId();
    }

    static String wrongClassroomMessage(ClassroomCourseDefinition nextRequired) {
        ClassroomLocationCatalog.Location location = ClassroomLocationCatalog.require(nextRequired.courseId());
        return "You are in the wrong classroom.\n\nYour next class is "
                + nextRequired.courseName()
                + " in Room "
                + location.classroomNumber()
                + ".";
    }

    private void ensureDefaultAssignments(UUID playerId) {
        List<FacultyCourseAssignment> existing = assignmentRepository.findByPlayerIdOrderByCreatedAtAsc(playerId);
        if (!existing.isEmpty()) {
            return;
        }
        for (ClassroomCourseDefinition course : FacultyCourseAssignmentCatalog.defaultAssignedCourses()) {
            try {
                assignmentRepository.saveAndFlush(FacultyCourseAssignment.assign(playerId, course.courseId()));
            } catch (DataIntegrityViolationException ignored) {
                // Concurrent first access already inserted this assignment.
            }
        }
    }
}
