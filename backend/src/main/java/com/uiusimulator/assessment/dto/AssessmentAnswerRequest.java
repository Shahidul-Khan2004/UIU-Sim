package com.uiusimulator.assessment.dto;
import jakarta.validation.constraints.*;
public record AssessmentAnswerRequest(@NotNull @Min(0) @Max(7) Integer questionIndex,
        @NotNull @Min(-1) @Max(3) Integer answerIndex) {}
