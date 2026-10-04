# UIU Simulator

UIU Simulator (UIU-Sim) is a 3D university life simulation set on the campus of United International University. Explore the campus as a student or faculty member, attend or teach classes, interact with campus services and other students, and build your reputation through everyday decisions.

The current playable beta includes admission, campus exploration, online saves, student and faculty progression, and a six-day first-semester cycle with classes, quizzes, a midterm, and final exams.

## Download and demo

- **[Download and play the game](https://drive.google.com/drive/folders/1DOByqxLLwwb3KS0DPft52OQWTj2Xtu-V?usp=sharing)** — available builds on Google Drive.
- **[Watch the gameplay demo](https://youtu.be/Np1KEdiroSk)** — YouTube walkthrough.

An internet connection is required for sign-in and saving progress online. To play an available build, use the download above; the development instructions below are for running the source project.

## Getting started in the game

1. Launch the game and select **Sign In**. Complete sign-in or sign-up in your browser, then return to the game.
2. Choose **Continue** to resume your saved journey or **New Game** to start again. Each account has one active save; starting a new game replaces it after confirmation.
3. For a new journey, visit the ground-floor receptionist to register your name, role, department, and university ID, and receive your ID card.
4. Use your ID at campus scanners, explore the building, and follow your class routine and daily objectives. The **Escape** menu provides your ID card, class routine, save controls, and day progression; students can also open their report card.

## Gameplay

### Campus life

- Explore the ground floor and Floors 1–10, with classrooms, the canteen, library, faculty offices, and an academic advisor.
- Travel using elevators, stair/floor transitions, and interactive doors; use the floor map to navigate.
- Register through the receptionist and use ID scanners and gates, including temporary-ID recovery when your card is forgotten or lost.
- Talk to batchmates and senior students through branching conversations, and make choices at the canteen breakfast queue.
- Switch between first-person and third-person camera views.

### Student journey

- Choose **CSE** or **BBA** during admission. CSE students take Introduction to Computer Science, English, and Discrete Mathematics; BBA students take Introduction to Business, Principles of Accounting, and English.
- Attend scheduled lectures, earn rewards for participation, or face the consequences of leaving early, skipping, or using proxy attendance.
- Build **Aura** and **Academic Reputation** through campus activities, conversations, class attendance, and study.
- Study in the library through timed self-study sessions and the **Rocket Study** computer minigame.
- Take timed quizzes and exams. Academic Reputation affects question difficulty and answer time; cheating carries a risk of penalties and course dropping.
- View saved assessment marks, course grades, grade points, and CGPA in the report card.
- Ask the Gemini-powered academic advisor for advice tailored to your Aura and Academic Reputation when the backend has advisor access configured.

### Faculty journey

- Receive a faculty ID, access your office, and use the faculty computer portal.
- View assigned courses, class routines, and enrolled student lists; prepare and share course material links.
- Teach scheduled classes and prepare questions for exam days, with progress and **Faculty Reputation** saved separately from student stats.
- Complete your assigned teaching activities, take a coffee break, and advance to the next day.

The current default faculty assignments are Introduction to Computer Science and Discrete Mathematics.

### Semester 1 schedule

| Day | Student schedule |
| --- | --- |
| 1 and 4 | Regular classes and campus activities |
| 2 | Quiz 1 |
| 3 | Midterm |
| 5 | Quiz 2 |
| 6 | Final exams and semester results |

The student beta concludes after the Day 6 finals with a semester-completion screen and report card. Progression into later semesters is still under development.

## Controls

| Input | Action |
| --- | --- |
| W / A / S / D | Move |
| Mouse | Look around |
| Left Shift | Sprint |
| Space | Jump |
| E | Interact with the object or person you are looking at |
| P | Toggle first-person / third-person camera |
| M | Expand or collapse the floor map |
| Escape | Open the game menu or close the current panel |
| Hold Space or left mouse button | Thrust in Rocket Study |

## Technology and architecture

| Area | Technology |
| --- | --- |
| Game client | Unity 6.3 (`6000.3.23f1`), C#, Universal Render Pipeline |
| Input and world tooling | Unity Input System, ProBuilder, AI Navigation |
| Backend | Java 17, Spring Boot 3.4.5, Spring Security, Spring Data JPA |
| Authentication | Clerk browser sign-in, JWT verification, and session refresh |
| Persistence | Supabase-hosted PostgreSQL, Flyway migrations |
| Academic advisor | Gemini through the Google GenAI Java SDK |
| Tests | Unity Test Framework; JUnit, Spring Boot tests, and H2 |

```text
Unity game client ── authenticated REST requests ──> Spring Boot API ──> PostgreSQL
Browser sign-in  ──> Clerk ── HTTP auth bridge ──> Unity session
Spring Boot API  ──> Gemini academic advisor
```

The backend validates authentication and gameplay actions and persists saves, stats, daily activities, assessments, and faculty progress. Unity communicates with the API; database credentials and provider secrets stay on the server.

The client starts through **Bootstrap → Login → SaveSelection → UIU_Main**. Main owns the player and shared gameplay systems, while campus floors load as separate additive scenes. Browser sign-in returns through an HTTP bridge polled by Unity, with token refresh managed by the client; custom deep links are an optional callback path.

## Repository structure

```text
unity-client/UIU-Sim/           Unity project to open in Unity Hub
  Assets/Scenes/Auth/           Bootstrap, login, and save selection
  Assets/Scenes/Main/           Persistent campus gameplay scene
  Assets/Scenes/Floors/         GroundFloor and Floor01–Floor10
  Assets/Scenes/Minigames/      Rocket Study scene
  Assets/Scripts/              Gameplay, UI, authentication, networking, and editor tools
  Assets/Tests/                Building EditMode and PlayMode tests
  Assets/Resources/            Public backend configuration
backend/
  src/main/java/com/uiusimulator/  Auth, player, assessment, advisor, and shared services
  src/main/resources/           App configuration, question banks, and database migrations
  src/test/                     Backend tests
  Dockerfile                    Backend container build
docs/                           Supporting architecture, design, and setup notes
```

## Run from source

### Prerequisites

- Unity Hub and Unity **6000.3.23f1**, plus build support for your target platform.
- JDK **17** and Maven.
- Git LFS for the repository's large Unity assets. After cloning, run `git lfs install` and `git lfs pull` from the repository root.
- A PostgreSQL database and a Clerk application configured for your backend's browser sign-in origin.
- A Gemini API key if you want the academic advisor enabled.

### Backend

From the repository root:

```bash
cd backend
cp .env.example .env
```

Edit `backend/.env` before starting the API:

| Variables | Configuration |
| --- | --- |
| `DATABASE_URL`, `DATABASE_USERNAME`, `DATABASE_PASSWORD` | Your PostgreSQL JDBC URL and credentials; replace the example connection values |
| `CLERK_PUBLISHABLE_KEY`, `CLERK_SECRET_KEY` | Keys for your Clerk application; the secret key is used for server-side session minting and refresh |
| `CLERK_ISSUER`, `CLERK_JWKS_URL` | Issuer and JWKS endpoint for the same Clerk instance as your keys |
| `CLERK_AUTHORIZED_PARTIES` | Allowed sign-in origins; include `http://localhost:8080` for the default local setup |
| `GEMINI_API_KEY` | Optional; enables the academic advisor |
| `UIU_BACKEND_MODE` | Set to `LOCAL` for local Unity development |
| `UIU_LOCAL_BACKEND_URL` | `http://localhost:8080` by default |
| `UIU_REMOTE_BACKEND_URL` | Your hosted HTTPS API URL when using `REMOTE` mode |

Run from `backend/` using JDK 17:

```bash
mvn spring-boot:run
```

The application loads `.env` automatically, and Flyway applies the database migrations on startup. The default local port is 8080; `PORT` takes precedence over `SERVER_PORT`. Check [the local health endpoint](http://localhost:8080/actuator/health) and [browser sign-in page](http://localhost:8080/auth/login) once the API is running.

### Unity client

1. Open **`unity-client/UIU-Sim`** in Unity Hub with the project editor version.
2. Select **UIU Simulator → Sync Backend Config From .env** after configuring `backend/.env`. For a local API, confirm `UIU_BACKEND_MODE=LOCAL`.
3. Open `Assets/Scenes/Auth/Bootstrap.unity` and enter Play Mode.
4. Select **Sign In**, complete Clerk authentication in the browser, and continue through save selection into the campus.

The editor copies only the public `UIU_*` connection settings into `Assets/Resources/BackendConfig.asset`, and syncs them again before a player build. Standalone players use these baked settings. The committed asset selects the remote backend, so sync your local configuration before testing against localhost.

### Builds and hosting

Build the client from the Unity project's Build Profiles with **Bootstrap** first, followed by Login, SaveSelection, Main, the floor scenes, and the minigame scene. To connect a distributed build to a hosted API, set `UIU_BACKEND_MODE=REMOTE` and configure a valid `UIU_REMOTE_BACKEND_URL` using HTTPS before building.

The backend includes a multi-stage [Dockerfile](backend/Dockerfile) for Render-style hosting. Use `backend` as the service root, `./Dockerfile` as the Dockerfile path, and `/actuator/health` as the health check. Configure server environment variables in the host's settings; Render supplies `PORT` automatically. Run the backend tests separately before packaging, as the Docker build skips them.

## Tests and developer tools

Run backend tests with JDK 17:

```bash
cd backend
mvn test
```

For Unity, open **Window → General → Test Runner** and run the EditMode and PlayMode suites. Tests cover gameplay flows, admission and ID cards, NPC conversations, traversal, library study, assessments, faculty systems, UI, authentication parsing, and building scenes. Gameplay tests live under `Assets/Scripts/Gameplay/Editor/Tests`, with additional authentication and building test folders.

The **Tools → UIU Simulator → Building Generator** editor tool creates floor geometry from layout assets under `Assets/Data/Floors/`. Additional editor tools support campus props, NPC setup, auth scenes, and floor authoring.

Supporting notes are in [docs/](docs/), including the [building workflow](docs/unity-building-workflow.md) and [architecture notes](docs/architecture.md). Some documents describe earlier prototype milestones; this README reflects the current implementation, and the scene settings, configuration, and source code provide the detailed contracts.
