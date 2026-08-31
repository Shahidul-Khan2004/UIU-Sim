# Authentication (Unity)

## Scene flow

1. `Scenes/Auth/Bootstrap.unity` — checks session, routes to Login or Main  
2. `Scenes/Auth/Login.unity` — player Sign In (Clerk browser)  
3. `Scenes/Main/UIU_Main.unity` — campus gameplay (guarded)

## Dev auth return path (Linux / Editor)

Unity opens `/auth/login?session=<id>` → after Clerk sign-in the page `POST`s the JWT to `/auth/dev/bridge/<id>` → Unity polls until ready.

Custom schemes (`uiusim://`, `uiu-simulator://`) are **not** used for day-to-day Linux Editor work — the OS has no app registered for them.

## Core scripts

| Script | Role |
|--------|------|
| `ClerkAuthManager` | Open browser login / logout / bridge poll |
| `AuthCallbackHandler` | Deep-link / Editor JWT callback (optional) |
| `UserSession` | Token + profile + state |
| `AuthenticationState` | LoggedOut / Authenticating / Authenticated / Failed |
| `AuthHost` | Persistent auth objects across scenes |
| `AuthBootstrapController` | Bootstrap routing |
| `LoginSceneController` | Player login UI |
| `AuthGameplayGuard` | Blocks Main without auth |

## Testing

1. Start backend (`mvn spring-boot:run` with `.env`).
2. Play from Bootstrap → Login → **Sign In**.
3. Complete Clerk in the browser; leave Unity running.
4. Unity should authenticate without any deep-link dialog.
