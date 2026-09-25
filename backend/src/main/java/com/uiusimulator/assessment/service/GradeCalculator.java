package com.uiusimulator.assessment.service;
public final class GradeCalculator {
    public record Grade(String letter, double gradePoint, String description) {}
    private static final int[] MINIMUM = {90,86,82,78,74,70,66,62,58,55,0};
    private static final String[] LETTER = {"A","A-","B+","B","B-","C+","C","C-","D+","D","F"};
    private static final double[] POINT = {4,3.67,3.33,3,2.67,2.33,2,1.67,1.33,1,0};
    private static final String[] DESCRIPTION = {"Outstanding","Excellent","Very Good","Good","Above Average","Average","Below Average","Poor","Very Poor","Pass","Fail"};
    private GradeCalculator() {}
    public static Grade calculate(int marks) {
        if (marks < 0 || marks > 100) throw new IllegalArgumentException("Course marks must be 0–100.");
        for (int i = 0; i < MINIMUM.length; i++)
            if (marks >= MINIMUM[i]) return new Grade(LETTER[i], POINT[i], DESCRIPTION[i]);
        throw new IllegalStateException();
    }
}
