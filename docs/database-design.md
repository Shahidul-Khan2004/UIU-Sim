# Database Design

Persistence uses **Supabase PostgreSQL**. Schema changes go through **Flyway** only (`ddl-auto: validate`).

## players

Migration: `backend/src/main/resources/db/migration/V1__create_players_table.sql`

| Column | Type | Notes |
|--------|------|-------|
| `id` | `UUID` | Primary key |
| `clerk_user_id` | `TEXT` | Unique Clerk user id (`JWT.sub`) |
| `email` | `TEXT` | Optional; from JWT claims when present |
| `username` | `TEXT` | Optional display name |
| `created_at` | `TIMESTAMPTZ` | Set on insert |
| `last_login` | `TIMESTAMPTZ` | Updated on each successful `/api/auth/login` |

Clerk users map 1:1 to players via `clerk_user_id`.
