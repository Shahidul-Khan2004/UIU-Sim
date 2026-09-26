package com.uiusimulator.player.entity;

import java.util.List;

/**
 * Default faculty-to-course assignments for this milestone.
 * Later faculty can receive different courses without changing Unity or rewriting
 * {@link com.uiusimulator.player.entity.FacultyCourseAssignment} persistence.
 */
public final class FacultyCourseAssignmentCatalog {

    private FacultyCourseAssignmentCatalog() {
    }

    /**
     * Default assigned courses for a faculty player who has no rows yet.
     * Expand or replace this list per faculty/department later.
     */
    public static List<ClassroomCourseDefinition> defaultAssignedCourses() {
        return List.of(
                ClassroomCourseDefinition.ICS,
                ClassroomCourseDefinition.DISCRETE_MATHEMATICS
        );
    }
}
