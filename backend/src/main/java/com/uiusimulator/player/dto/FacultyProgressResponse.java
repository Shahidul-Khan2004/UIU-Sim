package com.uiusimulator.player.dto;

import com.uiusimulator.player.entity.FacultyProgress;
import java.util.List;

public record FacultyProgressResponse(
        int reputation,
        boolean facultyIdIssued,
        boolean computerUsed,
        boolean officeEntered,
        boolean classroomScanned,
        boolean lectureCompleted,
        boolean lectureLeft,
        boolean icsMaterialPrepared,
        boolean dmMaterialPrepared,
        boolean icsCompleted,
        boolean dmCompleted,
        boolean officeSetup,
        boolean materialsPrepared,
        String nextCourseCode,
        String activeCourseCode,
        String teachBlockedReason,
        List<FacultyCourseProgressItem> courses,
        boolean coffeeClaimedForCurrentDay,
        String coffeeOption,
        boolean questionsPreparedForCurrentDay
) {
    public static FacultyProgressResponse from(
            FacultyProgress progress,
            boolean facultyIdIssued,
            boolean icsMaterialPrepared,
            boolean dmMaterialPrepared,
            boolean icsCompleted,
            boolean dmCompleted,
            String nextCourseCode,
            List<FacultyCourseProgressItem> courses,
            String teachBlockedReason,
            int currentDay
    ) {
        boolean allAssignedComplete = icsCompleted && dmCompleted;
        boolean officeSetup = progress.isComputerUsed() || progress.isOfficeEntered();
        boolean coffeeClaimedForCurrentDay = progress.isCoffeeClaimedForDay(currentDay);
        return new FacultyProgressResponse(
                progress.getReputation(),
                facultyIdIssued,
                progress.isComputerUsed(),
                progress.isOfficeEntered(),
                progress.isClassroomScanned(),
                allAssignedComplete || progress.isLectureCompleted(),
                progress.isLectureLeft(),
                icsMaterialPrepared,
                dmMaterialPrepared,
                icsCompleted,
                dmCompleted,
                officeSetup,
                icsMaterialPrepared && dmMaterialPrepared,
                nextCourseCode,
                progress.getActiveCourseCode(),
                teachBlockedReason,
                courses == null ? List.of() : List.copyOf(courses),
                coffeeClaimedForCurrentDay,
                coffeeClaimedForCurrentDay ? progress.getCoffeeOption() : null,
                progress.isQuestionsPreparedForDay(currentDay)
        );
    }

    public static FacultyProgressResponse stub(
            int reputation,
            boolean computerUsed,
            boolean officeEntered,
            boolean classroomScanned,
            boolean lectureCompleted,
            boolean lectureLeft,
            boolean icsMaterialPrepared,
            boolean dmMaterialPrepared
    ) {
        return new FacultyProgressResponse(
                reputation,
                false,
                computerUsed,
                officeEntered,
                classroomScanned,
                lectureCompleted,
                lectureLeft,
                icsMaterialPrepared,
                dmMaterialPrepared,
                false,
                false,
                computerUsed || officeEntered,
                icsMaterialPrepared && dmMaterialPrepared,
                lectureCompleted ? null : "ICS",
                null,
                null,
                List.of(),
                false,
                null,
                false
        );
    }
}
