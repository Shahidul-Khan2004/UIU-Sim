package com.uiusimulator.player.entity;

/**
 * Existing classroom rooms already used by Unity location assets.
 * Do not invent new rooms here; extend only when a classroom asset exists.
 *
 * ICS: Room 427, Floor 4
 * Discrete Mathematics: Room 423, Floor 4
 * English: Room 702, Floor 7
 */
public final class ClassroomLocationCatalog {

    public record Location(String classroomNumber, int floor) {
    }

    public static final Location ICS = new Location("427", 4);
    public static final Location DISCRETE_MATHEMATICS = new Location("423", 4);
    public static final Location ENGLISH = new Location("702", 7);

    private ClassroomLocationCatalog() {
    }

    public static Location require(String courseId) {
        if (courseId == null || courseId.isBlank()) {
            throw new IllegalArgumentException("Unknown classroom course.");
        }
        return switch (courseId.trim()) {
            case "ICS" -> ICS;
            case "DM" -> DISCRETE_MATHEMATICS;
            case "ENGLISH" -> ENGLISH;
            default -> throw new IllegalArgumentException("Unknown classroom course.");
        };
    }
}
