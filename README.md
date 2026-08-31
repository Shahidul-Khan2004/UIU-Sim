# University Game

University Game (UIU Simulator) is an MVP 3D university simulation. The repository contains the Unity game client, a Spring Boot backend API, and documentation.

## Technology stack

- **Unity 6.3** with **C#** — 3D game client
- **Spring Boot 3.4** (Java 17+) — backend API
- **Clerk** — browser authentication (JWT)
- **Supabase PostgreSQL** — player persistence via Flyway

## Repository structure

```text
unity-client/   Unity 6 game project (UIU-Sim/)
backend/        Spring Boot API
docs/           Architecture, setup, API, database, and game design notes
```

## Development setup

1. Clone the repository.
2. Install Unity 6.3 and open `unity-client/UIU-Sim`.
3. Configure backend env vars from `backend/.env.example` (Clerk + Supabase).
4. Run the API: `cd backend && mvn spring-boot:run`
5. Follow `docs/setup-guide.md` for the full auth flow.

## Authentication flow (MVP)

```text
Bootstrap → (session?) → Login (Clerk browser) → Main campus
                 └──────── Authenticated ─────────┘
```

Unity opens Spring-served Clerk login → deep link returns JWT → local session (backend validation optional). Campus scenes are blocked until authenticated.

Unity **never** communicates with Supabase directly.

## Documentation

See the files in `docs/` for architecture, API, database, and setup details.
