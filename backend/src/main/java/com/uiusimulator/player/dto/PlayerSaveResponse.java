package com.uiusimulator.player.dto;

import com.uiusimulator.player.entity.PlayerSave;

public record PlayerSaveResponse(
        String role,
        String playerName,
        String department,
        String universityId,
        int semester,
        int currentDay,
        boolean admissionCompleted,
        boolean idCardIssued
) {
    public static PlayerSaveResponse from(PlayerSave save) {
        return new PlayerSaveResponse(
                save.getRole().name(),
                save.getPlayerName(),
                save.getDepartment().getCode(),
                save.getUniversityId(),
                save.getSemester(),
                save.getCurrentDay(),
                save.isAdmissionCompleted(),
                save.isIdCardIssued()
        );
    }
}
