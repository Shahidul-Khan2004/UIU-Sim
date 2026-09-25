package com.uiusimulator.assessment;
import static org.assertj.core.api.Assertions.*;
import static org.mockito.Mockito.*;
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
import java.util.concurrent.*;
import org.junit.jupiter.api.*;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.orm.jpa.DataJpaTest;
import org.springframework.context.annotation.Import;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.test.context.ActiveProfiles;
import org.springframework.test.context.bean.override.mockito.MockitoSpyBean;
import org.springframework.transaction.annotation.*;
import org.springframework.transaction.PlatformTransactionManager;
import org.springframework.transaction.support.TransactionTemplate;

/** Real independent transactions exercise serialization and atomic rollback, not only entity methods. */
@DataJpaTest(showSql=false)
@ActiveProfiles("test")
@Import({PlayerService.class,PlayerSaveService.class,PlayerActivityService.class,PlayerDayService.class,
    AttendIcsService.class,AssessmentService.class,CourseEnrollmentService.class,CheatPolicy.class,
    AssessmentServiceTest.Config.class})
@Transactional(propagation=Propagation.NOT_SUPPORTED)
class AssessmentTransactionTest {
    @Autowired AssessmentService service;
    @Autowired PlayerService players;
    @Autowired PlayerSaveService saves;
    @Autowired PlayerSaveRepository saveRepository;
    @Autowired PlayerStatsRepository stats;
    @Autowired PlayerAssessmentResultRepository results;
    @MockitoSpyBean PlayerCourseEnrollmentRepository enrollments;
    @Autowired DepartmentRepository departments;
    @Autowired PlayerRepository playerRepository;
    @Autowired PlayerDayService days;
    @Autowired AssessmentServiceTest.TestRandom random;
    @Autowired AssessmentServiceTest.TestClock clock;
    @Autowired AssessmentServiceTest.TestCatalog catalog;
    @Autowired PlatformTransactionManager manager;
    Jwt jwt; Player player;
    @BeforeEach void setup() {
        random.value=.99;random.calls=0;catalog.override=null;clock.now=Instant.parse("2026-01-01T00:00:00Z");
        jwt=AssessmentServiceTest.jwt("tx_"+UUID.randomUUID());
        tx().executeWithoutResult(s->{
            var cse=departments.findAll().stream().filter(d->d.getCode().equals("CSE")).findFirst()
                .orElseGet(()->departments.saveAndFlush(Department.createNew("CSE","Computer Science")));
            player=players.getOrProvisionPlayer(jwt);
            saves.createSave(jwt,new PlayerSaveCreateRequest(PlayerRole.STUDENT,"Test",cse.getId(),"011-"+UUID.randomUUID()));
            saveRepository.findByPlayerId(player.getId()).orElseThrow().advanceToNextDay();
        });
    }
    @AfterEach void cleanup() {
        reset(enrollments);
        saves.deleteSave(jwt);
        tx().executeWithoutResult(s->{stats.deleteById(player.getId());playerRepository.deleteById(player.getId());});
    }
    @Test void simultaneousStartAndCheatRequestsReturnOneCanonicalAttemptAndPenalty() throws Exception {
        var pool=Executors.newFixedThreadPool(2);
        try {
            var gate=new CountDownLatch(1);
            Callable<AssessmentResponse> start=()->{gate.await();return service.start(jwt,"ICS",AssessmentType.QUIZ_1);};
            var one=pool.submit(start);var two=pool.submit(start);gate.countDown();
            var a=one.get(10,TimeUnit.SECONDS);assertThat(two.get(10,TimeUnit.SECONDS).attemptId()).isEqualTo(a.attemptId());
            random.value=0;random.calls=0;
            var cheatGate=new CountDownLatch(1);
            Callable<AssessmentResponse> cheat=()->{cheatGate.await();return service.resolveCheat(jwt,a.attemptId());};
            var c1=pool.submit(cheat);var c2=pool.submit(cheat);cheatGate.countDown();
            assertThat(c1.get(10,TimeUnit.SECONDS).aura()).isEqualTo(45);
            assertThat(c2.get(10,TimeUnit.SECONDS).aura()).isEqualTo(45);
            assertThat(random.calls).isEqualTo(1);
            assertThat(results.findByPlayerIdAndSemester(player.getId(),1)).hasSize(1);
            assertThat(enrollments.findByPlayerIdAndSemester(player.getId(),1)).hasSize(1);
            assertThat(service.reportCard(jwt).courses().get(0).grade()).isEqualTo("F");
        } finally {pool.shutdownNow();}
    }
    @Test void failureDuringDropRollsBackStatsResultAndCourseTogether() {
        var a=service.start(jwt,"ICS",AssessmentType.QUIZ_1);random.value=0;
        doThrow(new IllegalStateException("test persistence failure")).when(enrollments).save(any(PlayerCourseEnrollment.class));
        assertThatThrownBy(()->service.resolveCheat(jwt,a.attemptId())).hasMessageContaining("test persistence failure");
        reset(enrollments);
        assertThat(stats.findByPlayerId(player.getId()).orElseThrow().getAura()).isEqualTo(50);
        assertThat(stats.findByPlayerId(player.getId()).orElseThrow().getAcademicReputation()).isEqualTo(50);
        assertThat(results.findById(a.attemptId()).orElseThrow().getState()).isEqualTo(AssessmentState.STARTED);
        assertThat(enrollments.findByPlayerIdAndSemesterAndCourseId(player.getId(),1,"ICS").orElseThrow().getStatus()).isEqualTo(CourseStatus.ACTIVE);
        assertThat(service.resolveCheat(jwt,a.attemptId()).state()).isEqualTo("CHEAT_CAUGHT");
    }
    @Test void concurrentFinalizeAndCheatProduceOneTerminalResult() throws Exception {
        var a=service.start(jwt,"ICS",AssessmentType.QUIZ_1);random.value=0;
        var pool=Executors.newFixedThreadPool(2);var gate=new CountDownLatch(1);
        try {
            var cheat=pool.submit(()->{gate.await();return service.resolveCheat(jwt,a.attemptId());});
            var finalize=pool.submit(()->{gate.await();return days.finalizeCurrentDay(jwt);});gate.countDown();
            var result=cheat.get(10,TimeUnit.SECONDS);finalize.get(10,TimeUnit.SECONDS);
            var stored=results.findById(a.attemptId()).orElseThrow();
            assertThat(stored.getState().name()).isEqualTo(result.state());
            assertThat(stored.getState()).isIn(AssessmentState.MISSED,AssessmentState.CHEAT_CAUGHT);
            var savedStats=stats.findByPlayerId(player.getId()).orElseThrow();
            assertThat(savedStats.getAcademicReputation()).isEqualTo(stored.getState()==AssessmentState.CHEAT_CAUGHT?40:50);
            assertThat(results.findByPlayerIdAndSemester(player.getId(),1)).hasSize(3);
        }finally {pool.shutdownNow();}
    }
    private TransactionTemplate tx(){return new TransactionTemplate(manager);}
}
