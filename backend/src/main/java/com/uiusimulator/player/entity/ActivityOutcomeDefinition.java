package com.uiusimulator.player.entity;

/**
 * Normalized outcome payload used by {@link com.uiusimulator.player.service.PlayerActivityService}
 * so multiple activity types share one resolve path.
 */
public record ActivityOutcomeDefinition(
        ActivityStatus status,
        String outcomeName,
        int auraDelta,
        int reputationDelta
) {
}
