package com.uiusimulator.advisor.model;

public enum StudentStandingBand {
    LOW,
    MODERATE,
    HIGH;

    public static StudentStandingBand fromStat(int value) {
        if (value < 40) {
            return LOW;
        }
        if (value < 70) {
            return MODERATE;
        }
        return HIGH;
    }
}
