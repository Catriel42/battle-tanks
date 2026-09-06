# BattleTanks

Real-time multiplayer tank battle game. Players register an account, create or join game rooms, and fight in a 2D tile-based arena. Built on a client-server architecture using HTTP REST for authentication and room management, and SignalR for all real-time in-game communication.

## Documentation

Full project documentation is available in the [project wiki](https://gitlab.com/jala-university1/cohort-3/ES.CSPR-364.GA.T2.26.M1/SC/catriel.pereira/battle-tanks/-/wikis/Home).

The wiki covers:

- Architecture overview and C4 diagrams (Context, Containers, Components)
- Use case diagrams (Authentication, Lobby, Combat)
- Entity-Relationship diagram and domain model
- REST API, SignalR API, and WebSocket API reference
- Sequence diagrams for all key flows
- Frontend component hierarchy, state management, and game engine internals
- Infrastructure setup and Docker Compose services
- Architecture Decision Records (ADRs)

---

## Tech Stack

| Layer | Technology | Version |
| :--- | :--- | :--- |
| Backend Runtime | .NET / ASP.NET Core | 10.0 |
| Backend Language | C# | 13 |
| Frontend Framework | Angular | 22.0 |
| Frontend Language | TypeScript | 6.0 |
| Database | PostgreSQL | 18 |
| ORM | Entity Framework Core (Npgsql) | 10.0 |
| Real-Time | SignalR | Built-in ASP.NET Core 10 |
| State Management | NgRx Signals | 22.0 |
| Authentication | JWT Bearer (HMAC-SHA256) | - |
| Containerization | Docker Compose | - |

---

## Prerequisites

| Tool | Version |
| :--- | :--- |
| Docker | Latest |
| Docker Compose | v2+ |
| .NET SDK | 10.0 |
| Node.js | 20+ |
| Angular CLI | 22.x |

---

## Running the Project

### 1. Start the Infrastructure

Start PostgreSQL and pgAdmin using Docker Compose:

```bash
docker compose up -d
```

### 2. Run the Backend

You must apply database migrations manually before the first run.

```bash
cd BattleTanks-Backend
dotnet ef database update
dotnet run
```

The API will be available at `http://localhost:5000`.

### 3. Run the Frontend

```bash
cd BattleTanks-Frontend
npm install
ng serve
```

The application will be available at `http://localhost:4200`.

---

## Services and Ports

| Service | URL | Credentials |
| :--- | :--- | :--- |
| Frontend SPA | <http://localhost:4200> | - |
| Backend API | <http://localhost:5000> | - |
| SignalR Hub | <http://localhost:5000/gamehub> | - |
| PostgreSQL | localhost:5432 | user: `battletanks` / pass: `battletanks` |
| pgAdmin | <http://localhost:8081> | email: `admin@battletanks.com` / pass: `admin` |

---

## Useful Commands

| Command | Directory | Description |
| :--- | :--- | :--- |
| `docker compose up -d` | Root | Start all infrastructure services. |
| `docker compose down` | Root | Stop containers (data is preserved). |
| `docker compose down -v` | Root | Stop containers and delete all data. |
| `dotnet ef database update` | `BattleTanks-Backend/` | Apply pending EF Core migrations to the database. |
| `dotnet run` | `BattleTanks-Backend/` | Start the backend API. |
| `dotnet watch run` | `BattleTanks-Backend/` | Start backend with hot reload. |
| `ng serve` | `BattleTanks-Frontend/` | Start the Angular dev server. |
