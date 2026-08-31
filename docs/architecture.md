# Architecture

UIU Simulator is a Unity 6 campus exploration client backed by a Spring Boot API. Clerk provides browser authentication. Supabase PostgreSQL stores player data. **Unity never talks to Supabase directly.**

## System diagram

```text
Unity Client
    |
    | opens browser
    v
Spring Boot /auth/login  (Clerk JS)
    |
    | uiusim://auth/callback?token=JWT
    v
Unity stores JWT
    |
    | Authorization: Bearer <JWT>
    v
Spring Boot API  (validate JWT via Clerk JWKS)
    |
    v
Supabase PostgreSQL
```

## Auth sequence

1. Player clicks **Login** in Unity.
2. Unity opens `http://localhost:8080/auth/login`.
3. Player signs in / signs up with Clerk in the browser.
4. Auth page obtains a Clerk session JWT.
5. **Dev (Linux/Editor):** page posts JWT to `/auth/dev/bridge/{session}`; Unity polls and continues.
6. **Optional deep link:** `uiusim://auth/callback?token=...` (not registered on Linux; shows “No Apps Available”).
7. Unity stores JWT and enters Main.

## Component boundaries

| Component | Owns |
|-----------|------|
| Unity Authentication scripts | Browser open, callback, session state |
| Unity ApiClient | HTTP to Spring Boot only |
| Spring Boot auth | JWT verification, login orchestration |
| Spring Boot player | Persistence / profile rules |
| Supabase | Database only (via Spring) |

## Backend packages

`com.uiusimulator.config | auth.* | player.* | common.*`
