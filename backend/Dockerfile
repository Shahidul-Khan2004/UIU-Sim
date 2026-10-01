# Render deployment image only. Local development uses: mvn spring-boot:run
# Multi-stage build: Maven (compile) -> JRE (run)

FROM maven:3.9-eclipse-temurin-17 AS build
WORKDIR /app

# Layer cache: resolve dependencies before copying sources
COPY pom.xml .
RUN mvn -B -q dependency:go-offline

COPY src ./src
RUN mvn -B -q clean package -DskipTests

FROM eclipse-temurin:17-jre AS runtime
WORKDIR /app

RUN useradd --system --no-create-home appuser
USER appuser

COPY --from=build /app/target/uiu-simulator-backend-0.1.0-SNAPSHOT.jar app.jar

# Render injects PORT; Spring reads server.port=${PORT:${SERVER_PORT:8080}}
EXPOSE 8080

ENTRYPOINT ["java", "-jar", "app.jar"]
