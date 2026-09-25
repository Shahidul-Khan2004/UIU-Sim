package com.uiusimulator.assessment.service;
import java.security.SecureRandom;
import org.springframework.stereotype.Component;
@Component
public class SecureAssessmentRandom implements AssessmentRandom {
    private final SecureRandom random = new SecureRandom();
    public double nextDouble() { return random.nextDouble(); }
}
