package com.uiusimulator.assessment.service;

import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.uiusimulator.assessment.config.*;
import com.uiusimulator.assessment.dto.*;
import com.uiusimulator.assessment.entity.*;
import com.uiusimulator.assessment.repository.*;
import com.uiusimulator.player.dto.DaySummaryActivityResponse;
import com.uiusimulator.player.entity.*;
import com.uiusimulator.player.exception.*;
import com.uiusimulator.player.repository.*;
import com.uiusimulator.player.service.PlayerService;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import java.time.*;
import java.util.*;

/** All mutations take stats -> save locks, matching End Day and New Game. The stats row
 * serializes even first-attempt inserts and different-course cheating; unique keys are a backstop. */
@Service
public class AssessmentService {
    private final PlayerService players;
    private final PlayerStatsRepository stats;
    private final PlayerSaveRepository saves;
    private final PlayerAssessmentResultRepository results;
    private final PlayerCourseEnrollmentRepository enrollments;
    private final CourseEnrollmentService courses;
    private final AssessmentCatalog catalog;
    private final CheatPolicy cheat;
    private final AssessmentRandom random;
    private final Clock clock;
    private final ObjectMapper mapper = new ObjectMapper();
    public AssessmentService(PlayerService players, PlayerStatsRepository stats, PlayerSaveRepository saves,
            PlayerAssessmentResultRepository results, PlayerCourseEnrollmentRepository enrollments,
            CourseEnrollmentService courses, AssessmentCatalog catalog, CheatPolicy cheat, AssessmentRandom random, Clock clock) {
        this.players = players; this.stats = stats; this.saves = saves; this.results = results;
        this.enrollments = enrollments; this.courses = courses; this.catalog = catalog;
        this.cheat = cheat; this.random = random; this.clock = clock;
    }
    private record Context(Player player, PlayerStats stats, PlayerSave save) {}
    private Context lock(Jwt jwt) {
        var player = players.getOrProvisionPlayer(jwt);
        var lockedStats = stats.findByPlayerIdWithLock(player.getId()).orElseThrow(() -> new PlayerStatsNotFoundException(player.getId()));
        var save = saves.findByPlayerIdWithLock(player.getId()).orElseThrow(PlayerSaveNotFoundException::new);
        if (!AssessmentCatalog.eligible(save)) throw new IllegalArgumentException("Assessments are available only to CSE students.");
        return new Context(player, lockedStats, save);
    }
    private void requireSchedule(Context ctx, String course, AssessmentType type) {
        AssessmentCatalog.course(course);
        if (catalog.scheduled(ctx.save()) != type) throw new IllegalArgumentException("This assessment is not scheduled today.");
    }
    @Transactional
    public AssessmentResponse start(Jwt jwt, String course, AssessmentType type) {
        var ctx = lock(jwt);
        requireSchedule(ctx, course, type);
        var existing = results.findByPlayerIdAndSemesterAndCourseIdAndAssessmentType(ctx.player().getId(), ctx.save().getSemester(), course, type);
        // Canonical terminal recovery takes precedence over the drop it may itself have caused.
        if (existing.isPresent()) {
            var a = existing.get();
            if (!a.isTerminal()) { courses.requireActive(ctx.player().getId(), ctx.save().getSemester(), course); expire(a); }
            return response(ctx, a);
        }
        courses.requireActive(ctx.player().getId(), ctx.save().getSemester(), course);
        var snapshot = catalog.select(course, type, ctx.stats().getAcademicReputation(), random);
        var now = clock.instant();
        var a = PlayerAssessmentResult.start(ctx.player().getId(), ctx.save().getSemester(), ctx.save().getCurrentDay(),
                course, type, encode(snapshot), snapshot.seconds(), now);
        enrollments.findByPlayerIdAndSemesterAndCourseId(ctx.player().getId(), ctx.save().getSemester(), course)
                .orElseGet(() -> enrollments.save(PlayerCourseEnrollment.active(ctx.player().getId(), ctx.save().getSemester(), course, now)));
        results.saveAndFlush(a);
        return response(ctx, a);
    }
    private PlayerAssessmentResult owned(Context ctx, UUID id) {
        // Scoped query prevents other players' attempts, old semester attempts, or stale New Game requests.
        return results.findByPlayerIdAndSemester(ctx.player().getId(), ctx.save().getSemester()).stream()
                .filter(a -> a.getId().equals(id)).findFirst().orElseThrow(() -> new IllegalArgumentException("Unknown assessment attempt."));
    }
    private void requireActiveAttempt(Context ctx, PlayerAssessmentResult a) {
        requireSchedule(ctx, a.getCourseId(), a.getAssessmentType());
        courses.requireActive(ctx.player().getId(), ctx.save().getSemester(), a.getCourseId());
    }
    @Transactional
    public AssessmentResponse answer(Jwt jwt, UUID id, AssessmentAnswerRequest request) {
        var ctx = lock(jwt); var a = owned(ctx, id);
        if (a.isTerminal()) return response(ctx, a);
        requireActiveAttempt(ctx, a);
        if (request.questionIndex() == null || request.answerIndex() == null || request.answerIndex() < -1 || request.answerIndex() > 3)
            throw new IllegalArgumentException("Invalid answer.");
        if (request.questionIndex() < a.getQuestionIndex()) return response(ctx, a); // response-loss retry
        if (request.questionIndex() != a.getQuestionIndex()) throw new IllegalArgumentException("Unexpected question index.");
        var snapshot = snapshot(a);
        var q = snapshot.questions().get(a.getQuestionIndex());
        if (request.answerIndex() >= 0 && !q.visibleOptions().contains(request.answerIndex()))
            throw new IllegalArgumentException("Answer is not visible.");
        boolean onTime = clock.instant().isBefore(a.getQuestionDeadline());
        a.answer(onTime && request.answerIndex() == q.correctAnswer(), snapshot.seconds(), clock.instant());
        results.saveAndFlush(a);
        return response(ctx, a);
    }
    @Transactional
    public AssessmentResponse resolveCheat(Jwt jwt, UUID id) {
        var ctx = lock(jwt); var a = owned(ctx, id);
        if (a.isTerminal()) return response(ctx, a);
        requireActiveAttempt(ctx, a);
        expire(a);
        if (a.isTerminal()) return response(ctx, a);
        boolean caught = random.nextDouble() < cheat.caughtProbability();
        int auraBefore = ctx.stats().getAura(), repBefore = ctx.stats().getAcademicReputation();
        ctx.stats().modifyStats(caught ? cheat.caughtAura() : cheat.successAura(), caught ? cheat.caughtAcademic() : 0);
        var now = clock.instant();
        a.finish(caught ? AssessmentState.CHEAT_CAUGHT : AssessmentState.CHEAT_SUCCESS, caught ? 0 : a.getMaxMarks(),
                ctx.stats().getAura() - auraBefore, ctx.stats().getAcademicReputation() - repBefore, now);
        if (caught) {
            var enrollment = enrollments.findByPlayerIdAndSemesterAndCourseId(ctx.player().getId(), ctx.save().getSemester(), a.getCourseId())
                    .orElseGet(() -> PlayerCourseEnrollment.active(ctx.player().getId(), ctx.save().getSemester(), a.getCourseId(), now));
            enrollment.drop(now); enrollments.save(enrollment);
        }
        stats.saveAndFlush(ctx.stats()); results.saveAndFlush(a);
        return response(ctx, a);
    }
    private void expire(PlayerAssessmentResult a) {
        if (!a.isTerminal() && !clock.instant().isBefore(a.getQuestionDeadline())) {
            a.answer(false, snapshot(a).seconds(), clock.instant()); results.saveAndFlush(a);
        }
    }
    @Transactional(readOnly = true)
    public ReportCardResponse reportCard(Jwt jwt) {
        var player = players.getOrProvisionPlayer(jwt);
        var save = saves.findByPlayerId(player.getId()).orElseThrow(PlayerSaveNotFoundException::new);
        return report(player.getId(), save);
    }
    private ReportCardResponse report(UUID player, PlayerSave save) {
        if (!AssessmentCatalog.eligible(save)) return new ReportCardResponse(save.getSemester(), save.getCurrentDay(), null, List.of());
        var rows = results.findByPlayerIdAndSemester(player, save.getSemester());
        var dropped = enrollments.findByPlayerIdAndSemester(player, save.getSemester()).stream()
                .filter(e -> e.getStatus() == CourseStatus.DROPPED_CHEATING).map(PlayerCourseEnrollment::getCourseId).toList();
        var reportCourses = new ArrayList<ReportCardResponse.Course>();
        for (var course : ClassroomCourseDefinition.all()) {
            boolean isDropped = dropped.contains(course.courseId());
            int total = 0; boolean complete = true;
            var components = new ArrayList<ReportCardResponse.Component>();
            for (var type : AssessmentType.values()) {
                var a = rows.stream().filter(r -> r.getCourseId().equals(course.courseId()) && r.getAssessmentType() == type).findFirst().orElse(null);
                boolean terminal = a != null && a.isTerminal();
                if (!terminal) complete = false;
                if (terminal) total += a.getMarksObtained();
                components.add(new ReportCardResponse.Component(type.name(), type.displayName(), terminal ? a.getState().name() : isDropped ? "DROPPED" : a != null ? "STARTED" : "PENDING",
                        terminal ? a.getMarksObtained() : null, type.maxMarks(), type.questionCount()));
            }
            if (isDropped) total = 0; // authoritative course-level override of all historical marks
            var grade = isDropped || complete ? GradeCalculator.calculate(total) : null;
            reportCourses.add(new ReportCardResponse.Course(course.courseId(), course.courseName(), isDropped ? "DROPPED_CHEATING" : "ACTIVE",
                    total, grade == null ? "Pending" : grade.letter(), grade == null ? null : grade.gradePoint(),
                    grade == null ? "Pending" : grade.description(), components));
        }
        var scheduled = catalog.scheduled(save);
        return new ReportCardResponse(save.getSemester(), save.getCurrentDay(), scheduled == null ? null : scheduled.name(), reportCourses);
    }
    /** Caller (End Day) already owns the same stats/save locks. Joins that transaction. */
    @Transactional
    public void finalizeDay(Player player, PlayerSave save) {
        var type = catalog.scheduled(save);
        if (type == null) return;
        for (var course : ClassroomCourseDefinition.all()) {
            if (courses.isDropped(player.getId(), save.getSemester(), course.courseId())) continue;
            var a = results.findByPlayerIdAndSemesterAndCourseIdAndAssessmentType(player.getId(), save.getSemester(), course.courseId(), type)
                    .orElseGet(() -> PlayerAssessmentResult.start(player.getId(), save.getSemester(), save.getCurrentDay(), course.courseId(), type, null, 1, clock.instant()));
            if (!a.isTerminal()) {
                a.finish(AssessmentState.MISSED, 0, 0, 0, clock.instant()); results.saveAndFlush(a);
            }
        }
    }
    @Transactional(readOnly = true)
    public List<DaySummaryActivityResponse> summary(Player player, PlayerSave save) {
        return results.findByPlayerIdAndSemester(player.getId(), save.getSemester()).stream()
                .filter(a -> a.getDayNumber() == save.getCurrentDay() && a.isTerminal())
                .sorted(Comparator.comparing(PlayerAssessmentResult::getCourseId))
                .map(a -> new DaySummaryActivityResponse("ASSESSMENT_" + a.getCourseId(),
                        a.getState() == AssessmentState.MISSED || a.getState() == AssessmentState.CHEAT_CAUGHT ? "MISSED" : "COMPLETED",
                        a.getState().name(), a.getAuraDelta(), a.getAcademicReputationDelta(),
                        a.getMarksObtained(), a.getMaxMarks(), a.getAssessmentType().displayName(), a.getState() == AssessmentState.CHEAT_CAUGHT)).toList();
    }
    private AssessmentResponse response(Context ctx, PlayerAssessmentResult a) {
        AssessmentResponse.Question question = null;
        String difficulty = ""; double remaining = 0;
        if (!a.isTerminal()) {
            var snapshot = snapshot(a); difficulty = snapshot.difficulty();
            var q = snapshot.questions().get(a.getQuestionIndex());
            question = new AssessmentResponse.Question(q.id(), q.text(), q.visibleOptions().stream()
                    .map(i -> new AssessmentResponse.Option(i, q.answers().get(i))).toList());
            remaining = Math.max(0, Duration.between(clock.instant(), a.getQuestionDeadline()).toMillis() / 1000.0);
        }
        return new AssessmentResponse(a.getId(), a.getCourseId(), AssessmentCatalog.course(a.getCourseId()).courseName(), a.getAssessmentType().name(),
                a.getAssessmentType().displayName(), a.getState().name(), a.getAssessmentType().questionCount(), a.getQuestionIndex(),
                a.getMarksObtained(), a.getMaxMarks(), difficulty, remaining, question, a.getAuraDelta(), a.getAcademicReputationDelta(),
                ctx.stats().getAura(), ctx.stats().getAcademicReputation(), report(ctx.player().getId(), ctx.save()));
    }
    private String encode(AssessmentCatalog.Snapshot snapshot) {
        try { return mapper.writeValueAsString(snapshot); } catch (JsonProcessingException e) { throw new IllegalStateException("Invalid assessment snapshot", e); }
    }
    private AssessmentCatalog.Snapshot snapshot(PlayerAssessmentResult a) {
        try { return mapper.readValue(a.getAttemptSnapshot(), AssessmentCatalog.Snapshot.class); }
        catch (JsonProcessingException e) { throw new IllegalStateException("Invalid assessment snapshot", e); }
    }
}
