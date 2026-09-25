package com.uiusimulator.assessment.repository;
import com.uiusimulator.assessment.entity.PlayerCourseEnrollment;
import org.springframework.data.jpa.repository.JpaRepository;
import java.util.*;
public interface PlayerCourseEnrollmentRepository extends JpaRepository<PlayerCourseEnrollment, UUID> {
    Optional<PlayerCourseEnrollment> findByPlayerIdAndSemesterAndCourseId(UUID player, int semester, String course);
    List<PlayerCourseEnrollment> findByPlayerIdAndSemester(UUID player, int semester);
    void deleteByPlayerId(UUID player);
}
