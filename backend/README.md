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
2. Start the API. `.env` in this directory is loaded automatically:

```bash
mvn spring-boot:run
```

3. Open the auth page: [http://localhost:8080/auth/login](http://localhost:8080/auth/login)

## Port binding

- Local: `SERVER_PORT` from `.env` (default `8080`).
- Render: injects `PORT`. Spring reads `server.port=${PORT:${SERVER_PORT:8080}}`.

## Required environment variables

| Variable | Purpose |
|----------|---------|
| `DATABASE_URL` | JDBC URL (Supabase pooler) |
| `DATABASE_USERNAME` | Pooler username |
| `DATABASE_PASSWORD` | Database password |
| `CLERK_PUBLISHABLE_KEY` | Browser auth page only |
| `CLERK_SECRET_KEY` | Server-only Clerk Backend API (token refresh) |
| `CLERK_ISSUER` | Clerk Frontend API issuer URL |
| `CLERK_JWKS_URL` | `https://<issuer-host>/.well-known/jwks.json` |
| `CLERK_AUTHORIZED_PARTIES` | Comma-separated `azp` allow-list (include auth page origin) |

### Public Unity build keys (also in `.env`, not server secrets)

| Variable | Purpose |
|----------|---------|
| `UIU_BACKEND_MODE` | `LOCAL` or `REMOTE` — baked into Unity at build time |
| `UIU_LOCAL_BACKEND_URL` | Local Spring Boot URL (default `http://localhost:8080`) |
| `UIU_REMOTE_BACKEND_URL` | HTTPS Render URL (placeholder until deployed) |

Never commit real secrets. Never expose `CLERK_SECRET_KEY` or database credentials to Unity or the auth HTML page.

## Clerk Dashboard checklist

1. Use application `app_3IhCzPT7JDek4JtFgWOxbeKcs9k`.
2. Allow origin `http://localhost:8080` (and your Render HTTPS origin after deploy).
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

## Render deployment (Docker)

**Local development stays Maven** — use `mvn spring-boot:run` with `backend/.env` as above.
Docker is only for hosting on Render.

From `backend/` (optional local image check):

```bash
docker build -t uiu-simulator-backend .
```

Render Web Service settings: Root Directory `backend`, Runtime **Docker**,
Dockerfile Path `./Dockerfile`, Health Check Path `/actuator/health`.
Set server environment variables in the Render dashboard (not in the image).
