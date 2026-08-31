# Setup Guide

## Prerequisites

- Unity 6.3 LTS (project under `unity-client/UIU-Sim`)
- JDK 17+ and Maven 3.8+
- Clerk application `app_3IhCzPT7JDek4JtFgWOxbeKcs9k`
- Supabase Postgres (shared pooler)

## Backend

1. `cd backend`
2. Copy `.env.example` → `.env` and fill:

   - `DATABASE_URL` / `DATABASE_USERNAME` / `DATABASE_PASSWORD`
   - `CLERK_PUBLISHABLE_KEY`
   - `CLERK_ISSUER` (Frontend API URL)
   - `CLERK_JWKS_URL` (`<issuer>/.well-known/jwks.json`)
   - `CLERK_AUTHORIZED_PARTIES=http://localhost:8080`

3. Export env vars and run:

```bash
set -a && source .env && set +a
mvn spring-boot:run
```

4. Flyway applies `V1__create_players_table.sql` on startup.
5. Visit `http://localhost:8080/auth/login` and create a test user.

### Clerk Dashboard

- Allowed origins: include `http://localhost:8080`
- Optional: add `email` / `username` claims to the session token template

## Unity

1. Open `unity-client/UIU-Sim` in Unity 6.3.
2. Confirm Build Settings start with `Assets/Scenes/Auth/Bootstrap.unity`.
3. Press Play — unauthenticated users land on Login; authenticated users go to Main.
4. Click **Sign In** — Unity opens `http://localhost:8080/auth/login?session=...` and **polls** for the JWT.
5. Finish Clerk in the browser. The page posts the token to the HTTP bridge; Unity continues automatically.
6. Fallback: use **Copy token** on the web page + Editor **Apply JWT**.

### Deep links (why Ubuntu says “No Apps Available”)

| Item | Value |
|------|--------|
| Scheme in Unity Project Settings | `uiusim` (`iOSURLSchemes` / `macOSURLSchemes` only) |
| Auth page deep link | `uiusim://auth/callback?token=...` |
| Linux handler registered? | **No** — neither Unity Editor nor a player build is an `x-scheme-handler` |
| `Application.deepLinkActivated` | Implemented in `AuthCallbackHandler`, but never fires without an OS handler |

**Do not rely on custom URI schemes for Linux Editor development.** Use the HTTP bridge above.

Active Input Handling must remain **Input System Package (New)**. Login UI uses `InputSystemUIInputModule`.

## Security reminders

- Do not put database credentials or `CLERK_SECRET_KEY` in Unity.
- Do not log JWT tokens.
- `.env` is gitignored — never commit secrets.
