# BudgetFlow API

Budget and activity management for a youth movement branch. The coordinator manages the guides; every
guide manages her own group of participants and reports the meetings she runs (attendance + expenses).
Every expense is charged to a budget pot calculated from fixed rates, and the server refuses a meeting
that goes over its pot. Guides also share one equipment store, and its single projector is the
contended resource the concurrency design protects.

ASP.NET Core Web API (.NET 10) · PostgreSQL 16 · EF Core · JWT · NLog · AutoMapper.

## Architecture

| Project | Responsibility |
|---------|----------------|
| `BudgetFlow.Core` | Entities, enums, DTOs, repository/service interfaces, `Result`, settings. No dependencies. |
| `BudgetFlow.Data` | `AppDbContext`, Fluent configurations, repositories, unit of work, migrations, dev seed. |
| `BudgetFlow.Service` | Business rules, budget calculation, authorization of "is it hers?", AutoMapper profile. |
| `BudgetFlow.API` | Controllers, middleware (correlation id, exception handling), JWT, DI, Swagger, NLog. |
| `BudgetFlow.Tests` | xUnit + Moq + FluentAssertions. |

Dependency direction: **Core → nothing · Data → Core · Service → Core · API → Core + Service + Data**
(Data is referenced by the API for DI registration only). The Service layer never depends on EF Core.

Services return a `Result` (`Ok`, `Validation`, `Forbidden`, `NotFound`, `Conflict`) instead of
throwing for expected outcomes; `ApiControllerBase` maps it to HTTP, so services never know about HTTP.

## Budget rules

Budgets are always computed, never stored. Rates come from the `Budget` section of `appsettings.json`.

| Pot | Owner | Formula (default rates) | Paid by meeting type |
|-----|-------|-------------------------|----------------------|
| Activity | Guide | flat 300 | `Activity` |
| Birthday | Guide | 4 × her participants | `Birthday` |
| Guide meetings | Coordinator | 20 × guides × 4 meetings | `GuidesMeeting` |
| Annual | Coordinator | 35 × participants + 2 × 5 × participants | `SpecialEvent` |

The coordinator can add money to a guide's pot (`Extra`) or move it from one of her own pots
(`Transfer`); guide pots include the additions, coordinator pots subtract the transfers.

**Deliberate change from the plan: split charging.** The plan charged a meeting to one pot only. At the
coordinator's request, an `Activity` meeting over the activity pot can be completed from the birthday pot:
the server first answers `409` with `shortfall` and `birthdayAvailable`; if the guide resends with
`allowBirthdayDraw: true`, the rest of the activity pot is charged to Activity and only the shortfall to
Birthday. No pot goes below zero; if both together are not enough, the meeting is refused.

## Concurrency: the single projector

The branch owns one projector (`Projector 1` in the demo data), and several guides may try to take it
at the same moment. Exactly one of them must succeed.

- `Equipment.Version` is mapped to PostgreSQL's system column `xmin` as the EF Core concurrency token,
  so no extra column is needed.
- `PUT /api/equipment/{id}/holder` loads the item tracked, checks it is `Available`, sets it `Taken`
  and saves, in one transaction. EF Core issues
  `UPDATE "Equipment" SET … WHERE "Id" = @id AND xmin = @originalXmin`.
- If another guide committed first, `xmin` has changed, zero rows are updated and EF Core throws
  `DbUpdateConcurrencyException`. The Data layer translates it to `ConcurrencyConflictException`,
  and the API returns **409 Conflict** ("Another guide just took this item. Please try again.").
- The same token protects returning, editing and deleting an item. Every conflict is logged at `Warning`.

Result: one `204`, one `409`, one holder. To watch it happen, run the API with the demo data
(`SeedDemoData` on, see below) and then:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\race-demo.ps1
```

## Running locally

**Requirements:** .NET 10 SDK, Docker Desktop, `dotnet-ef` (`dotnet tool install --global dotnet-ef`).

1. **PostgreSQL.** Copy `.env.example` to `.env`, choose a password, then:

   ```powershell
   docker compose up -d
   ```

   Starts PostgreSQL 16 (`budgetflow-db`, port 5432, database `BudgetFlowDb`).

2. **Secrets** (User Secrets, never in `appsettings*.json`), with the same password as in `.env`:

   ```powershell
   dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=BudgetFlowDb;Username=postgres;Password=<your password>" --project BudgetFlow.API
   dotnet user-secrets set "Jwt:Key" "<a random string of at least 32 characters>" --project BudgetFlow.API
   ```

   The server refuses to start if `Jwt:Key` is missing or shorter than 32 bytes. Outside Development,
   supply the same keys as environment variables (`ConnectionStrings__Default`, `Jwt__Key`).

3. **Run** with the `http` (or `https`) profile, so the environment is `Development`:

   ```powershell
   dotnet run --project BudgetFlow.API --launch-profile http
   ```

   Pending migrations are applied on startup, so the tables are created automatically
   (`dotnet ef database update --project BudgetFlow.Data --startup-project BudgetFlow.API` does the same by hand).

4. **Swagger:** <http://localhost:5178/swagger>. Log in with `POST /api/auth/login`, click
   **Authorize** and paste the `token`.

The database starts empty: open the client (or `POST /api/auth/register`) to register the coordinator,
who then adds the guides. For a ready-made demo, set `"SeedDemoData": true` in
`appsettings.Development.json`: an empty database is then filled with three users, ten participants,
three equipment items and sample meetings (a database that already has users is never seeded).

## Deploying to Render

The `Dockerfile` builds and runs `BudgetFlow.API`. On Render: create a **PostgreSQL** database, then a
**Web Service** (runtime Docker, branch `main`) with these environment variables:

| Variable | Value |
|----------|-------|
| `ConnectionStrings__Default` | The database's **Internal Database URL** (`postgresql://…` is accepted as is) |
| `Jwt__Key` | A random secret of at least 32 characters (Render's **Generate** button) |
| `SeedDemoData` | `true` to create the demo users on the first start (optional) |
| `Cors__AllowedOrigins` | The client's address, e.g. `https://….onrender.com` (comma-separated, no path) |

The service listens on Render's `PORT`, applies the migrations on startup and serves Swagger at `/swagger`.

## Authentication & roles

Every endpoint requires a valid JWT (`Authorization: Bearer <token>`) except `/api/auth/*`. The token
carries the user id, email and role, and the API validates issuer, audience, signing key and lifetime
(120 minutes). Role checks use `[Authorize(Roles = …)]`; ownership checks ("only her own") are in the
Service layer and return `403`.

| Role | Can |
|------|-----|
| Coordinator | Manage guides and equipment, see every guide's budget and meetings, add or cancel budget additions, report `GuidesMeeting` and `SpecialEvent` meetings. |
| Guide | Manage her own participants, report `Activity` and `Birthday` meetings, see her own budget, take and return equipment. |

There is exactly one coordinator: `POST /api/auth/register` creates her once and returns `409`
afterwards. Guides are created by the coordinator.

### Demo users (only when `SeedDemoData` is `true`)

| Role | Email | Password |
|------|-------|----------|
| Coordinator | `sarah@budgetflow.local` | `Coord!2026` |
| Guide | `rachel@budgetflow.local` | `Guide!2026` |
| Guide | `shira@budgetflow.local` | `Guide!2026` (second guide in the projector race) |

These accounts exist only in the local demo database; they are not real passwords.

## API

| Method | Route | Access |
|--------|-------|--------|
| POST | `/api/auth/register` | public, once (the coordinator) |
| POST | `/api/auth/login` | public |
| GET | `/api/users?sort=&page=&pageSize=` | Coordinator |
| POST / PUT / DELETE | `/api/users`, `/api/users/{id}` | Coordinator |
| GET | `/api/users/{id}` | Coordinator, or the guide herself |
| GET | `/api/users/{id}/overview`, `/api/users/me/overview` | Coordinator, or the guide herself |
| GET / POST | `/api/users/{guideId}/participants` | Coordinator, or the guide herself |
| GET / PUT / DELETE | `/api/participants/{id}` | Coordinator, or the participant's guide |
| GET | `/api/meetings?userId=&type=&from=&to=&sort=&page=&pageSize=` | Coordinator (any user); a guide gets her own |
| GET | `/api/meetings/{id}` | Coordinator, or the reporter |
| POST | `/api/meetings` | any logged-in user (type must match role) |
| PUT / DELETE | `/api/meetings/{id}` | the reporter only |
| GET | `/api/users/{userId}/budget` | Coordinator, or the guide herself |
| GET | `/api/budget/me` | any logged-in user |
| GET | `/api/budget/summary` | Coordinator |
| GET | `/api/budget-additions?guideId=` | Coordinator |
| POST | `/api/users/{guideId}/budget-additions` | Coordinator |
| DELETE | `/api/budget-additions/{id}` | Coordinator (`409` if already spent) |
| GET | `/api/equipment?status=`, `/api/equipment/{id}` | any logged-in user |
| POST / PUT / DELETE | `/api/equipment`, `/api/equipment/{id}` | Coordinator (`409` while on loan) |
| PUT | `/api/equipment/{id}/holder` | any logged-in user (take) |
| DELETE | `/api/equipment/{id}/holder` | the holder or the Coordinator (return) |

## Errors

Expected errors are returned as **ProblemDetails** (RFC 9457), with extra fields when relevant
(e.g. `shortfall`, `birthdayAvailable`):

```json
{ "status": 409, "title": "Conflict", "detail": "…", "instance": "/api/meetings" }
```

| Status | When |
|--------|------|
| 400 | Validation failed, or the request body could not be read |
| 401 | Missing or invalid token, wrong email or password |
| 403 | Wrong role, or not her own resource |
| 404 | Resource not found |
| 409 | Business conflict (over budget, item on loan, coordinator exists) or concurrency conflict |
| 500 | Unexpected error |

Unexpected errors are caught by `ExceptionHandlingMiddleware` and returned as
`{ "traceId", "status", "message" }`. Stack traces, SQL and EF Core details are never returned.

## Logging

`ILogger<T>` everywhere, with NLog underneath (`BudgetFlow.API/nlog.config`): console and
`BudgetFlow.API/logs/yyyy-MM-dd.log`. `CorrelationIdMiddleware` gives every request a new id, adds it to
every log line and returns it in the `X-Correlation-ID` header (and as `traceId` in a 500 answer).

Levels: `Information` (requests, logins, main actions), `Warning` (equipment conflicts, refused meetings),
`Error` (unhandled exceptions). Passwords, hashes, tokens, request bodies and headers are never logged.

## Tests

```powershell
dotnet test BudgetFlow.sln
```

`BudgetFlow.Tests` uses xUnit, Moq and FluentAssertions (pinned to 7.x, the last version under the
Apache 2.0 licence).

- **Service unit tests** (repositories, unit of work and mapper mocked with Moq; no database):
  taking an available item / a taken item (`409`), returning by a guide who is not the holder (`403`),
  guide and coordinator budget formulas, and an over-budget activity meeting without the birthday draw
  (`409` with `shortfall` and `birthdayAvailable`) and with it (split: the rest to Activity, the
  shortfall to Birthday).
- **Concurrency proof** (`EquipmentConcurrencyTests`): two independent `AppDbContext` instances read the
  same `Available` projector and both mark it `Taken`. The first `SaveChanges` succeeds and the second
  throws `DbUpdateConcurrencyException`; the database keeps the first take only. It runs against real
  PostgreSQL (InMemory and SQLite have no `xmin`), in a `BudgetFlowDb_Test` database that is created,
  migrated and dropped by the test fixture. The connection is read from the `BUDGETFLOW_TEST_CONNECTION`
  environment variable or, if it is not set, from the API's User Secrets (`ConnectionStrings:Default`),
  with the database name replaced. The Docker container must be running.

## Deliberate limitations

- **One coordinator per installation.** Registration is open only until she exists; guides never self-register.
- **No refresh tokens.** A token lives 120 minutes; the user logs in again afterwards.
- **A meeting's type cannot change** after it is reported, since the type decides the pot. Delete and report again.
- **The correlation id is always generated by the server**, never taken from the client, so no one can inject text into the logs.
- **Budget amounts are not stored**; they are recalculated on every read, so they can never go out of sync.
