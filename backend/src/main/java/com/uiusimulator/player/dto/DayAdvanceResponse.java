package com.uiusimulator.player.dto;

import com.uiusimulator.player.entity.PlayerSave;
import com.uiusimulator.player.entity.PlayerStats;

public record DayAdvanceResponse(
        int semester,
        int currentDay,
        boolean alreadyAdvanced,
        boolean idCardIssued,
        int aura,
        int academicReputation
) {
    public static DayAdvanceResponse of(PlayerSave save, PlayerStats stats, boolean alreadyAdvanced) {
        return new DayAdvanceResponse(
                save.getSemester(),
                save.getCurrentDay(),
                alreadyAdvanced,
                save.isIdCardIssued(),
                stats.getAura(),
                stats.getAcademicReputation()
        );
    }
}
