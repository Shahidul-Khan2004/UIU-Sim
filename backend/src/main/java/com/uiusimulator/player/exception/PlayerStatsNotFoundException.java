package com.uiusimulator.player.exception;

import java.util.UUID;

public class PlayerStatsNotFoundException extends RuntimeException {

    public PlayerStatsNotFoundException(UUID playerId) {
        super("Player statistics record missing for playerId=" + playerId);
    }

    public PlayerStatsNotFoundException(String message) {
        super(message);
    }
}
