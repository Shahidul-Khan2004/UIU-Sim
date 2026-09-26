package com.uiusimulator.player.dto;

import java.util.List;

public record FacultyCoursesResponse(
        String facultyId,
        List<FacultyCourseItem> courses
) {
    public record FacultyCourseItem(
            String courseCode,
            String courseName,
            String classroom,
            int floor
    ) {
    }
}
