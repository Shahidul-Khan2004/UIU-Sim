package com.uiusimulator.assessment.service;
import com.uiusimulator.assessment.entity.*;
import com.uiusimulator.assessment.repository.PlayerCourseEnrollmentRepository;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import java.util.UUID;
@Service
public class CourseEnrollmentService {
    private final PlayerCourseEnrollmentRepository repository;
    public CourseEnrollmentService(PlayerCourseEnrollmentRepository repository) { this.repository = repository; }
    @Transactional(readOnly = true)
    public boolean isDropped(UUID player, int semester, String course) {
        return repository.findByPlayerIdAndSemesterAndCourseId(player, semester, course)
                .map(e -> e.getStatus() == CourseStatus.DROPPED_CHEATING).orElse(false);
    }
    @Transactional(readOnly = true)
    public void requireActive(UUID player, int semester, String course) {
        if (isDropped(player, semester, course))
            throw new IllegalArgumentException("You were removed from this course after being caught cheating.");
    }
}
