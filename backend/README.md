# Backend — UIU Simulator API

Spring Boot API for authentication and player persistence.

Unity talks **only** to this API. Supabase/PostgreSQL credentials never leave the backend.

## Stack

- Java 17+
- Spring Boot 3.4
- Spring Security (Clerk JWT via JWKS)
- Spring Data JPA + Flyway
- PostgreSQL (Supabase pooler)

## Quick start

1. Copy `.env.example` to `.env` and fill in values (see below).
2. Export the variables into your shell (or use your IDE env config):

```bash
set -a && source .env && set +a
./mvnw spring-boot:run
# or: mvn spring-boot:run
```

3. Open the auth page: [http://localhost:8080/auth/login](http://localhost:8080/auth/login)

## Required environment variables

| Variable | Purpose |
|----------|---------|
| `DATABASE_URL` | JDBC URL (Supabase pooler) |
| `DATABASE_USERNAME` | Pooler username |
| `DATABASE_PASSWORD` | Database password |
| `CLERK_PUBLISHABLE_KEY` | Browser auth page only |
| `CLERK_ISSUER` | Clerk Frontend API issuer URL |
| `CLERK_JWKS_URL` | `https://<issuer-host>/.well-known/jwks.json` |
| `CLERK_AUTHORIZED_PARTIES` | Comma-separated `azp` allow-list (include `http://localhost:8080`) |

Never commit real secrets. Never expose `CLERK_SECRET_KEY` to Unity or the auth HTML page.

## Clerk Dashboard checklist

1. Use application `app_3IhCzPT7JDek4JtFgWOxbeKcs9k`.
2. Allow origin `http://localhost:8080`.
3. Optionally add `email` / `username` to the session JWT template so player profiles are richer.
4. Confirm JWKS URL matches `CLERK_JWKS_URL`.

## Main endpoints

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/auth/login` | Public | Clerk JS sign-in page → `uiusim://auth/callback?token=...` |
| `POST` | `/api/auth/login` | Bearer JWT | Validate Clerk token, upsert player, return profile |
| `GET` | `/api/players/me` | Bearer JWT | Current player profile |
| `GET` | `/actuator/health` | Public | Health check |

## Tests

```bash
mvn test
```

## Package layout

```text
com.uiusimulator
├── config
├── auth (controller, service, dto, security, exception)
├── player (controller, service, repository, entity, dto)
└── common (exception, response, logging)
```
