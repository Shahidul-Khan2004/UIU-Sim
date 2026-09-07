package com.uiusimulator.advisor.model;

import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.CsvSource;

import static org.junit.jupiter.api.Assertions.assertEquals;

class StudentStandingBandTest {

    @ParameterizedTest
    @CsvSource({
            "0, LOW",
            "1, LOW",
            "38, LOW",
            "39, LOW",
            "40, MODERATE",
            "41, MODERATE",
            "68, MODERATE",
            "69, MODERATE",
            "70, HIGH",
            "71, HIGH",
            "99, HIGH",
            "100, HIGH"
    })
    void fromStat_boundaries(int stat, StudentStandingBand expected) {
        assertEquals(expected, StudentStandingBand.fromStat(stat));
    }
}
