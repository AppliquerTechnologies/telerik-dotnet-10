# Orders

A small orders and customers app built with ASP.NET Core 10 Razor Pages, Telerik UI for ASP.NET Core, EF Core and SQL Server. All data is generated sample data.

- 150,000 orders and 200 customers, seeded on first start
- Telerik Grid with server-side paging, sorting and filtering, in two versions: offset paging (`/Orders`) and cursor paging (`/Orders/Cursor`)
- Popup editing with a filterable customer dropdown, a date picker, DataAnnotations and business-rule errors shown on the field
- Details window (Summary / Notes / Attachments tabs), file upload, a small REST API, a status summary, session state
- Cookie login with two roles: Admin can add, edit and delete; User is read-only
- Optimistic concurrency with a SQL Server `rowversion`, EF Core migrations applied on startup, structured JSON logging with Serilog
- A loader covers every server call (grid reads, saves, deletes, uploads, sign in and out); it only appears if a call takes longer than 150 ms and blocks clicks meanwhile, so nothing is submitted twice

## Setup

You need the .NET 10 SDK, SQL Server (LocalDB is fine) and internet access, because the Kendo scripts, theme and Inter font load from CDNs.

1. **Telerik NuGet feed.** `NuGet.config` lists the feed but not the credentials. Add them to your user-level config once:

   ```bash
   dotnet nuget add source https://nuget.telerik.com/v3/index.json -n TelerikOnlineFeed -u api-key -p <YOUR_API_KEY> --store-password-in-clear-text --configfile "%APPDATA%\NuGet\NuGet.Config"
   ```

   The source name has to stay `TelerikOnlineFeed`.
2. **Telerik licence.** Put `telerik-license.txt` in `%APPDATA%\Telerik\` (or set `TELERIK_LICENSE`) before building. Without it the UI shows a trial banner. Don't commit the key.
3. **Database.** The default connection string in `appsettings.json` points at `(localdb)\MSSQLLocalDB`, database `TelerikDemoOrders`. The app creates the database and applies the migrations when it starts (see [Migrations](#migrations)).
4. **Run.**

   ```bash
   cd TelerikDemo2
   dotnet run
   ```

   The first start applies the migrations and seeds the database in batches of 5,000 (about 10 seconds on my machine). To reseed, drop the database.

Sample accounts:

| User  | Password    | Role                           |
|-------|-------------|--------------------------------|
| admin | `Admin123!` | Admin: view, add, edit, delete |
| user  | `User123!`  | User: read-only                |

## Migrations

The schema is managed with EF Core migrations (in `TelerikDemo2/Migrations`). The app applies any pending migration when it starts, waiting and retrying for about a minute if SQL Server isn't reachable yet, and then seeds the database if it is empty. Set `Database__MigrateOnStartup=false` (or `Database:MigrateOnStartup` in `appsettings.json`) to turn that off and apply migrations yourself.

You need the EF tool once: `dotnet tool install --global dotnet-ef`. Run the commands from the solution folder (or drop `--project TelerikDemo2` inside the project folder):

```bash
dotnet ef migrations add <Name> --project TelerikDemo2       # after changing the model
dotnet ef database update --project TelerikDemo2             # apply pending migrations by hand
dotnet ef migrations list --project TelerikDemo2             # show applied / pending
dotnet ef migrations remove --project TelerikDemo2           # undo the last migration (if not applied)
dotnet ef migrations script --idempotent --project TelerikDemo2 -o migrate.sql   # SQL script for a deployment
dotnet ef database drop --project TelerikDemo2               # start over (development only)
```

`dotnet ef` reads the connection string from `appsettings.json` or the `ConnectionStrings__DefaultConnection` environment variable, and stops with an error if neither is set. A unit test fails if the model changes without a matching migration.

If you have a database from before migrations existed (created with `EnsureCreated`), drop it and let the app recreate it; the first migration would otherwise fail with "There is already an object named 'Customers'".

## Logging

Logs go through Serilog as one JSON object per line (compact format with the rendered message), to the console and to `logs/orders-<date>.json` (daily files, 14 kept). Every request produces one line with method, path, status, elapsed time, user name and client IP; the `/healthz` probe is left out. Startup failures are logged as JSON too. Request bodies, form values and passwords are not logged.

Levels and sinks are in the `Serilog` section of `appsettings.json`, so they can be changed with environment variables, for example `Serilog__MinimumLevel__Default=Debug`. In Development the EF Core SQL commands are logged at Information as well, which is noisy while the database is seeded.

## Docker

```bash
cp .env.example .env     # set MSSQL_SA_PASSWORD, TELERIK_NUGET_KEY, TELERIK_LICENSE
docker compose up --build
```

Then open <http://localhost:8080>. Compose starts SQL Server and the app; the app waits for the database and seeds it on first start.

The Telerik feed key and licence are passed to the build as BuildKit secrets, so they don't end up in the repo, the image layers or the final image. Leave `TELERIK_LICENSE` empty to build in trial mode. Data, uploads, log files and the auth keys live in named volumes (`docker compose down -v` removes them). Settings can be overridden with environment variables, for example `ConnectionStrings__DefaultConnection` and `UseHttpsRedirection` (off in the image, TLS is expected to end at a proxy). `GET /healthz` returns 200 when the database is reachable.

Avoid `;` and `=` in the SA password, since it is placed in a connection string.

## Project layout

Code is grouped by feature, and inside a feature by use case.

```marmaid
Domain/            entities and OrderRules
Common/            request dispatcher, Result, KeysetPager
Infrastructure/    DbContext, startup migration and seeding, attachment storage
Migrations/        EF Core migrations
Features/
  Orders/          pages, one file per use case (ListOrders, GetOrdersPage, CreateOrder, ...), view models,
                   editor templates, REST endpoints
  Customers/       customer lookup
  Auth/            login, logout, roles, permissions
  Shared/          layouts
```

Razor Pages are served from `Features/` instead of `Pages/`. PageModels stay thin: they bind input, check the permission, send one request through `IDispatcher` and shape the response for Telerik. The dispatcher is a small in-house version of the mediator pattern, and handlers are registered by scanning the assembly.

Order edits and deletes are checked against a `rowversion` column. The version travels to the browser with each row and back with every update or delete, and EF adds it to the `WHERE` clause. If someone else changed or deleted the order in the meantime, the save is refused with a clear message and the list refreshes. This also makes the Shipped check atomic with the update.

Business rules (no Shipped order on create, Shipped orders can't be changed or deleted, total between 10 and 5,000, no future dates, the customer must exist) are in `Domain/OrderRules.cs` and nowhere else. The same class holds the limits that the view model attribute, the editor templates, the EF mapping and the seeder use. Who can edit is defined once in `Features/Auth/Permissions.cs`; the write handlers enforce it and the views use it to hide buttons. Upload limits come from `Attachments` in `appsettings.json` and are also passed to the Upload widget.

Grid reads use `AsNoTracking`, project to a view model in the query and go through `ToDataSourceResultAsync`, so paging, sorting and filtering run in SQL. There are indexes on `OrderDate`, `Status` and `Total`, and on `Customers.Name`. Anti-forgery tokens travel with every Telerik call through `.Data("forgeryToken")`.

## Offset and cursor paging

| | `/Orders` | `/Orders/Cursor` |
| --- | --- | --- |
| Navigation | numbered pages, jump anywhere | previous / next |
| SQL | `OFFSET` plus `COUNT(*)` | `WHERE (sort column, id) > last row seen`, no count |
| Deep pages | slower the deeper you go | same cost at any depth when the sort column is indexed |
| While data changes | rows can shift or repeat | stable |

Both pages share a base PageModel, the grid definition, the filter bar and `wwwroot/js/orders.js`. They only differ in the read handler and the paging bar. The cursor logic is in `Common/KeysetPager.cs`: one sort column plus the id as tie-break, an opaque cursor, and the browser keeps a stack of cursors so Previous works. Changing the sort or filter goes back to page 1.

Timing when paging through all 150,000 rows in pages of 100 (LocalDB): 4 to 12 ms a page when sorting by `OrderId`, `OrderDate`, `Status` or `Total` (the last one measured across the full walk), which all have indexes. Sorting by customer name takes roughly 175 ms a page. There is an index on `Customers.Name`, but the list sorts orders by the joined customer name, so SQL still has to join and sort all orders for each page. Making that sort cheap would need the name stored on the order (or an indexed view).

## Tests

`dotnet test` runs 27 unit tests for `OrderRules`, `KeysetPager` and the migration check (no database needed). The browser behaviour was checked by hand and with throwaway Playwright scripts that are not part of the repo.

## What has not been verified

- Only Chrome was tested (1440 px and 390 px wide). Firefox, Safari, Edge and real touch devices were not.
- The grid header filter menus render, but I didn't click through them. The server side of filtering was tested with direct requests.
- No accessibility review (keyboard use, screen readers, contrast).
- The pages need the CDNs; not tested offline.
- Migrations were only run against LocalDB, and only the initial migration exists (no upgrade path between versions was tried).
- Only LocalDB was used, not a full SQL Server instance, and HTTPS/production settings were not tried.
- There are no handler or integration tests. The HTTP checks were done by hand with curl and small scripts.
- **Docker:** `docker compose config` validates and the app was run locally with the container settings (no HTTPS redirect, persisted keys, `/healthz`, database retry), but the image build and `docker compose up` were never run. The Dockerfile, the secret handling, the SQL Server healthcheck and the volume permissions are untested.
- Seeding time will differ on other hardware.

## Shortcuts

- Hardcoded users with plain-text passwords (`Features/Auth/AuthSetup.cs`): no Identity, hashing or lockout.
- The migration runs inside the web app at startup. That's fine for a single instance; with several instances you'd usually run it as a separate deployment step (EF takes a lock so they won't collide, but the first request still waits).
- `ListOrders` returns an `IQueryable` so Telerik can page and filter it in SQL. Handlers use `AppDbContext` directly, without a repository layer.
- The cursor page has no total count, no jump-to-page, sorts by one column only and assumes non-null sort values.
- The dispatcher has no pipeline behaviours (logging, validation).
- Uploaded files stay on local disk: no virus scan, and they aren't removed when an order is deleted.
- The customer dropdown loads all 200 customers once and filters in the browser.
- Seed dates end on a fixed date (2026-09-30) so the data is repeatable.
- Kendo scripts come from the CDN instead of being served locally.
- Cookie auth uses the defaults, with no extra hardening.
- Not built: dynamic forms, Excel export, dark mode.

## Screen recording script (5 to 7 minutes)

1. **0:00 Intro (30 s).** Razor Pages on .NET 10 with Telerik UI, EF Core and SQL Server, 150,000 orders. Nothing is loaded into memory; paging, sorting and filtering happen in SQL.
2. **0:30 Read-only user (45 s).** Sign in as `user`. There is no New order button and no Edit or Delete, only Details.
3. **1:15 Grid at scale (90 s).** Page through, change the page size. Sort by Total and Customer. Filter Status, then Date, then Customer. Mention `ServerOperation(true)`, `ToDataSourceResultAsync`, `AsNoTracking`, the projection to a view model and the indexes.
4. **2:45 Details window (45 s).** Open Details; the window loads its content by AJAX (`OnGetDetailsAsync`). Switch between Summary, Notes and Attachments. Show the search box, the customer ComboBox, the status cards (from `/api/orders/summary`) and the "Last viewed" note from the session.
5. **3:30 Admin (30 s).** Sign in as `admin`. The New order, Edit and Delete controls appear. The role is checked in every handler; hiding the buttons is only for convenience.
6. **4:00 Create and validate (90 s).** New order: save empty to see the validation messages. Filter the customer dropdown by typing. Pick a date. Enter a total of 1 (range error). Choose Shipped and save: "A new order cannot start as Shipped." appears on the Status field. Fix it and save.
7. **5:30 Edit and delete rules (60 s).** Edit an order to Processing. Try to edit a Shipped order and to delete one; both are refused. Delete a New order. Upload a .txt in Attachments and try an .exe.
8. **6:15 Cursor paging and wrap-up (45 s).** Open `/Orders/Cursor`, use Next and Previous, and explain the trade-off against offset paging. Close with the shortcuts listed above and what comes next: ASP.NET Identity, more tests, a separate migration step.
