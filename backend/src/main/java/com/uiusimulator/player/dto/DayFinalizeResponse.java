package com.uiusimulator.player.dto;

import java.util.List;

public record DayFinalizeResponse(
        int semester,
        int day,
        List<DaySummaryActivityResponse> activities,
        int totalAuraDelta,
        int totalAcademicReputationDelta,
        int aura,
        int academicReputation
) {
    public static DayFinalizeResponse of(
            int semester,
            int day,
            List<DaySummaryActivityResponse> activities,
            int aura,
            int academicReputation
    ) {
        int totalAura = activities.stream().mapToInt(DaySummaryActivityResponse::auraDelta).sum();
        int totalRep = activities.stream().mapToInt(DaySummaryActivityResponse::academicReputationDelta).sum();
        return new DayFinalizeResponse(semester, day, activities, totalAura, totalRep, aura, academicReputation);
    }
}
