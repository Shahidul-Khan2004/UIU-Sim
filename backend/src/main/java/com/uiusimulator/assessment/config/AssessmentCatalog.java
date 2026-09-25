package com.uiusimulator.assessment.config;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.uiusimulator.assessment.entity.AssessmentType;
import com.uiusimulator.assessment.service.AssessmentRandom;
import com.uiusimulator.player.entity.*;
import org.springframework.core.io.ClassPathResource;
import org.springframework.stereotype.Component;
import java.io.IOException;
import java.util.*;

/** Editable server configuration. Only Semester 1 / Day 2 is scheduled. */
@Component
public class AssessmentCatalog {
    public record Band(String name, int minimum, int seconds, double easy, double normal, double hard, int distractorsRemoved) {
        double weight(String tier) { return switch (tier) { case "EASY" -> easy; case "NORMAL" -> normal; case "HARD" -> hard; default -> throw new IllegalArgumentException("Unknown tier"); }; }
    }
    public record Question(String id, String courseId, String text, List<String> answers, int correctAnswer, String tier) {}
    public record SelectedQuestion(String id, String text, List<String> answers, int correctAnswer, List<Integer> visibleOptions) {}
    public record Snapshot(String difficulty, int seconds, List<SelectedQuestion> questions) {}
    public record Schedule(int semester, int day, AssessmentType type) {}
    public record Configuration(List<Band> bands, List<Schedule> schedule) {}
    private final Configuration configuration;
    private final Map<String, List<Question>> banks = new HashMap<>();
    public AssessmentCatalog() {
        var mapper = new ObjectMapper();
        try {
            try (var input = new ClassPathResource("assessments/config.json").getInputStream()) {
                configuration = mapper.readValue(input, Configuration.class);
            }
            for (var course : ClassroomCourseDefinition.all()) {
                try (var input = new ClassPathResource("assessments/" + course.courseId().toLowerCase(Locale.ROOT) + "-sample.json").getInputStream()) {
                    var bank = List.of(mapper.readValue(input, Question[].class));
                    var ids = new HashSet<String>();
                    for (var q : bank) {
                        if (!ids.add(q.id()) || !course.courseId().equals(q.courseId()) || q.answers().size() != 4 || q.correctAnswer() < 0 || q.correctAnswer() > 3
                                || !List.of("EASY", "NORMAL", "HARD").contains(q.tier())) throw new IllegalArgumentException("Invalid question bank");
                    }
                    banks.put(course.courseId(), bank);
                }
            }
            validate();
        } catch (IOException e) { throw new IllegalStateException("Cannot load assessment configuration", e); }
    }
    private void validate() {
        var bands = configuration.bands();
        if (bands.isEmpty() || bands.get(0).minimum() != 0) throw new IllegalArgumentException("Bands must start at zero");
        int previous = -1;
        for (var b : bands) {
            if (b.minimum() <= previous || b.minimum() > 100 || b.seconds() < 1 || b.distractorsRemoved() < 0 || b.distractorsRemoved() > 1
                || b.easy() <= 0 || b.normal() <= 0 || b.hard() <= 0) throw new IllegalArgumentException("Invalid difficulty band");
            previous = b.minimum();
        }
        var days = new HashSet<String>();
        for (var s : configuration.schedule()) {
            if (!days.add(s.semester() + ":" + s.day()) || s.semester() < 1 || s.day() < 1 || s.type() == null)
                throw new IllegalArgumentException("Invalid assessment schedule");
            for (var bank : banks.values()) if (bank.size() < s.type().questionCount())
                throw new IllegalArgumentException("Add enough real questions before scheduling this assessment");
        }
    }
    public static boolean eligible(PlayerSave save) {
        return save.getRole() == PlayerRole.STUDENT && save.getDepartment() != null && "CSE".equalsIgnoreCase(save.getDepartment().getCode());
    }
    public AssessmentType scheduled(PlayerSave save) {
        if (!eligible(save)) return null;
        return configuration.schedule().stream().filter(s -> s.semester() == save.getSemester() && s.day() == save.getCurrentDay())
                .map(Schedule::type).findFirst().orElse(null);
    }
    public Band band(int reputation) {
        return configuration.bands().stream().filter(b -> reputation >= b.minimum()).reduce((a,b) -> b).orElseThrow();
    }
    public Snapshot select(String course, AssessmentType type, int reputation, AssessmentRandom random) {
        var band = band(reputation);
        var pool = new ArrayList<>(banks.get(course));
        if (pool.size() < type.questionCount()) throw new IllegalArgumentException("Not enough questions configured.");
        var selected = new ArrayList<SelectedQuestion>();
        for (int i = 0; i < type.questionCount(); i++) {
            double roll = random.nextDouble() * pool.stream().mapToDouble(q -> band.weight(q.tier())).sum();
            int index = 0;
            while (index < pool.size() - 1 && (roll -= band.weight(pool.get(index).tier())) >= 0) index++;
            var q = pool.remove(index);
            var options = new ArrayList<>(List.of(0,1,2,3));
            if (band.distractorsRemoved() == 1) {
                var wrong = options.stream().filter(a -> a != q.correctAnswer()).toList();
                options.remove(wrong.get(Math.min(2, (int)(random.nextDouble() * 3))));
            }
            selected.add(new SelectedQuestion(q.id(), q.text(), q.answers(), q.correctAnswer(), options));
        }
        return new Snapshot(band.name(), band.seconds(), selected);
    }
    public static ClassroomCourseDefinition course(String courseId) {
        return ClassroomCourseDefinition.all().stream().filter(c -> c.courseId().equals(courseId)).findFirst()
                .orElseThrow(() -> new IllegalArgumentException("Unknown assessment course."));
    }
}
