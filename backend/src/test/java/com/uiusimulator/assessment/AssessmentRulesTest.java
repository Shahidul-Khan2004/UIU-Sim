package com.uiusimulator.assessment;
import static org.assertj.core.api.Assertions.*;
import com.uiusimulator.assessment.config.*;
import com.uiusimulator.assessment.entity.*;
import com.uiusimulator.assessment.service.*;
import java.util.*;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.CsvSource;
class AssessmentRulesTest {
    @Test void assessmentDefinitionsTotal100WithFiveMarksEach() {
        assertThat(Arrays.stream(AssessmentType.values()).mapToInt(AssessmentType::maxMarks).sum()).isEqualTo(100);
        assertThat(AssessmentType.QUIZ_1.questionCount()).isEqualTo(3);
        assertThat(AssessmentType.MIDTERM.questionCount()).isEqualTo(6);
        assertThat(AssessmentType.QUIZ_2.questionCount()).isEqualTo(3);
        assertThat(AssessmentType.FINAL.questionCount()).isEqualTo(8);
        assertThat(AssessmentType.MARKS_PER_QUESTION).isEqualTo(5);
    }
    @ParameterizedTest @CsvSource({"0,VERY HARD","39,VERY HARD","40,HARD","49,HARD","50,NORMAL","59,NORMAL","60,EASY","67,EASY","68,VERY EASY","100,VERY EASY"})
    void configurableBandsAtEveryBoundary(int reputation,String band) {
        assertThat(new AssessmentCatalog().band(reputation).name()).isEqualTo(band);
    }
    @Test void selectionHasUniqueStableQuestions_andOnlyRemovesWrongDistractor() {
        var catalog=new AssessmentCatalog();
        for(var course:List.of("ICS","ENGLISH","DM")) for(int rep:List.of(0,40,50,60,68,100)) for(var type:AssessmentType.values()) {
            var selected=catalog.select(course,type,rep,()->.5);
            assertThat(selected.questions()).hasSize(type.questionCount());
            assertThat(selected.questions().stream().map(AssessmentCatalog.SelectedQuestion::id)).doesNotHaveDuplicates();
            for(var q:selected.questions()) {
                assertThat(q.visibleOptions()).contains(q.correctAnswer()).hasSize(rep>=60 ? 3 : 4);
                assertThat(q.text()).doesNotContain("[SAMPLE / TEST]");
            }
        }
        assertThat(catalog.band(0).seconds()).isLessThan(catalog.band(40).seconds());
        assertThat(catalog.band(40).seconds()).isLessThan(catalog.band(50).seconds());
        assertThat(catalog.band(50).seconds()).isLessThan(catalog.band(60).seconds());
        assertThat(catalog.band(60).seconds()).isLessThan(catalog.band(68).seconds());
    }
    @Test void eachCourseHasAReusablePoolOfAtLeastFiftyQuestions() {
        var catalog=new AssessmentCatalog();
        for(var course:List.of("ICS","ENGLISH","DM")) {
            var bank=catalog.bank(course);
            assertThat(bank).hasSizeGreaterThanOrEqualTo(50);
            assertThat(bank.stream().map(AssessmentCatalog.Question::id)).doesNotHaveDuplicates();
            assertThat(bank.stream().map(AssessmentCatalog.Question::tier).distinct()).containsExactlyInAnyOrder("EASY","NORMAL","HARD");
            assertThat(bank).allSatisfy(q -> {
                assertThat(q.courseId()).isEqualTo(course);
                assertThat(q.text()).isNotBlank().doesNotContain("[SAMPLE / TEST]");
                assertThat(q.answers()).hasSize(4).allSatisfy(a -> assertThat(a).isNotBlank());
                assertThat(q.correctAnswer()).isBetween(0,3);
                assertThat(q.tier()).isIn("EASY","NORMAL","HARD");
            });
        }
    }
    @Test void bbaSampleBanksLoadWithRequiredShapeAndSelection() {
        var catalog=new AssessmentCatalog();
        for(var course:List.of("IB","POA","BBA-ENGLISH")) {
            var bank=catalog.bank(course);
            assertThat(bank).hasSizeGreaterThanOrEqualTo(8);
            assertThat(bank.stream().map(AssessmentCatalog.Question::id)).doesNotHaveDuplicates();
            assertThat(bank.stream().map(AssessmentCatalog.Question::tier).distinct()).containsExactlyInAnyOrder("EASY","NORMAL","HARD");
            assertThat(bank).allSatisfy(q -> {
                assertThat(q.courseId()).isEqualTo(course);
                assertThat(q.answers()).hasSize(4);
                assertThat(q.correctAnswer()).isBetween(0,3);
            });
            assertThat(catalog.select(course,AssessmentType.FINAL,50,()->0.5).questions()).hasSize(8);
            assertThat(catalog.select(course,AssessmentType.QUIZ_1,50,()->0.3).questions()).hasSize(3);
        }
    }
    @Test void randomSelectionDrawsDifferentSetsAndKeepsRequiredCounts() {
        var catalog=new AssessmentCatalog();
        var first=catalog.select("ICS",AssessmentType.QUIZ_1,50,()->0.1);
        var second=catalog.select("ICS",AssessmentType.QUIZ_1,50,()->0.9);
        assertThat(first.questions()).hasSize(3);
        assertThat(second.questions()).hasSize(3);
        assertThat(first.questions().stream().map(AssessmentCatalog.SelectedQuestion::id).toList())
                .isNotEqualTo(second.questions().stream().map(AssessmentCatalog.SelectedQuestion::id).toList());
        assertThat(catalog.select("ENGLISH",AssessmentType.MIDTERM,50,()->0.4).questions()).hasSize(6);
        assertThat(catalog.select("DM",AssessmentType.QUIZ_2,50,()->0.6).questions()).hasSize(3);
        assertThat(catalog.select("ICS",AssessmentType.FINAL,50,()->0.2).questions()).hasSize(8);
        var seen=new HashSet<String>();
        var rolls=new double[]{0.05,0.2,0.35,0.5,0.65,0.8,0.95,0.15};
        var i=new int[]{0};
        AssessmentRandom cycling=()->rolls[i[0]++%rolls.length];
        for(int attempt=0;attempt<8;attempt++)
            catalog.select("ICS",AssessmentType.QUIZ_1,50,cycling).questions().forEach(q->seen.add(q.id()));
        assertThat(seen.size()).isGreaterThan(8);
        assertThat(AssessmentType.QUIZ_1.maxMarks()).isEqualTo(15);
        assertThat(AssessmentType.MIDTERM.maxMarks()).isEqualTo(30);
        assertThat(AssessmentType.QUIZ_2.maxMarks()).isEqualTo(15);
        assertThat(AssessmentType.FINAL.maxMarks()).isEqualTo(40);
    }
    @ParameterizedTest @CsvSource({"0,F,0","54,F,0","55,D,1","57,D,1","58,D+,1.33","61,D+,1.33","62,C-,1.67","65,C-,1.67","66,C,2","69,C,2","70,C+,2.33","73,C+,2.33","74,B-,2.67","77,B-,2.67","78,B,3","81,B,3","82,B+,3.33","85,B+,3.33","86,A-,3.67","89,A-,3.67","90,A,4","100,A,4"})
    void gradesRespectEveryBoundary(int marks,String grade,double point) {
        assertThat(GradeCalculator.calculate(marks).letter()).isEqualTo(grade);
        assertThat(GradeCalculator.calculate(marks).gradePoint()).isEqualTo(point);
    }
    @Test void invalidGradeBoundsRejected() {
        assertThatThrownBy(()->GradeCalculator.calculate(-1)).isInstanceOf(IllegalArgumentException.class);
        assertThatThrownBy(()->GradeCalculator.calculate(101)).isInstanceOf(IllegalArgumentException.class);
    }
}
