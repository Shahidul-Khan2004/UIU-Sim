package com.uiusimulator.assessment;

import static org.assertj.core.api.Assertions.*;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.uiusimulator.assessment.config.*;
import com.uiusimulator.assessment.dto.*;
import com.uiusimulator.assessment.entity.*;
import com.uiusimulator.assessment.repository.*;
import com.uiusimulator.assessment.service.*;
import com.uiusimulator.player.dto.*;
import com.uiusimulator.player.entity.*;
import com.uiusimulator.player.repository.*;
import com.uiusimulator.player.service.*;
import java.time.*;
import java.util.*;
import org.junit.jupiter.api.*;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.*;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.orm.jpa.DataJpaTest;
import org.springframework.boot.test.context.TestConfiguration;
import org.springframework.context.annotation.*;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.test.context.ActiveProfiles;
import org.springframework.test.util.ReflectionTestUtils;

@DataJpaTest(showSql = false)
@ActiveProfiles("test")
@Import({PlayerService.class, PlayerSaveService.class, PlayerActivityService.class, PlayerDayService.class,
    AttendIcsService.class, AssessmentService.class, CourseEnrollmentService.class, CheatPolicy.class,
    AssessmentServiceTest.Config.class})
class AssessmentServiceTest {
    @Autowired AssessmentService service;
    @Autowired PlayerService players;
    @Autowired PlayerSaveService saves;
    @Autowired PlayerSaveRepository saveRepository;
    @Autowired PlayerStatsRepository statsRepository;
    @Autowired PlayerAssessmentResultRepository results;
    @Autowired PlayerCourseEnrollmentRepository enrollments;
    @Autowired DepartmentRepository departments;
    @Autowired PlayerDayService days;
    @Autowired AttendIcsService attendance;
    @Autowired PlayerActivityService activities;
    @Autowired TestRandom random;
    @Autowired TestClock clock;
    @Autowired TestCatalog catalog;
    Jwt jwt;
    Player player;
    Department cse;
    @BeforeEach void setup() {
        random.value = .99; random.calls = 0; catalog.override = null;
        clock.now = Instant.parse("2026-01-01T00:00:00Z");
        cse = departments.saveAndFlush(Department.createNew("CSE", "Computer Science"));
        jwt = jwt("assessment_" + UUID.randomUUID());
        player = players.getOrProvisionPlayer(jwt);
        saves.createSave(jwt, new PlayerSaveCreateRequest(PlayerRole.STUDENT, "Test Student", cse.getId(), "011-test"));
        saveRepository.findByPlayerId(player.getId()).orElseThrow().advanceToNextDay();
    }
    @ParameterizedTest @ValueSource(ints={0,1,2,3})
    void normalScores_areServerGraded_andNeverChangeStats(int correct) throws Exception {
        var a = service.start(jwt,"ICS",AssessmentType.QUIZ_1);
        var original = a;
        for (int i=0;i<3;i++) a = service.answer(jwt,a.attemptId(),new AssessmentAnswerRequest(i,i<correct?key(a):wrongKey(a)));
        assertThat(a.state()).isEqualTo("COMPLETED");
        assertThat(a.marksObtained()).isEqualTo(correct*5);
        assertThat(a.maxMarks()).isEqualTo(15);
        assertThat(a.aura()).isEqualTo(50); assertThat(a.academicReputation()).isEqualTo(50);
        assertThat(a.auraDelta()).isZero(); assertThat(a.academicReputationDelta()).isZero();
        assertThat(service.answer(jwt,a.attemptId(),new AssessmentAnswerRequest(2,0)).marksObtained()).isEqualTo(correct*5);
        assertThat(service.resolveCheat(jwt,a.attemptId()).state()).isEqualTo("COMPLETED");
        assertThat(service.start(jwt,"ICS",AssessmentType.QUIZ_1).attemptId()).isEqualTo(original.attemptId());
        assertThat(results.findByPlayerIdAndSemester(player.getId(),1)).hasSize(1);
    }
    @Test void everyAssessmentUsesSameNormalEngineAndFinalGradeDoesNotChangeStats() throws Exception {
        for (var type : AssessmentType.values()) {
            catalog.override = type;
            var a = service.start(jwt, "ICS", type);
            for (int i = 0; i < type.questionCount(); i++)
                a = service.answer(jwt, a.attemptId(), new AssessmentAnswerRequest(i, key(a)));
            assertThat(a.marksObtained()).isEqualTo(type.maxMarks());
            assertThat(a.questionCount()).isEqualTo(type.questionCount());
            assertThat(a.aura()).isEqualTo(50);
            assertThat(a.academicReputation()).isEqualTo(50);
        }
        var course = service.reportCard(jwt).courses().get(0);
        assertThat(course.total()).isEqualTo(100);
        assertThat(course.grade()).isEqualTo("A");
        assertThat(course.gradePoint()).isEqualTo(4.0);
    }
    @Test void missingSaveIsRejectedAndIncompleteGradeRemainsPending() {
        assertThatThrownBy(() -> service.start(jwt("no-save"), "ICS", AssessmentType.QUIZ_1))
                .isInstanceOf(com.uiusimulator.player.exception.PlayerSaveNotFoundException.class);
        var a = service.start(jwt, "ICS", AssessmentType.QUIZ_1);
        service.resolveCheat(jwt, a.attemptId());
        var course = service.reportCard(jwt).courses().get(0);
        assertThat(course.total()).isEqualTo(15);
        assertThat(course.grade()).isEqualTo("Pending");
        assertThat(course.gradePoint()).isNull();
        assertThat(course.components().subList(1, 4)).allMatch(c -> c.marksObtained() == null && c.state().equals("PENDING"));
    }
    @Test void serverDoesNotExposeAnswerKeyOrFutureQuestions() throws Exception {
        var a = service.start(jwt,"ICS",AssessmentType.QUIZ_1);
        String json = new ObjectMapper().writeValueAsString(a);
        assertThat(json).doesNotContain("correctAnswer", "attemptSnapshot", "visibleOptions");
        assertThat(a.question().options()).hasSize(4);
    }
    @Test void resume_preservesSelectionDifficultyMarksAndDeadline() throws Exception {
        var first=service.start(jwt,"ICS",AssessmentType.QUIZ_1);
        var snapshot=results.findById(first.attemptId()).orElseThrow().getAttemptSnapshot();
        stats().modifyStats(0,50);
        clock.now=clock.now.plusSeconds(5);
        var resumed=service.start(jwt,"ICS",AssessmentType.QUIZ_1);
        assertThat(resumed.attemptId()).isEqualTo(first.attemptId());
        assertThat(resumed.question()).isEqualTo(first.question());
        assertThat(resumed.difficulty()).isEqualTo("NORMAL");
        assertThat(resumed.secondsRemaining()).isEqualTo(first.secondsRemaining()-5);
        assertThat(results.findById(first.attemptId()).orElseThrow().getAttemptSnapshot()).isEqualTo(snapshot);
        var answered=service.answer(jwt,first.attemptId(),new AssessmentAnswerRequest(0,key(first)));
        var retry=service.answer(jwt,first.attemptId(),new AssessmentAnswerRequest(0,key(first)));
        assertThat(retry.questionIndex()).isEqualTo(1); assertThat(retry.marksObtained()).isEqualTo(5);
        assertThat(retry.question()).isEqualTo(answered.question());
    }
    @Test void expiredAnswersAndUnansweredQuestionsScoreZero() throws Exception {
        var a=service.start(jwt,"ICS",AssessmentType.QUIZ_1);
        clock.now=clock.now.plusSeconds(60);
        a=service.answer(jwt,a.attemptId(),new AssessmentAnswerRequest(0,key(a)));
        assertThat(a.marksObtained()).isZero();
        a=service.answer(jwt,a.attemptId(),new AssessmentAnswerRequest(1,-1));
        clock.now=clock.now.plusSeconds(60);
        a=service.start(jwt,"ICS",AssessmentType.QUIZ_1);
        assertThat(a.state()).isEqualTo("COMPLETED"); assertThat(a.marksObtained()).isZero();
        assertThat(service.resolveCheat(jwt,a.attemptId()).state()).isEqualTo("COMPLETED");
    }
    @ParameterizedTest @EnumSource(AssessmentType.class)
    void cheatSuccessUsesDefinitionFullMarksAndOneReward(AssessmentType type) {
        catalog.override=type; // test-only schedule: no future days added in production
        var a=service.start(jwt,"ICS",type);
        random.calls=0;
        var success=service.resolveCheat(jwt,a.attemptId());
        assertThat(success.marksObtained()).isEqualTo(type.maxMarks());
        assertThat(success.aura()).isEqualTo(60); assertThat(success.academicReputation()).isEqualTo(50);
        assertThat(success.reportCard().courses().get(0).status()).isEqualTo("ACTIVE");
        assertThat(service.resolveCheat(jwt,a.attemptId()).aura()).isEqualTo(60);
        assertThat(random.calls).isEqualTo(1);
    }
    @Test void successfulCheatLeavesLaterAssessmentAvailable() {
        var a=service.start(jwt,"ICS",AssessmentType.QUIZ_1); service.resolveCheat(jwt,a.attemptId());
        catalog.override=AssessmentType.MIDTERM;
        assertThat(service.start(jwt,"ICS",AssessmentType.MIDTERM).state()).isEqualTo("STARTED");
    }
    @Test void cheatSuccessClampPersistsActualDeltaInSummary() {
        stats().modifyStats(48,0);
        var a=service.start(jwt,"ICS",AssessmentType.QUIZ_1);
        var done=service.resolveCheat(jwt,a.attemptId());
        assertThat(done.auraDelta()).isEqualTo(2); assertThat(done.aura()).isEqualTo(100);
        var summary=days.finalizeCurrentDay(jwt);
        assertThat(summary.activities()).filteredOn(x->x.activityId().equals("ASSESSMENT_ICS"))
            .singleElement().satisfies(x-> {assertThat(x.auraDelta()).isEqualTo(2);assertThat(x.marksObtained()).isEqualTo(15);});
    }
    @Test void caughtClampsDropsAndImmediatelyForcesFailWithNoRepeatedPenalty() {
        stats().modifyStats(-48,-46);
        var a=service.start(jwt,"ICS",AssessmentType.QUIZ_1); random.value=0; random.calls=0;
        var caught=service.resolveCheat(jwt,a.attemptId());
        assertThat(caught.marksObtained()).isZero();assertThat(caught.state()).isEqualTo("CHEAT_CAUGHT");
        assertThat(caught.auraDelta()).isEqualTo(-2); assertThat(caught.academicReputationDelta()).isEqualTo(-4);
        var course=caught.reportCard().courses().get(0);
        assertThat(course.status()).isEqualTo("DROPPED_CHEATING"); assertThat(course.total()).isZero();
        assertThat(course.grade()).isEqualTo("F"); assertThat(course.gradePoint()).isEqualTo(0.0);
        assertThat(course.components().subList(1,4)).allMatch(c->c.state().equals("DROPPED") && c.marksObtained()==null);
        assertThat(service.resolveCheat(jwt,a.attemptId()).aura()).isZero();
        assertThat(random.calls).isEqualTo(1);
        assertThat(service.start(jwt,"ICS",AssessmentType.QUIZ_1).state()).isEqualTo("CHEAT_CAUGHT");
        var summary=days.finalizeCurrentDay(jwt);
        assertThat(summary.activities()).filteredOn(x->x.activityId().equals("ASSESSMENT_ICS")).singleElement()
            .satisfies(x->{assertThat(x.auraDelta()).isEqualTo(-2);assertThat(x.academicReputationDelta()).isEqualTo(-4);assertThat(x.courseDropped()).isTrue();});
        days.advanceDay(jwt,new DayAdvanceRequest(1,2));
        assertThat(service.reportCard(jwt).courses().get(0).status()).isEqualTo("DROPPED_CHEATING");
        assertThatThrownBy(()->attendance.startLecture(jwt)).hasMessageContaining("removed");
        assertThatThrownBy(()->attendance.punchProxy(jwt)).hasMessageContaining("removed");
        // Configure future assessment in test only; the course remains blocked.
        ReflectionTestUtils.setField(saveRepository.findByPlayerId(player.getId()).orElseThrow(),"currentDay",2);
        catalog.override=AssessmentType.MIDTERM;
        assertThatThrownBy(()->service.start(jwt,"ICS",AssessmentType.MIDTERM)).hasMessageContaining("removed");
    }
    @Test void dropOverridesHistoricalMarks_withoutFabricatingFutureRows() throws Exception {
        var a=service.start(jwt,"ICS",AssessmentType.QUIZ_1);
        for(int i=0;i<3;i++) a=service.answer(jwt,a.attemptId(),new AssessmentAnswerRequest(i,key(a)));
        catalog.override=AssessmentType.MIDTERM;
        a=service.start(jwt,"ICS",AssessmentType.MIDTERM); random.value=0;
        var caught=service.resolveCheat(jwt,a.attemptId());
        assertThat(caught.auraDelta()).isEqualTo(-5); assertThat(caught.academicReputationDelta()).isEqualTo(-10);
        var course=caught.reportCard().courses().get(0);
        assertThat(course.total()).isZero(); assertThat(course.grade()).isEqualTo("F");
        assertThat(course.components().get(0).marksObtained()).isEqualTo(15);
        assertThat(results.findByPlayerIdAndSemester(player.getId(),1)).hasSize(2);
    }
    @ParameterizedTest @EnumSource(AssessmentType.class)
    void missedIsZeroNoStatsAndFinalizationIsIdempotent(AssessmentType type) {
        catalog.override=type;
        activities.resolveActivity(jwt,new ActivityResolveRequest("BREAKFAST","POROTTA_WAIT"));
        var first=days.finalizeCurrentDay(jwt); var second=days.finalizeCurrentDay(jwt);
        assertThat(first.academicReputation()).isEqualTo(50);assertThat(first.aura()).isEqualTo(50);
        assertThat(second.totalAcademicReputationDelta()).isZero();assertThat(second.totalAuraDelta()).isZero();
        var rows=results.findByPlayerIdAndSemester(player.getId(),1);
        assertThat(rows).hasSize(3).allMatch(a->a.getState()==AssessmentState.MISSED && a.getMarksObtained()==0 && a.getMaxMarks()==type.maxMarks());
        assertThat(service.reportCard(jwt).courses()).allMatch(c->c.status().equals("ACTIVE"));
        catalog.override=type==AssessmentType.MIDTERM ? AssessmentType.FINAL : AssessmentType.MIDTERM;
        assertThat(service.start(jwt,"ICS",catalog.override).state()).isEqualTo("STARTED");
    }
    @Test void abandonedAttemptBecomesMissed() throws Exception {
        var a=service.start(jwt,"ICS",AssessmentType.QUIZ_1);
        service.answer(jwt,a.attemptId(),new AssessmentAnswerRequest(0,key(a)));
        days.finalizeCurrentDay(jwt);
        assertThat(results.findById(a.attemptId()).orElseThrow().getMarksObtained()).isZero();
        assertThat(results.findById(a.attemptId()).orElseThrow().getState()).isEqualTo(AssessmentState.MISSED);
    }
    @Test void coursesAreIndependentAndAllDroppedCoursesAreSkippedOnLaterFinalization() {
        for (String course:List.of("ICS","ENGLISH","DM")) {
            var a=service.start(jwt,course,AssessmentType.QUIZ_1); random.value=0;
            service.resolveCheat(jwt,a.attemptId());
            var report=service.reportCard(jwt);
            if(course.equals("ICS")) assertThat(report.courses().subList(1,3)).allMatch(c->c.status().equals("ACTIVE"));
            if(course.equals("ENGLISH")) assertThat(report.courses().get(2).status()).isEqualTo("ACTIVE");
        }
        catalog.override=AssessmentType.MIDTERM;
        days.finalizeCurrentDay(jwt);days.finalizeCurrentDay(jwt);
        assertThat(results.findByPlayerIdAndSemester(player.getId(),1)).hasSize(3);
        assertThat(stats().getAcademicReputation()).isEqualTo(20);
        assertThat(service.reportCard(jwt).courses()).allMatch(c->c.status().equals("DROPPED_CHEATING"));
    }
    @Test void normalAttendanceAndProxyAreUnavailableOnAssessmentDay() {
        for(var c:ClassroomCourseDefinition.all()) {
            assertThatThrownBy(()->attendance.startLecture(jwt,c.activityId())).hasMessageContaining("Assessment today");
            assertThatThrownBy(()->attendance.punchProxy(jwt,c.activityId())).hasMessageContaining("Assessment today");
        }
    }
    @Test void wrongDayFutureAssessmentUnknownCourseFacultyBbaAndWrongSemesterRejected() {
        assertThatThrownBy(()->service.start(jwt,"ICS",AssessmentType.MIDTERM)).hasMessageContaining("not scheduled");
        assertThatThrownBy(()->service.start(jwt,"UNKNOWN",AssessmentType.QUIZ_1)).hasMessageContaining("Unknown");
        var save=saveRepository.findByPlayerId(player.getId()).orElseThrow();
        ReflectionTestUtils.setField(save,"currentDay",1);
        assertThatThrownBy(()->service.start(jwt,"ICS",AssessmentType.QUIZ_1)).hasMessageContaining("not scheduled");
        ReflectionTestUtils.setField(save,"currentDay",2);ReflectionTestUtils.setField(save,"semester",2);
        assertThatThrownBy(()->service.start(jwt,"ICS",AssessmentType.QUIZ_1)).hasMessageContaining("not scheduled");
        ReflectionTestUtils.setField(save,"semester",1);ReflectionTestUtils.setField(save,"role",PlayerRole.FACULTY);
        assertThatThrownBy(()->service.start(jwt,"ICS",AssessmentType.QUIZ_1)).hasMessageContaining("CSE students");
        ReflectionTestUtils.setField(save,"role",PlayerRole.STUDENT);
        var bba=departments.saveAndFlush(Department.createNew("BBA","Business"));ReflectionTestUtils.setField(save,"department",bba);
        assertThatThrownBy(()->service.start(jwt,"ICS",AssessmentType.QUIZ_1)).hasMessageContaining("CSE students");
    }
    @Test void invalidAnswersAndOtherPlayersAttemptAreRejected() {
        var a=service.start(jwt,"ICS",AssessmentType.QUIZ_1);
        assertThatThrownBy(()->service.answer(jwt,a.attemptId(),new AssessmentAnswerRequest(0,4))).hasMessageContaining("Invalid answer");
        assertThatThrownBy(()->service.answer(jwt,a.attemptId(),new AssessmentAnswerRequest(2,0))).hasMessageContaining("Unexpected question");
        Jwt other=jwt("other_player");players.getOrProvisionPlayer(other);
        saves.createSave(other,new PlayerSaveCreateRequest(PlayerRole.STUDENT,"Other",cse.getId(),"011-other"));
        assertThatThrownBy(()->service.resolveCheat(other,a.attemptId())).hasMessageContaining("Unknown assessment");
    }
    @Test void newGameClearsHistoricalRowsAndDrops() {
        var a=service.start(jwt,"ICS",AssessmentType.QUIZ_1);random.value=0;service.resolveCheat(jwt,a.attemptId());
        saves.deleteSave(jwt);
        assertThat(results.findByPlayerIdAndSemester(player.getId(),1)).isEmpty();
        assertThat(enrollments.findByPlayerIdAndSemester(player.getId(),1)).isEmpty();
        assertThat(stats().getAura()).isEqualTo(50);assertThat(stats().getAcademicReputation()).isEqualTo(50);
    }
    private PlayerStats stats() { return statsRepository.findByPlayerId(player.getId()).orElseThrow(); }
    private int key(AssessmentResponse a) throws Exception {
        var row=results.findById(a.attemptId()).orElseThrow();
        return new ObjectMapper().readValue(row.getAttemptSnapshot(),AssessmentCatalog.Snapshot.class).questions().get(a.questionIndex()).correctAnswer();
    }
    private int wrongKey(AssessmentResponse a) throws Exception { int correct=key(a); return a.question().options().stream().mapToInt(AssessmentResponse.Option::index).filter(i->i!=correct).findFirst().orElseThrow(); }
    static Jwt jwt(String sub) { return Jwt.withTokenValue("test").header("alg","none").subject(sub).build(); }
    @TestConfiguration static class Config {
        @Bean TestRandom random() { return new TestRandom(); }
        @Bean TestClock clock() { return new TestClock(); }
        @Bean TestCatalog catalog() { return new TestCatalog(); }
    }
    static class TestRandom implements AssessmentRandom { double value; int calls; public double nextDouble(){ calls++; return value; } }
    static class TestClock extends Clock {
        Instant now; public ZoneId getZone(){return ZoneOffset.UTC;} public Clock withZone(ZoneId zone){return this;} public Instant instant(){return now;}
    }
    static class TestCatalog extends AssessmentCatalog {
        AssessmentType override;
        public AssessmentType scheduled(PlayerSave save) { return override != null && AssessmentCatalog.eligible(save) ? override : super.scheduled(save); }
        public Snapshot select(String course,AssessmentType type,int rep,AssessmentRandom random) {
            if(type!=AssessmentType.FINAL) return super.select(course,type,rep,random);
            var base=super.select(course,AssessmentType.MIDTERM,rep,random);
            var questions=new ArrayList<>(base.questions());
            for(int i=6;i<8;i++) { var q=base.questions().get(i-6); questions.add(new SelectedQuestion("TEST-FINAL-"+i,q.text(),q.answers(),q.correctAnswer(),q.visibleOptions())); }
            return new Snapshot(base.difficulty(),base.seconds(),questions);
        }
    }
}
