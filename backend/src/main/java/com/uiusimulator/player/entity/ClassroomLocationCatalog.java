package com.uiusimulator.player.entity;

/**
 * Existing classroom rooms already used by Unity location assets.
 * Do not invent new rooms here; extend only when a classroom asset exists.
 *
 * ICS: Room 427, Floor 4
 * Discrete Mathematics: Room 423, Floor 4
 * English (CSE): Room 702, Floor 7
 * Introduction to Business: Room 523, Floor 5
 * Principles of Accounting: Room 527, Floor 5
 * English (BBA): Room 701, Floor 7
 */
public final class ClassroomLocationCatalog {

    public record Location(String classroomNumber, int floor) {
    }

    public static final Location ICS = new Location("427", 4);
    public static final Location DISCRETE_MATHEMATICS = new Location("423", 4);
    public static final Location ENGLISH = new Location("702", 7);
    public static final Location IB = new Location("523", 5);
    public static final Location POA = new Location("527", 5);
    public static final Location BBA_ENGLISH = new Location("701", 7);

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
            case "IB" -> IB;
            case "POA" -> POA;
            case "BBA-ENGLISH" -> BBA_ENGLISH;
            default -> throw new IllegalArgumentException("Unknown classroom course.");
        };
    }
}
