package com.uiusimulator.player.service;

import com.uiusimulator.player.dto.FacultyCourseProgressItem;
import com.uiusimulator.player.dto.FacultyProgressResponse;
import com.uiusimulator.player.entity.ClassroomCourseDefinition;
import com.uiusimulator.player.entity.FacultyCourseAssignmentCatalog;
import com.uiusimulator.player.entity.FacultyCourseProgress;
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

        boolean facultyIdIssued = playerSaveRepository.findByPlayerId(playerId)
                .map(PlayerSave::isIdCardIssued)
                .orElse(false);

        return FacultyProgressResponse.from(
                progress,
                facultyIdIssued,
                icsPrepared,
                dmPrepared,
                icsCompleted,
                dmCompleted,
                nextCourseCode,
                courses,
                teachBlockedReason
        );
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
