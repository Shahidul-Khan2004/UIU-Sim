package com.uiusimulator.player.entity;

import java.util.regex.Pattern;

/**
 * Server-authoritative reward for completing a campus student NPC conversation.
 * One activity row per NPC ({@code NPC_TALK_<NPC_ID>}) makes the reward first-completion-per-day,
 * because current-day rows are unique per player and cleared on day advance.
 * Faculty players may complete conversations but never earn Aura from them.
 */
public final class NpcConversationDefinition {

    public static final String ACTIVITY_PREFIX = "NPC_TALK_";
    public static final String OUTCOME_COMPLETED = "COMPLETED";
    public static final int STUDENT_AURA_REWARD = 2;

    // activity_id is VARCHAR(64); the prefix uses 9 characters.
    private static final Pattern ACTIVITY_ID_PATTERN = Pattern.compile("^NPC_TALK_[A-Z0-9_]{1,40}$");

    private NpcConversationDefinition() {
    }

    public static boolean isNpcConversation(String activityId) {
        return activityId != null && activityId.startsWith(ACTIVITY_PREFIX);
    }

    public static ActivityOutcomeDefinition outcomeFor(String activityId, String outcomeRaw, PlayerRole role) {
        if (!ACTIVITY_ID_PATTERN.matcher(activityId).matches()) {
            throw new IllegalArgumentException("Unsupported NPC conversation activityId: " + activityId);
        }
        if (!OUTCOME_COMPLETED.equals(outcomeRaw)) {
            throw new IllegalArgumentException("Unsupported NPC conversation outcome: " + outcomeRaw);
        }

        int auraReward = role == PlayerRole.STUDENT ? STUDENT_AURA_REWARD : 0;
        return new ActivityOutcomeDefinition(ActivityStatus.COMPLETED, OUTCOME_COMPLETED, auraReward, 0);
    }
}
