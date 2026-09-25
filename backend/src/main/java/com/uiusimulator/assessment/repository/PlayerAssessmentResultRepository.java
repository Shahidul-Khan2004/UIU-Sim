package com.uiusimulator.assessment.repository;
import com.uiusimulator.assessment.entity.PlayerAssessmentResult;
import org.springframework.data.jpa.repository.JpaRepository;
import java.util.*;
public interface PlayerAssessmentResultRepository extends JpaRepository<PlayerAssessmentResult, UUID> {
    Optional<PlayerAssessmentResult> findByPlayerIdAndSemesterAndCourseIdAndAssessmentType(UUID player, int semester, String course, com.uiusimulator.assessment.entity.AssessmentType type);
    List<PlayerAssessmentResult> findByPlayerIdAndSemester(UUID player, int semester);
    void deleteByPlayerId(UUID player);
}
