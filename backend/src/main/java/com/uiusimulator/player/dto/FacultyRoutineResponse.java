package com.uiusimulator.player.dto;

import java.util.List;

public record FacultyRoutineResponse(
        FacultyProfile profile,
        List<FacultyRoutineItem> routine
) {
    public record FacultyProfile(
            String name,
            String facultyId,
            String department,
            String designation,
            String office
    ) {
    }

    public record FacultyRoutineItem(
            String courseId,
            String courseName,
            String classroomNumber,
            int floor
    ) {
    }
}
