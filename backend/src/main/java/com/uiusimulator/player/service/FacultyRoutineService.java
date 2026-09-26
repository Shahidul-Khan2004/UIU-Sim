package com.uiusimulator.player.service;

import com.uiusimulator.player.dto.FacultyRoutineResponse;
import com.uiusimulator.player.dto.FacultyRoutineResponse.FacultyProfile;
import com.uiusimulator.player.dto.FacultyRoutineResponse.FacultyRoutineItem;
import com.uiusimulator.player.entity.ClassroomCourseDefinition;
import com.uiusimulator.player.entity.ClassroomLocationCatalog;
import com.uiusimulator.player.entity.Player;
import com.uiusimulator.player.entity.PlayerRole;
import com.uiusimulator.player.entity.PlayerSave;
import com.uiusimulator.player.exception.PlayerSaveNotFoundException;
import com.uiusimulator.player.repository.PlayerSaveRepository;
import java.util.List;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
public class FacultyRoutineService {

    /**
     * Matches Unity {@code FacultyIdentity} for this milestone. Not persisted.
     */
    static final String ASSIGNED_DESIGNATION = "Lecturer";
    static final String ASSIGNED_OFFICE = "335";

    /**
     * Existing Unity classroom location assets. Do not reverse this mapping.
     * ICS: IcsClassroomLocation.asset — Room 427, Floor 4
     * Discrete Mathematics: DmClassroomLocation.asset — Room 423, Floor 4
     */
    static final String ICS_CLASSROOM_NUMBER = ClassroomLocationCatalog.ICS.classroomNumber();
    static final int ICS_FLOOR = ClassroomLocationCatalog.ICS.floor();
    static final String DM_CLASSROOM_NUMBER = ClassroomLocationCatalog.DISCRETE_MATHEMATICS.classroomNumber();
    static final int DM_FLOOR = ClassroomLocationCatalog.DISCRETE_MATHEMATICS.floor();

    private final PlayerService playerService;
    private final PlayerSaveRepository playerSaveRepository;

    public FacultyRoutineService(
            PlayerService playerService,
            PlayerSaveRepository playerSaveRepository
    ) {
        this.playerService = playerService;
        this.playerSaveRepository = playerSaveRepository;
    }

    @Transactional(readOnly = true)
    public FacultyRoutineResponse getFacultyRoutine(Jwt jwt) {
        Player player = playerService.getOrProvisionPlayer(jwt);
        PlayerSave save = playerSaveRepository.findByPlayerId(player.getId())
                .orElseThrow(PlayerSaveNotFoundException::new);

        if (save.getRole() != PlayerRole.FACULTY) {
            throw new IllegalArgumentException("Faculty routine is available only to faculty.");
        }

        FacultyProfile profile = new FacultyProfile(
                save.getPlayerName(),
                save.getUniversityId(),
                save.getDepartment().getCode(),
                ASSIGNED_DESIGNATION,
                ASSIGNED_OFFICE
        );

        List<FacultyRoutineItem> routine = List.of(
                item(ClassroomCourseDefinition.ICS, ICS_CLASSROOM_NUMBER, ICS_FLOOR),
                item(ClassroomCourseDefinition.DISCRETE_MATHEMATICS, DM_CLASSROOM_NUMBER, DM_FLOOR)
        );

        return new FacultyRoutineResponse(profile, routine);
    }

    private static FacultyRoutineItem item(
            ClassroomCourseDefinition course,
            String classroomNumber,
            int floor
    ) {
        return new FacultyRoutineItem(course.courseId(), course.courseName(), classroomNumber, floor);
    }
}
