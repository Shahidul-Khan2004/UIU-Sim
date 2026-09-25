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
        for(var course:List.of("ICS","ENGLISH","DM")) for(int rep:List.of(0,40,50,60,68,100)) {
            var selected=catalog.select(course,AssessmentType.QUIZ_1,rep,()->.5);
            assertThat(selected.questions()).hasSize(3);
            assertThat(selected.questions().stream().map(AssessmentCatalog.SelectedQuestion::id)).doesNotHaveDuplicates();
            for(var q:selected.questions()) {
                assertThat(q.visibleOptions()).contains(q.correctAnswer()).hasSize(rep>=60 ? 3 : 4);
                assertThat(q.text()).startsWith("[SAMPLE / TEST]");
            }
        }
        assertThat(catalog.band(0).seconds()).isLessThan(catalog.band(40).seconds());
        assertThat(catalog.band(40).seconds()).isLessThan(catalog.band(50).seconds());
        assertThat(catalog.band(50).seconds()).isLessThan(catalog.band(60).seconds());
        assertThat(catalog.band(60).seconds()).isLessThan(catalog.band(68).seconds());
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
