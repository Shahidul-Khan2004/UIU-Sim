package com.uiusimulator.player.service;

import com.uiusimulator.player.dto.FacultyCourseProgressItem;
import com.uiusimulator.player.dto.FacultyProgressResponse;
import com.uiusimulator.player.entity.ClassroomCourseDefinition;
import com.uiusimulator.player.entity.FacultyCoffeeOption;
import com.uiusimulator.player.entity.FacultyCourseAssignmentCatalog;
import com.uiusimulator.player.entity.FacultyCourseProgress;
import com.uiusimulator.player.entity.FacultyDaySchedule;
import com.uiusimulator.player.entity.FacultyProgress;
import com.uiusimulator.player.entity.Player;
import com.uiusimulator.player.entity.PlayerRole;
import com.uiusimulator.player.entity.PlayerSave;
import com.uiusimulator.player.exception.PlayerSaveNotFoundException;
import com.uiusimulator.player.repository.CourseMaterialRepository;
import com.uiusimulator.player.repository.FacultyCourseAssignmentRepository;
import com.uiusimulator.player.repository.FacultyCourseProgressRepository;
import com.uiusimulator.player.repository.FacultyProgressRepository;
import com.uiusimulator.player.repository.PlayerSaveRepository;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.stream.Collectors;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.dao.DataIntegrityViolationException;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
public class FacultyProgressService {

    public static final String FACULTY_ONLY_MESSAGE = "Faculty progress is available only to faculty.";
    public static final String COFFEE_CLASSES_REQUIRED_MESSAGE = "Coffee is available after your classes.";
    public static final String PREPARE_QUESTIONS_NOT_EXAM_DAY_MESSAGE =
            "Question preparation is only available on exam days.";

    private static final Logger log = LoggerFactory.getLogger(FacultyProgressService.class);

    private final PlayerService playerService;
    private final PlayerSaveRepository playerSaveRepository;
    private final FacultyProgressRepository facultyProgressRepository;
    private final FacultyCourseProgressRepository facultyCourseProgressRepository;
    private final FacultyCourseAssignmentRepository assignmentRepository;
    private final CourseMaterialRepository materialRepository;

    public FacultyProgressService(
            PlayerService playerService,
            PlayerSaveRepository playerSaveRepository,
            FacultyProgressRepository facultyProgressRepository,
            FacultyCourseProgressRepository facultyCourseProgressRepository,
            FacultyCourseAssignmentRepository assignmentRepository,
            CourseMaterialRepository materialRepository
    ) {
        this.playerService = playerService;
        this.playerSaveRepository = playerSaveRepository;
        this.facultyProgressRepository = facultyProgressRepository;
        this.facultyCourseProgressRepository = facultyCourseProgressRepository;
        this.assignmentRepository = assignmentRepository;
        this.materialRepository = materialRepository;
    }

    @Transactional
    public FacultyProgressResponse getProgress(Jwt jwt) {
        FacultyContext context = requireFaculty(jwt);
        FacultyProgress progress = getOrCreate(context.player());
        return toResponse(progress);
    }

    @Transactional
    public FacultyProgressResponse useComputer(Jwt jwt) {
        FacultyContext context = requireFaculty(jwt);
        FacultyProgress locked = getOrCreateWithLock(context.player());
        boolean rewarded = locked.markComputerUsed();
        FacultyProgress saved = facultyProgressRepository.saveAndFlush(locked);
        if (rewarded) {
            log.info(
                    "Faculty computer reward applied for clerkUserId={}: reputation={}",
                    jwt.getSubject(),
                    saved.getReputation()
            );
        }
        return toResponse(saved);
    }

    /**
     * Optional once-per-day coffee claim. Awards exclusive +1/+2/+3 reputation for the selected
     * combination. Idempotent for the current player day; requires both assigned classes complete.
     */
    @Transactional
    public FacultyProgressResponse claimCoffee(Jwt jwt, FacultyCoffeeOption option) {
        if (option == null) {
            throw new IllegalArgumentException("Coffee option is required.");
        }

        FacultyContext context = requireFaculty(jwt);
        if (!bothAssignedCoursesCompleted(context.player().getId())) {
            throw new IllegalArgumentException(COFFEE_CLASSES_REQUIRED_MESSAGE);
        }

        int currentDay = context.save().getCurrentDay();
        FacultyProgress locked = getOrCreateWithLock(context.player());
        boolean rewarded = locked.claimCoffeeForDay(currentDay, option);
        FacultyProgress saved = facultyProgressRepository.saveAndFlush(locked);
        if (rewarded) {
            log.info(
                    "Faculty coffee reward applied for clerkUserId={} day={} option={} reputation={}",
                    jwt.getSubject(),
                    currentDay,
                    option.name(),
                    saved.getReputation()
            );
        }
        return toResponse(saved);
    }

    /**
     * Exam-day question preparation. Awards +3 reputation once for the current day.
     * Idempotent for the current player day; rejected on non-exam days (1 and 6).
     */
    @Transactional
    public FacultyProgressResponse prepareQuestions(Jwt jwt) {
        FacultyContext context = requireFaculty(jwt);
        int currentDay = context.save().getCurrentDay();
        if (!FacultyDaySchedule.isExamDay(context.save().getSemester(), currentDay)) {
            throw new IllegalArgumentException(PREPARE_QUESTIONS_NOT_EXAM_DAY_MESSAGE);
        }

        FacultyProgress locked = getOrCreateWithLock(context.player());
        boolean rewarded = locked.prepareQuestionsForDay(currentDay);
        FacultyProgress saved = facultyProgressRepository.saveAndFlush(locked);
        if (rewarded) {
            log.info(
                    "Faculty question prepare reward applied for clerkUserId={} day={} reputation={}",
                    jwt.getSubject(),
                    currentDay,
                    saved.getReputation()
            );
        }
        return toResponse(saved);
    }

    /**
     * Resets faculty daily teaching state before day advance: lecture session flags and
     * per-course completion. Preserves reputation, office/computer setup, materials,
     * coffee, and question/penalty day columns.
     */
    @Transactional
    public void resetDailyActivities(Player player) {
        if (player == null) {
            return;
        }
        FacultyProgress locked = getOrCreateWithLock(player);
        locked.resetDailyTeachingState();
        facultyProgressRepository.saveAndFlush(locked);

        List<FacultyCourseProgress> courses = facultyCourseProgressRepository.findByPlayerId(player.getId());
        for (FacultyCourseProgress course : courses) {
            FacultyCourseProgress row = facultyCourseProgressRepository
                    .findByPlayerIdAndCourseCodeWithLock(player.getId(), course.getCourseCode())
                    .orElse(course);
            row.markIncomplete();
            facultyCourseProgressRepository.saveAndFlush(row);
        }
    }

    /**
     * New faculty journeys start at 50 Reputation with no objective rewards applied.
     * Leftover rows from a previous test account / New Game must not keep +15 or mark
     * office / materials complete.
     */
    @Transactional
    public FacultyProgress initializeNewFacultyJourney(Player player) {
        clearFacultyJourney(player.getId());
        return createDefault(player);
    }

    @Transactional
    public void clearFacultyJourney(UUID playerId) {
        if (playerId == null) {
            return;
        }
        materialRepository.deleteByUploadedBy(playerId);
        materialRepository.flush();
        facultyCourseProgressRepository.deleteByPlayerId(playerId);
        facultyCourseProgressRepository.flush();
        assignmentRepository.deleteByPlayerId(playerId);
        assignmentRepository.flush();
        facultyProgressRepository.deleteByPlayerId(playerId);
        facultyProgressRepository.flush();
    }

    /**
     * Awards first-material reputation for ICS or Discrete Mathematics.
     * Caller must invoke this before inserting the new material row.
     */
    @Transactional
    public void rewardFirstCourseMaterialIfNeeded(Player player, String courseId, long existingMaterialCount) {
        if (player == null || courseId == null || existingMaterialCount > 0) {
            return;
        }
        if (!isMaterialRewardCourse(courseId)) {
            return;
        }

        FacultyProgress locked = getOrCreateWithLock(player);
        locked.addReputation(FacultyProgress.MATERIAL_REWARD);
        facultyProgressRepository.saveAndFlush(locked);
        log.info("Faculty first-material reward applied for playerId={} courseId={}", player.getId(), courseId);
    }

    FacultyContext requireFaculty(Jwt jwt) {
        Player player = playerService.getOrProvisionPlayer(jwt);
        PlayerSave save = playerSaveRepository.findByPlayerId(player.getId())
                .orElseThrow(PlayerSaveNotFoundException::new);
        if (save.getRole() != PlayerRole.FACULTY) {
            throw new IllegalArgumentException(FACULTY_ONLY_MESSAGE);
        }
        return new FacultyContext(player, save);
    }

    FacultyProgress getOrCreateWithLock(Player player) {
        return facultyProgressRepository.findByPlayerIdWithLock(player.getId())
                .orElseGet(() -> createDefault(player));
    }

    FacultyProgressResponse toResponse(FacultyProgress progress) {
        return toResponse(progress, null);
    }

    FacultyProgressResponse toResponse(FacultyProgress progress, String teachBlockedReason) {
        UUID playerId = progress.getPlayerId();
        boolean icsPrepared = materialRepository.countByCourseIdAndUploadedBy(
                ClassroomCourseDefinition.ICS.courseId(),
                playerId
        ) > 0;
        boolean dmPrepared = materialRepository.countByCourseIdAndUploadedBy(
                ClassroomCourseDefinition.DISCRETE_MATHEMATICS.courseId(),
                playerId
        ) > 0;

        Map<String, FacultyCourseProgress> byCode = facultyCourseProgressRepository.findByPlayerId(playerId)
                .stream()
                .collect(Collectors.toMap(FacultyCourseProgress::getCourseCode, row -> row, (a, b) -> a));

        List<FacultyCourseProgressItem> courses = new ArrayList<>();
        String nextCourseCode = null;
        boolean icsCompleted = false;
        boolean dmCompleted = false;
        for (ClassroomCourseDefinition course : FacultyCourseAssignmentCatalog.defaultAssignedCourses()) {
            FacultyCourseProgress row = byCode.get(course.courseId());
            boolean completed = row != null && row.isCompleted();
            if (ClassroomCourseDefinition.ICS.courseId().equals(course.courseId())) {
                icsCompleted = completed;
            }
            if (ClassroomCourseDefinition.DISCRETE_MATHEMATICS.courseId().equals(course.courseId())) {
                dmCompleted = completed;
            }
            if (nextCourseCode == null && !completed) {
                nextCourseCode = course.courseId();
            }
            courses.add(new FacultyCourseProgressItem(
                    course.courseId(),
                    completed,
                    row == null ? null : row.getCompletedAt()
            ));
        }

        var save = playerSaveRepository.findByPlayerId(playerId);
        boolean facultyIdIssued = save.map(PlayerSave::isIdCardIssued).orElse(false);
        int currentDay = save.map(PlayerSave::getCurrentDay).orElse(1);

        return FacultyProgressResponse.from(
                progress,
                facultyIdIssued,
                icsPrepared,
                dmPrepared,
                icsCompleted,
                dmCompleted,
                nextCourseCode,
                courses,
                teachBlockedReason,
                currentDay
        );
    }

    private boolean bothAssignedCoursesCompleted(UUID playerId) {
        for (ClassroomCourseDefinition course : FacultyCourseAssignmentCatalog.defaultAssignedCourses()) {
            FacultyCourseProgress row = facultyCourseProgressRepository
                    .findByPlayerIdAndCourseCode(playerId, course.courseId())
                    .orElse(null);
            if (row == null || !row.isCompleted()) {
                return false;
            }
        }
        return true;
    }

    private FacultyProgress getOrCreate(Player player) {
        return facultyProgressRepository.findByPlayerId(player.getId())
                .orElseGet(() -> createDefault(player));
    }

    private FacultyProgress createDefault(Player player) {
        try {
            return facultyProgressRepository.saveAndFlush(FacultyProgress.createDefault(player));
        } catch (DataIntegrityViolationException ex) {
            return facultyProgressRepository.findByPlayerId(player.getId())
                    .or(() -> facultyProgressRepository.findByPlayerIdWithLock(player.getId()))
                    .orElseThrow(() -> ex);
        }
    }

    static boolean isMaterialRewardCourse(String courseId) {
        return ClassroomCourseDefinition.ICS.courseId().equals(courseId)
                || ClassroomCourseDefinition.DISCRETE_MATHEMATICS.courseId().equals(courseId);
    }

    record FacultyContext(Player player, PlayerSave save) {
    }
}
