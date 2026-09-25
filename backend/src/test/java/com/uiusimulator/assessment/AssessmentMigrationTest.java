package com.uiusimulator.assessment;
import static org.assertj.core.api.Assertions.*;
import java.sql.*;
import java.nio.charset.StandardCharsets;
import java.util.UUID;
import org.junit.jupiter.api.Test;
import org.h2.jdbcx.JdbcDataSource;
class AssessmentMigrationTest {
    @Test void forwardMigrationEnforcesUniquenessBoundsAndDropIntegrity() throws Exception {
        var ds=new JdbcDataSource();ds.setURL("jdbc:h2:mem:assessment_"+UUID.randomUUID()+";MODE=PostgreSQL;DATABASE_TO_LOWER=TRUE");
        try(var c=ds.getConnection();var s=c.createStatement()) {
            s.execute("CREATE DOMAIN IF NOT EXISTS TIMESTAMPTZ AS TIMESTAMP WITH TIME ZONE");
            s.execute("CREATE TABLE players(id UUID PRIMARY KEY)");
            try(var input=getClass().getResourceAsStream("/db/migration/V11__assessments_and_course_enrollments.sql")) {
                for(var sql:new String(input.readAllBytes(),StandardCharsets.UTF_8).split(";")) if(!sql.isBlank()) s.execute(sql);
            }
            var player=UUID.randomUUID();s.execute("INSERT INTO players VALUES ('"+player+"')");
            String prefix="INSERT INTO player_course_enrollments(id,player_id,semester,course_id,status,created_at,updated_at) VALUES ('";
            s.execute(prefix+UUID.randomUUID()+"','"+player+"',1,'ICS','ACTIVE',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP)");
            assertThatThrownBy(()->s.execute(prefix+UUID.randomUUID()+"','"+player+"',1,'ICS','ACTIVE',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP)")).isInstanceOf(SQLException.class);
            assertThatThrownBy(()->s.execute("UPDATE player_course_enrollments SET status='DROPPED_CHEATING'")).isInstanceOf(SQLException.class);
            String result="INSERT INTO player_assessment_results(id,player_id,semester,day_number,course_id,assessment_type,state,marks_obtained,max_marks,completed_at,created_at,updated_at) VALUES ('";
            s.execute(result+UUID.randomUUID()+"','"+player+"',1,2,'ICS','QUIZ_1','COMPLETED',10,15,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP)");
            assertThatThrownBy(()->s.execute("UPDATE player_assessment_results SET marks_obtained=20")).isInstanceOf(SQLException.class);
            assertThatThrownBy(()->s.execute("UPDATE player_assessment_results SET max_marks=30")).isInstanceOf(SQLException.class);
            assertThatThrownBy(()->s.execute("UPDATE player_assessment_results SET state='CHEAT_CAUGHT'")).isInstanceOf(SQLException.class);
            assertThatThrownBy(()->s.execute("UPDATE player_assessment_results SET aura_delta=10")).isInstanceOf(SQLException.class);
            assertThatThrownBy(()->s.execute(result+UUID.randomUUID()+"','"+player+"',1,2,'ICS','QUIZ_1','COMPLETED',0,15,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP)")).isInstanceOf(SQLException.class);
        }
    }
}
