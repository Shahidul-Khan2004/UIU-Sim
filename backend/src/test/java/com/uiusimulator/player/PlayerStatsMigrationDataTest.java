package com.uiusimulator.player;

import static org.assertj.core.api.Assertions.assertThat;

import java.io.InputStream;
import java.nio.charset.StandardCharsets;
import java.sql.Connection;
import java.sql.PreparedStatement;
import java.sql.ResultSet;
import java.sql.Statement;
import java.sql.Timestamp;
import java.time.Instant;
import java.util.ArrayList;
import java.util.List;
import java.util.UUID;
import org.h2.jdbcx.JdbcDataSource;
import org.junit.jupiter.api.Test;

class PlayerStatsMigrationDataTest {

    @Test
    void migrationV3_preservesExistingV2PlayerStatsAndDropsOldColumns() throws Exception {
        // 1. Set up in-memory PostgreSQL-mode H2 DataSource
        JdbcDataSource dataSource = new JdbcDataSource();
        dataSource.setURL("jdbc:h2:mem:migration_test_" + System.currentTimeMillis() + ";MODE=PostgreSQL;DATABASE_TO_LOWER=TRUE;DB_CLOSE_DELAY=-1");
        dataSource.setUser("sa");
        dataSource.setPassword("");

        try (Connection conn = dataSource.getConnection();
             Statement stmt = conn.createStatement()) {
            // 2. Establish schema at V2
            stmt.execute("CREATE TABLE players (" +
                    "    id UUID PRIMARY KEY," +
                    "    clerk_user_id TEXT NOT NULL UNIQUE," +
                    "    email TEXT," +
                    "    username TEXT," +
                    "    created_at TIMESTAMP WITH TIME ZONE NOT NULL," +
                    "    last_login TIMESTAMP WITH TIME ZONE," +
                    "    aura INTEGER NOT NULL DEFAULT 50," +
                    "    academic_reputation INTEGER NOT NULL DEFAULT 50," +
                    "    CONSTRAINT chk_players_aura CHECK (aura >= 0 AND aura <= 100)," +
                    "    CONSTRAINT chk_players_academic_reputation CHECK (academic_reputation >= 0 AND academic_reputation <= 100)" +
                    ")");
        }

        // 3. Insert a Player with non-default stats at V2
        UUID playerId = UUID.randomUUID();
        String clerkUserId = "clerk_v2_user";
        Instant now = Instant.now();

        try (Connection conn = dataSource.getConnection();
             PreparedStatement ps = conn.prepareStatement(
                     "INSERT INTO players (id, clerk_user_id, email, username, created_at, last_login, aura, academic_reputation) " +
                     "VALUES (?, ?, ?, ?, ?, ?, ?, ?)"
             )) {
            ps.setObject(1, playerId);
            ps.setString(2, clerkUserId);
            ps.setString(3, "test@uiu.edu");
            ps.setString(4, "v2student");
            ps.setTimestamp(5, Timestamp.from(now));
            ps.setTimestamp(6, Timestamp.from(now));
            ps.setInt(7, 75);
            ps.setInt(8, 35);
            ps.executeUpdate();
        }

        // Verify V2 row has aura = 75, academic_reputation = 35 before migration
        try (Connection conn = dataSource.getConnection();
             PreparedStatement ps = conn.prepareStatement("SELECT aura, academic_reputation FROM players WHERE id = ?")) {
            ps.setObject(1, playerId);
            try (ResultSet rs = ps.executeQuery()) {
                assertThat(rs.next()).isTrue();
                assertThat(rs.getInt("aura")).isEqualTo(75);
                assertThat(rs.getInt("academic_reputation")).isEqualTo(35);
            }
        }

        // 4. Read and apply the exact V3 migration script
        String v3Sql;
        try (InputStream is = getClass().getResourceAsStream("/db/migration/V3__move_player_stats_to_player_stats_table.sql")) {
            assertThat(is).as("V3 migration script must exist").isNotNull();
            v3Sql = new String(is.readAllBytes(), StandardCharsets.UTF_8);
        }

        try (Connection conn = dataSource.getConnection()) {
            conn.setAutoCommit(false);
            try (Statement stmt = conn.createStatement()) {
                // Split statements by semicolon and execute in transaction
                for (String sqlStatement : v3Sql.split(";")) {
                    String trimmed = sqlStatement.trim();
                    if (!trimmed.isEmpty()) {
                        stmt.execute(trimmed);
                    }
                }
            }
            conn.commit();
        }

        // 5. Verify player_stats contains the row with exact values preserved
        try (Connection conn = dataSource.getConnection();
             PreparedStatement ps = conn.prepareStatement(
                     "SELECT player_id, aura, academic_reputation FROM player_stats WHERE player_id = ?"
             )) {
            ps.setObject(1, playerId);
            try (ResultSet rs = ps.executeQuery()) {
                assertThat(rs.next()).isTrue();
                assertThat(rs.getObject("player_id", UUID.class)).isEqualTo(playerId);
                assertThat(rs.getInt("aura")).isEqualTo(75);
                assertThat(rs.getInt("academic_reputation")).isEqualTo(35);
            }
        }

        // 6. Verify the old columns no longer exist on players
        try (Connection conn = dataSource.getConnection();
             ResultSet columns = conn.getMetaData().getColumns(null, null, "players", null)) {
            List<String> colNames = new ArrayList<>();
            while (columns.next()) {
                colNames.add(columns.getString("COLUMN_NAME").toLowerCase());
            }
            assertThat(colNames).contains("id", "clerk_user_id", "email", "username", "created_at", "last_login");
            assertThat(colNames).doesNotContain("aura", "academic_reputation");
        }
    }
}
