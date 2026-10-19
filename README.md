# YouthGroups API (BudgetFlow server)

## What the system does

YouthGroups is the server of a budget and activity management system for a youth movement branch.
The branch coordinator manages the guides, and every guide manages her own group of participants.
Guides report the meetings they run (activities and birthday parties) with the attendance and the
expenses. The coordinator reports guides meetings and special events. Every expense is charged to a
budget pot that is calculated from fixed rates in `appsettings.json`, and the server refuses a meeting
that goes over its pot. The coordinator can see the budget of every guide, add money to a guide's pot,
and manage the shared equipment store, from which guides take and return items.

The server is an ASP.NET Core Web API (.NET 10) in four layers:
`YouthGroups.API` (controllers, middleware) → `YouthGroups.Service` (business rules) →
`YouthGroups.Data` (EF Core, PostgreSQL) → `YouthGroups.Core` (entities, DTOs, interfaces).
Authentication uses JWT, and logging uses NLog.

## The limited resource: the projector

The branch has only ONE projector (`Projector 1` in the demo data), and every guide may want it for
the same evening. A guide takes an item with `PUT /api/equipment/{id}/holder` and returns it with
`DELETE /api/equipment/{id}/holder`. If two guides press "take" at the same moment, exactly one of them
must get it.

**How concurrency is handled (optimistic concurrency):**

- Every PostgreSQL row has a hidden system column, `xmin`, that changes on every write to the row.
  `Equipment.Version` is mapped to `xmin` as a concurrency token, so no extra column is needed.
- The take request loads the item (tracked), checks that it is `Available`, and saves it as `Taken`.
  The check and the save run in one transaction.
- EF Core adds the `xmin` value it read to the update:
  `UPDATE "Equipment" SET ... WHERE "Id" = 1 AND xmin = 847`.
  If the other guide saved first, `xmin` has changed, no row is updated, and EF Core throws
  `DbUpdateConcurrencyException`.
- The Data layer turns it into `ConcurrencyConflictException`, and the service returns
  **409 Conflict** ("Another guide just took this item. Please try again.") instead of a second
  successful take. The same token protects returning, editing and deleting an item.
- Every conflict is written to the log at `Warning` level.

To see it happen, run the API and then, from this folder:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\race-demo.ps1
```

Two guides take the projector at the same moment in each round. One gets `204` and the other gets `409`.

**A deliberate change from the plan: splitting a meeting between two budget pots.**
The plan said that a meeting is charged in full to one pot, with no split. At the coordinator's request,
an activity meeting that is over the activity pot can be completed from the birthday pot:

- First the server answers `409` with the missing amount (`shortfall`) and the amount left in the
  birthday pot (`birthdayAvailable`).
- If the guide confirms (`allowBirthdayDraw: true`), what is left in the activity pot is charged to
  Activity, and only the missing part is charged to Birthday.

This way the activity pot is used up first and no pot goes below zero. If even both pots together are
not enough, the meeting is refused. Every refused meeting is written to the log at `Warning` level.

## Running locally

**Requirements:** .NET 10 SDK, Docker Desktop, and the EF Core tools
(`dotnet tool install --global dotnet-ef`).

1. **Start PostgreSQL in Docker.** Copy `.env.example` to `.env` and choose a database password, then run:

   ```powershell
   docker compose up -d
   ```

   This starts PostgreSQL 16 in a container named `youthgroups-db` on port 5432, with a database named `YouthGroupsDb`.

2. **Set the secrets (User Secrets, never in `appsettings.json`).** Use the same password you chose in `.env`:

   ```powershell
   dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=YouthGroupsDb;Username=postgres;Password=<your password>" --project YouthGroups.API
   dotnet user-secrets set "Jwt:Key" "<a random string of at least 32 characters>" --project YouthGroups.API
   ```

   The server refuses to start if `Jwt:Key` is missing or shorter than 32 bytes.

3. **Create the tables:**

   ```powershell
   dotnet ef database update --project YouthGroups.Data --startup-project YouthGroups.API
   ```

4. **Run the API** with the `http` (or `https`) launch profile, so the environment is `Development` and
   User Secrets are loaded:

   ```powershell
   dotnet run --project YouthGroups.API --launch-profile http
   ```

   In Visual Studio, choose the `http` profile and press F5.

5. Open **Swagger** at <http://localhost:5178/swagger>. Log in with `POST /api/auth/login`, copy the
   `token`, click **Authorize** and paste it.

On the first run in Development (`"SeedDemoData": true` in `appsettings.Development.json`), the server
fills the empty database with demo data: three users, ten participants, three equipment items and
sample meetings. It never seeds a database that already has users.

Logs are written to `YouthGroups.API/logs/yyyy-MM-dd.log` and to the console. Every line carries the
request's `CorrelationId`, which the client also receives in the `X-Correlation-ID` response header.

## Demo users

| Role        | Email                      | Password     | What she can do |
|-------------|----------------------------|--------------|-----------------|
| Coordinator | `sarah@youthgroups.local`  | `Coord!2026` | Manage guides and equipment, see every budget, add budget to guides, report guides meetings and special events |
| Guide       | `rachel@youthgroups.local` | `Guide!2026` | Manage her participants, report activities and birthdays, take and return equipment |
| Guide       | `shira@youthgroups.local`  | `Guide!2026` | Same as Rachel, with her own group (used as the second guide in the projector race) |

These accounts exist only in the local demo database. They are not real passwords.
