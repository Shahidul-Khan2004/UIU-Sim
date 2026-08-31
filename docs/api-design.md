# API Design

Base URL (local): `http://localhost:8080`

All `/api/**` routes require:

```http
Authorization: Bearer <CLERK_SESSION_JWT>
```

## POST /api/auth/login

Validates the Clerk JWT, upserts the player profile, returns the player.

### Request

- Headers: `Authorization: Bearer <JWT>`
- Body: empty

### Response `200`

```json
{
  "success": true,
  "player": {
    "id": "11111111-1111-1111-1111-111111111111",
    "clerkUserId": "user_abc",
    "email": "player@uiu.edu",
    "username": "campus-explorer",
    "createdAt": "2026-01-01T00:00:00Z",
    "lastLogin": "2026-01-01T00:00:00Z"
  }
}
```

### Errors

| Status | When |
|--------|------|
| `401` | Missing/invalid/expired JWT |
| `500` | Unexpected / database failure |

Error body shape:

```json
{
  "success": false,
  "message": "Authentication failed",
  "timestamp": "2026-01-01T00:00:00Z",
  "path": "/api/auth/login"
}
```

## GET /api/players/me

Returns the authenticated player's profile (must already exist from login).

## GET /auth/login

Public HTML page embedding Clerk JS. After sign-in, redirects to:

`uiusim://auth/callback?token=<JWT>`

## GET /actuator/health

Public health probe.
