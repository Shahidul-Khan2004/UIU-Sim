package com.uiusimulator.assessment.config;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;
@Component
public record CheatPolicy(double caughtProbability, int successAura, int caughtAura, int caughtAcademic) {
    public CheatPolicy(@Value("${uiu.assessment.cheat.caught-probability:0.60}") double caughtProbability,
            @Value("${uiu.assessment.cheat.success-aura:10}") int successAura,
            @Value("${uiu.assessment.cheat.caught-aura:-5}") int caughtAura,
            @Value("${uiu.assessment.cheat.caught-academic:-10}") int caughtAcademic) {
        if (caughtProbability < 0 || caughtProbability > 1 || !Double.isFinite(caughtProbability))
            throw new IllegalArgumentException("Invalid caught probability.");
        this.caughtProbability = caughtProbability; this.successAura = successAura;
        this.caughtAura = caughtAura; this.caughtAcademic = caughtAcademic;
    }
}
