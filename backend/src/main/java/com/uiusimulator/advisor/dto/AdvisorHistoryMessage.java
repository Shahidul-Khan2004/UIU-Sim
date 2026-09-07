package com.uiusimulator.advisor.dto;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Pattern;
import jakarta.validation.constraints.Size;

public record AdvisorHistoryMessage(
        @NotBlank(message = "Role must not be blank")
        @Pattern(regexp = "user|advisor", message = "Role must be either 'user' or 'advisor'")
        String role,

        @NotBlank(message = "Content must not be blank")
        @Size(max = 500, message = "History message content must not exceed 500 characters")
        String content
) {}
