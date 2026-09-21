package com.uiusimulator.player.exception;

public class PlayerSaveAlreadyExistsException extends RuntimeException {

    public PlayerSaveAlreadyExistsException() {
        super("An active university journey already exists for this player");
    }
}
