package com.uiusimulator.player.dto;

import com.uiusimulator.player.entity.PlayerSave;

public record PlayerSaveResponse(
        String role,
        String department,
        String universityId,
        int semester,
        int currentDay,
        boolean admissionCompleted
) {
    public static PlayerSaveResponse from(PlayerSave save) {
        return new PlayerSaveResponse(
                save.getRole().name(),
                save.getDepartment().getCode(),
                save.getUniversityId(),
                save.getSemester(),
                save.getCurrentDay(),
                save.isAdmissionCompleted()
        );
    }
}
