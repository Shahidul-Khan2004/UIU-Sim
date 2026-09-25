package com.uiusimulator.assessment;
import static org.mockito.Mockito.*;
import static org.mockito.ArgumentMatchers.*;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.jwt;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.*;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.*;
import com.uiusimulator.assessment.controller.AssessmentController;
import com.uiusimulator.assessment.entity.AssessmentType;
import com.uiusimulator.assessment.dto.*;
import com.uiusimulator.assessment.service.AssessmentService;
import com.uiusimulator.auth.security.ClerkJwtAuthenticationFilter;
import com.uiusimulator.common.exception.GlobalExceptionHandler;
import com.uiusimulator.config.*;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.context.properties.EnableConfigurationProperties;
import org.springframework.boot.test.autoconfigure.web.servlet.WebMvcTest;
import org.springframework.context.annotation.Import;
import org.springframework.http.MediaType;
import org.springframework.security.oauth2.jwt.JwtDecoder;
import org.springframework.test.context.ActiveProfiles;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;
import java.util.*;
@WebMvcTest(AssessmentController.class)
@ActiveProfiles("test")
@EnableConfigurationProperties(ClerkProperties.class)
@Import({SecurityConfig.class,ClerkJwtAuthenticationFilter.class,GlobalExceptionHandler.class})
class AssessmentControllerTest {
    @Autowired MockMvc mvc;
    @MockitoBean AssessmentService service;
    @MockitoBean JwtDecoder decoder;
    @Test void reportCardSerializesPendingAndFinalCgpa() throws Exception {
        when(service.reportCard(any())).thenReturn(new ReportCardResponse(1, 3, "MIDTERM", List.of(), null));
        mvc.perform(get("/api/players/me/report-card").with(jwt())).andExpect(status().isOk())
            .andExpect(jsonPath("$.cgpa").value(org.hamcrest.Matchers.nullValue())).andExpect(jsonPath("$.cgpaStatus").value("PENDING"));
        when(service.reportCard(any())).thenReturn(new ReportCardResponse(1, 6, "FINAL", List.of(), 2.33));
        mvc.perform(get("/api/players/me/report-card").with(jwt())).andExpect(status().isOk())
            .andExpect(jsonPath("$.cgpa").value(2.33)).andExpect(jsonPath("$.cgpaStatus").value("FINAL"));
    }
    @Test void unauthenticatedRequestsAreRejected() throws Exception {
        mvc.perform(get("/api/players/me/report-card")).andExpect(status().isUnauthorized());
        mvc.perform(post("/api/players/me/assessments/ICS/QUIZ_1/start")).andExpect(status().isUnauthorized());
    }
    @Test void authenticatedPrincipalAndDefinitionAreForwarded() throws Exception {
        mvc.perform(post("/api/players/me/assessments/ICS/QUIZ_1/start").with(jwt())).andExpect(status().isOk());
        verify(service).start(argThat(j->j.getSubject().equals("user")),eq("ICS"),eq(AssessmentType.QUIZ_1));
    }
    @Test void invalidAnswerBoundsMissingAnswerAndMalformedPathsAre400() throws Exception {
        var id=UUID.randomUUID();
        for(var body:List.of("{}","{\"questionIndex\":0,\"answerIndex\":4}","{\"questionIndex\":-1,\"answerIndex\":0}","{\"marksObtained\":999}","not json"))
            mvc.perform(post("/api/players/me/assessments/"+id+"/answer").with(jwt()).contentType(MediaType.APPLICATION_JSON).content(body))
                .andExpect(status().isBadRequest());
        mvc.perform(post("/api/players/me/assessments/not-a-uuid/cheat").with(jwt())).andExpect(status().isBadRequest());
        mvc.perform(post("/api/players/me/assessments/ICS/BOGUS/start").with(jwt())).andExpect(status().isBadRequest());
        verifyNoInteractions(service);
    }
    @Test void cheatPayloadCannotChooseOutcomeOrStats() throws Exception {
        var id=UUID.randomUUID();
        mvc.perform(post("/api/players/me/assessments/"+id+"/cheat").with(jwt())
                .contentType(MediaType.APPLICATION_JSON).content("{\"caught\":false,\"marksObtained\":100,\"auraDelta\":100}"))
            .andExpect(status().isOk());
        verify(service).resolveCheat(any(),eq(id));
    }
}
