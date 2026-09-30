# Orders

A small orders and customers app built with ASP.NET Core 10 Razor Pages, Telerik UI for ASP.NET Core, EF Core and SQL Server. All data is generated sample data.

- 150,000 orders and 200 customers, seeded on first start
- Telerik Grid with server-side paging, sorting and filtering, in two versions: offset paging (`/Orders`) and cursor paging (`/Orders/Cursor`)
- Popup editing with a filterable customer dropdown, a date picker, DataAnnotations and business-rule errors shown on the field
- Details window (Summary / Notes / Attachments tabs), file upload, a small REST API, a status summary, session state
- Cookie login with two roles: Admin can add, edit and delete; User is read-only

## Setup

You need the .NET 10 SDK, SQL Server (LocalDB is fine) and internet access, because the Kendo scripts, theme and Inter font load from CDNs.

1. **Telerik NuGet feed.** `NuGet.config` lists the feed but not the credentials. Add them to your user-level config once:
   ```
   dotnet nuget add source https://nuget.telerik.com/v3/index.json -n TelerikOnlineFeed -u api-key -p <YOUR_API_KEY> --store-password-in-clear-text --configfile "%APPDATA%\NuGet\NuGet.Config"
   ```
   The source name has to stay `TelerikOnlineFeed`.
2. **Telerik licence.** Put `telerik-license.txt` in `%APPDATA%\Telerik\` (or set `TELERIK_LICENSE`) before building. Without it the UI shows a trial banner. Don't commit the key.
3. **Database.** The default connection string in `appsettings.json` points at `(localdb)\MSSQLLocalDB`, database `TelerikDemoOrders`.
4. **Run.**
   ```
   cd TelerikDemo2
   dotnet run
   ```
   The first start creates the database and seeds it in batches of 5,000 (about 10 seconds on my machine). To reseed, drop the database.

Sample accounts:

| User  | Password    | Role                           |
|-------|-------------|--------------------------------|
| admin | `Admin123!` | Admin: view, add, edit, delete |
| user  | `User123!`  | User: read-only                |

## Docker

```
cp .env.example .env     # set MSSQL_SA_PASSWORD, TELERIK_NUGET_KEY, TELERIK_LICENSE
docker compose up --build
```
Then open <http://localhost:8080>. Compose starts SQL Server and the app; the app waits for the database and seeds it on first start.

The Telerik feed key and licence are passed to the build as BuildKit secrets, so they don't end up in the repo, the image layers or the final image. Leave `TELERIK_LICENSE` empty to build in trial mode. Data, uploads and the auth keys live in named volumes (`docker compose down -v` removes them). Settings can be overridden with environment variables, for example `ConnectionStrings__DefaultConnection` and `UseHttpsRedirection` (off in the image, TLS is expected to end at a proxy). `GET /healthz` returns 200 when the database is reachable.

Avoid `;` and `=` in the SA password, since it is placed in a connection string.

## Project layout

Code is grouped by feature, and inside a feature by use case.

```
Domain/            entities and OrderRules
Common/            request dispatcher, Result, KeysetPager
Infrastructure/    DbContext, seeder, attachment storage
Features/
  Orders/          pages, one file per use case (ListOrders, GetOrdersPage, CreateOrder, ...), view models,
                   editor templates, REST endpoints
  Customers/       customer lookup
  Auth/            login, logout, roles, permissions
  Shared/          layouts
```

Razor Pages are served from `Features/` instead of `Pages/`. PageModels stay thin: they bind input, check the permission, send one request through `IDispatcher` and shape the response for Telerik. The dispatcher is a small in-house version of the mediator pattern, and handlers are registered by scanning the assembly.

Business rules (no Shipped order on create, Shipped orders can't be changed or deleted, total between 10 and 5,000, no future dates, the customer must exist) are in `Domain/OrderRules.cs` and nowhere else. The same class holds the limits that the view model attribute, the editor templates, the EF mapping and the seeder use. Who can edit is defined once in `Features/Auth/Permissions.cs`; the write handlers enforce it and the views use it to hide buttons. Upload limits come from `Attachments` in `appsettings.json` and are also passed to the Upload widget.

Grid reads use `AsNoTracking`, project to a view model in the query and go through `ToDataSourceResultAsync`, so paging, sorting and filtering run in SQL. There are indexes on `OrderDate` and `Status`. Anti-forgery tokens travel with every Telerik call through `.Data("forgeryToken")`.

## Offset and cursor paging

| | `/Orders` | `/Orders/Cursor` |
|---|---|---|
| Navigation | numbered pages, jump anywhere | previous / next |
| SQL | `OFFSET` plus `COUNT(*)` | `WHERE (sort column, id) > last row seen`, no count |
| Deep pages | slower the deeper you go | same cost at any depth when the sort column is indexed |
| While data changes | rows can shift or repeat | stable |

Both pages share a base PageModel, the grid definition, the filter bar and `wwwroot/js/orders.js`. They only differ in the read handler and the paging bar. The cursor logic is in `Common/KeysetPager.cs`: one sort column plus the id as tie-break, an opaque cursor, and the browser keeps a stack of cursors so Previous works. Changing the sort or filter goes back to page 1.

Timing when paging through all 150,000 rows in pages of 100: 5 to 14 seconds (7 to 10 ms a page) for the indexed columns (`OrderId`, `OrderDate`, `Status`), and roughly 140 seconds (95 ms a page) for `Total` and `CustomerName`, which have no index. Add indexes if those sorts matter.

## Tests

`dotnet test` runs 26 unit tests for `OrderRules` and `KeysetPager` (no database needed). The browser behaviour was checked by hand and with throwaway Playwright scripts that are not part of the repo.

## What has not been verified

- Only Chrome was tested (1440 px and 390 px wide). Firefox, Safari, Edge and real touch devices were not.
- The Upload widget renders and the upload/download handlers work when called directly, but I never selected a file through the browser.
- The grid header filter menus render, but I didn't click through them. The server side of filtering was tested with direct requests.
- No accessibility review (keyboard use, screen readers, contrast).
- The pages need the CDNs; not tested offline.
- Only LocalDB was used, not a full SQL Server instance, and HTTPS/production settings were not tried.
- There are no handler or integration tests. The HTTP checks were done by hand with curl and small scripts.
- **Docker:** `docker compose config` validates and the app was run locally with the container settings (no HTTPS redirect, persisted keys, `/healthz`, database retry), but the image build and `docker compose up` were never run. The Dockerfile, the secret handling, the SQL Server healthcheck and the volume permissions are untested.
- Seeding time will differ on other hardware.

## Shortcuts

- `EnsureCreated()` instead of migrations.
- Hardcoded users with plain-text passwords (`Features/Auth/AuthSetup.cs`): no Identity, hashing or lockout.
- No concurrency check (no rowversion). The last write wins, and the Shipped check isn't atomic with the update.
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
8. **6:15 Cursor paging and wrap-up (45 s).** Open `/Orders/Cursor`, use Next and Previous, and explain the trade-off against offset paging. Close with the shortcuts listed above and what comes next: migrations, ASP.NET Identity, rowversion, more tests.
