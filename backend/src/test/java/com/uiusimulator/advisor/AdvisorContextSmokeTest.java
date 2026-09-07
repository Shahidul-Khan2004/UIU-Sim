package com.uiusimulator.advisor;

import static org.assertj.core.api.Assertions.assertThat;

import com.uiusimulator.advisor.controller.AdvisorController;
import com.uiusimulator.advisor.service.AdvisorService;
import com.uiusimulator.advisor.service.GeminiAdvisorClient;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.context.ApplicationContext;
import org.springframework.test.context.ActiveProfiles;

/**
 * Smoke test verifying that the real Advisor bean graph initializes successfully
 * within the Spring ApplicationContext, preventing regressions where constructor
 * injection or configuration properties binding fails at runtime.
 */
@SpringBootTest
@ActiveProfiles("test")
class AdvisorContextSmokeTest {

    @Autowired
    private ApplicationContext applicationContext;

    @Autowired
    private GeminiAdvisorClient geminiAdvisorClient;

    @Autowired
    private AdvisorService advisorService;

    @Autowired
    private AdvisorController advisorController;

    @Test
    void applicationContextLoadsAndAdvisorBeanGraphExists() {
        assertThat(applicationContext).isNotNull();
        assertThat(geminiAdvisorClient).isNotNull();
        assertThat(advisorService).isNotNull();
        assertThat(advisorController).isNotNull();

        assertThat(applicationContext.getBean(GeminiAdvisorClient.class)).isSameAs(geminiAdvisorClient);
        assertThat(applicationContext.getBean(AdvisorService.class)).isSameAs(advisorService);
        assertThat(applicationContext.getBean(AdvisorController.class)).isSameAs(advisorController);
    }
}
