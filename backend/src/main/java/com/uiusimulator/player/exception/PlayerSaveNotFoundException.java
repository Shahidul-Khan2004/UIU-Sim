package com.uiusimulator.player.exception;

public class PlayerSaveNotFoundException extends RuntimeException {

    public PlayerSaveNotFoundException() {
        super("No active university journey found. Complete admission first.");
    }
}
