# Recipe Book

A self-hosted ASP.NET Core MVC app for keeping a family recipe book: save custom
recipes, build weekly meal plans, and generate shopping lists from them.
Ingredient names can optionally be enriched against the USDA FoodData Central API.

**Just want it running?** [Quick start](#quick-start-docker-compose), then read
[First run](#first-run). Maintainer gotchas live in [Architecture
notes](#architecture-notes) and [Troubleshooting](#troubleshooting).

## Table of contents

- [Features](#features)
- [Stack](#stack)
- [Quick start (Docker Compose)](#quick-start-docker-compose)
  - [First run](#first-run)
- [Configuration](#configuration)
  - [Useful commands](#useful-commands)
- [Architecture notes](#architecture-notes)
  - [Routing: attribute routes only](#routing-attribute-routes-only)
  - [Auth: the session guard is a real global filter](#auth-the-session-guard-is-a-real-global-filter)
  - [Sessions and login persistence](#sessions-and-login-persistence)
  - [Database & schema](#database--schema)
- [Run locally (dev)](#run-locally-dev)
- [Troubleshooting](#troubleshooting)
- [Notes and current limits](#notes-and-current-limits)
- [License](#license)

## Features

- **Recipe book** — per-user recipes with categories, difficulty, times, calories,
  tags, cuisines and ingredient lists.
- **Meal planning** — a plan is a set of `(date, meal type)` slots; each slot can
  hold several recipes.
- **Shopping lists** — ingredients needed by attached meal plans are aggregated on
  read into one list per user.
- **Multi-user** — session-cookie auth, per-user data isolation, hashed passwords.
- **Optional USDA lookup** — free-text fallback when no API key is configured.

## Stack

| | |
| --- | --- |
| Runtime | .NET 8 (`net8.0`), ASP.NET Core MVC |
| Data access | EF Core 8 + Pomelo.EntityFrameworkCore.MySql 8 |
| Database | MySQL / MariaDB (MariaDB 11 in Compose). Pomelo means **no** SQLite/SQL Server |
| Auth | ASP.NET Core `PasswordHasher` + distributed session cookie |
| Optional | [USDA FoodData Central API key](https://api.nal.usda.gov/fdc/) (free) |

## Quick start (Docker Compose)

Docker-only prerequisites: **Docker Engine with the Compose plugin**. The .NET SDK
is *not* needed — `dotnet build` runs inside the `sdk:8.0` build stage of the
Dockerfile.

From the repo root (`~/projects/repos/recipebook-csharp`):

```bash
cp .env.example .env      # then edit: the two passwords are mandatory
docker compose up -d --build
docker compose ps                      # db: healthy (healthcheck), app: running
docker compose logs -f app             # stop once Kestrel reports it is listening
```

Open **http://192.168.8.119:8080** (change the host/IP for your own deployment).

### First run

There is **no seeded account and no default password**. The app opens on a login
page; click **register**, create your user, and you land on the recipe book
(`/recipes`). Every other page requires that session.

To run several households on one instance, register one user each — data is
filtered by `UserId` on every query.

## Configuration

Compose reads `.env` automatically. `.env` is git-ignored; never commit real values.

| Variable | Required | Used for |
| --- | --- | --- |
| `RECIPEBOOK_DB_ROOT_PASSWORD` | yes | MariaDB root password (container-internal only) |
| `RECIPEBOOK_DB_PASSWORD` | yes | app's `recipebook` DB user password; interpolated into the connection string and the healthcheck |
| `USDA_FDC_API_KEY` | no | ingredient autocomplete; empty is fine, entry falls back to free-text |

Other settings come from standard ASP.NET Core variables (see `compose.yaml`):
`ASPNETCORE_ENVIRONMENT`, `ASPNETCORE_URLS`, `CONNECTIONSTRINGS__DEFAULTCONNECTION`,
and `DATAPROTECTION__KEYSDIRECTORY`.

### Useful commands

```bash
docker compose logs -f app     # follow app logs
docker compose ps              # health of app + db
docker compose restart app     # restart app only
docker compose up -d --build   # rebuild after code changes
docker compose down            # stop, keep both volumes
docker compose down -v         # stop and wipe BOTH volumes (data + session keys)
```

> `down -v` deletes `recipebook-db-data` **and** `recipebook-dpkeys`. Losing the
> key ring invalidates every session cookie (all users log in again) and logs a
> `key not found in the key ring` warning on the next boot.

## Architecture notes

Read these before touching routing, auth, or sessions — each has cost an outage.

### Routing: attribute routes only

Every URL comes from `[HttpGet]`/`[HttpPost]` attributes on the controllers.
`Program.cs` registers exactly **one** conventional route,
`{controller}/{action}`, with **no default values**. It exists only so
`RedirectToAction(...)` and `/Home/Error` can generate URLs; attribute routes
always outrank it.

**Never add a second conventional route, and never add defaults.** A pattern that
defaults both segments also matches `/`, which collides with the empty template on
`UserController.Index`. Two of this app's bugs were that collision —
`AmbiguousMatchException` on every request, surfaced as the generic error page.
Current root ownership: `/` is the login page, and logged-in users get bounced to
`/recipes`.

| URL | Controller action |
| --- | --- |
| `/` | `UserController.Index` (login) |
| `/register`, `/login`, `/logout` | `UserController` |
| `/recipes`, `/recipes/{category}` | `RecipeController.Index`, `ViewCategory` |
| `/recipe/{id}/view`, `/recipe/{id}/edit` | `RecipeController` |
| `/mealplan`, `/mealplan/{id}/edit` | `MealPlanController` |
| `/shoppinglist` | `ShoppingListController.Index` |

Links in views are written **absolute** (`/recipe/5/view`). A relative href
resolves against the current URL's directory, so `@id/view` from `/recipes` becomes
`/5/view` and 404s.

### Auth: the session guard is a real global filter

`Filters/SessionCheckFilter.cs` redirects any request with no `Session["uid"]` to
the login page; login/register/logout and `Error` are exempt. It is wired with
`options.Filters.Add<SessionCheckFilter>()` inside `AddControllersWithViews`, plus
`AddScoped<SessionCheckFilter>()` so DI can supply it.

**Registering it as `AddScoped<IFilterMetadata, SessionCheckFilter>()` does
nothing** — MVC collects filters from `MvcOptions.Filters` and attributes, never
from the DI container. That no-op was a real bug: the guard silently never ran, so
expired sessions rendered pages instead of redirecting.

Ordering in `Program.cs` matters: `UseSession()` must come **before** the
endpoints execute, otherwise `Session` is empty for every action and the guard
bounces everyone to login.

### Sessions and login persistence

`Session["uid"]` and `Session["FamilyName"]` drive auth and the page chrome
(`_Layout` hides the title and the global action buttons when there is no session).

Sessions live in an **in-process** cache, so the session object does not survive a
container restart. The cookie's encryption keys do, because they are persisted to
the `recipebook-dpkeys` volume via `PersistKeysToFileSystem`. Keep both and logins
survive rebuilds; wipe the volume and every user must log in again.
Cookies are `SameSite=Strict`, 30-day idle timeout, `SecurePolicy=SameAsRequest`
(plain HTTP on the LAN, HTTPS behind a TLS proxy).

### Database & schema

Startup calls `EnsureCreated()`, which creates missing tables but **never alters
existing ones**, and no EF migrations are checked in. After any data-model change
you must drop and rebuild — see **[docs/DATABASE.md](docs/DATABASE.md)** for the
reset options and the verify/troubleshooting queries.

## Run locally (dev)

Prerequisites: .NET 8 SDK (`dotnet --version` → `8.x`) and a reachable MySQL or
MariaDB server.

```bash
mysql -uroot -p -e "CREATE DATABASE recipe_book_db;"   # any name; match the string below

cd RecipeBook
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Server=localhost;Port=3306;UserId=root;Password=<your-password>;Database=recipe_book_db;"
dotnet user-secrets set "Usda:FdcApiKey" "<your-fdc-api-key>"   # optional
cd ..

dotnet run --project RecipeBook
```

Open **http://localhost:5155** (the port comes from
`Properties/launchSettings.json`; Kestrel prints the URL it bound).
User secrets are keyed by `<UserSecretsId>RecipeBook</UserSecretsId>` in
`RecipeBook.csproj` and live outside the repo. The tables are created on first
startup; the empty database must already exist.

No global `appsettings.json` change is needed — the dev connection string stays in
user secrets so it can never be committed.

## Troubleshooting

| Symptom | Cause | Fix |
| --- | --- | --- |
| Every page shows *"An error occurred while processing your request"* | route ambiguity (`AmbiguousMatchException`); the error page hides the real exception | `docker compose logs app`, search for `AmbiguousMatch`. See **Routing** above |
| Sent to login on every navigation, even right after logging in | keys wiped, `UseSession()` after the endpoints, or the filter not in `MvcOptions.Filters` | check the `recipebook-dpkeys` volume exists; check middleware order in `Program.cs` |
| `Error unprotecting the session cookie ... key not found in the key ring` | new/blank data-protection key ring; expected after the first `up -d --build`, and after `down -v` | log in once; confirm it goes away. If it repeats constantly, the volume is not mounted |
| A page renders with no title/buttons and no redirect | session is empty while the guard is not running | same as the row above; verify the filter is registered in `MvcOptions.Filters` |
| Missing column / schema mismatch after a model change | `EnsureCreated()` does not alter existing tables | reset with [docs/DATABASE.md](docs/DATABASE.md) |
| Build fails on `dotnet restore` inside Docker | no image cache / offline | re-run `docker compose up -d --build`; the restore layer is cached after the first success |
| Ingredient search does nothing | `USDA_FDC_API_KEY` empty (logs `Usda:FdcApiKey is not configured`) | add a key to `.env` or type ingredients by hand |

## Notes and current limits

- **Not a Web App** — session state is per-process, so run **exactly one** app
  container. Scale out needs a Redis/SQL-backed `IDistributedCache`.
- No HTTPS termination here. Put a TLS proxy in front for anything off the LAN;
  cookies already switch to `Secure` on an HTTPS request.
- **Security posture is LAN-only by design.** No rate limiting and no account
  lockout. No POST action carries `[ValidateAntiForgeryToken]` and no global
  antiforgery filter is registered, so coverage rests on ASP.NET Core's implicit
  `FormTagHelper` behaviour rather than anything this app configures. To check a
  rendered page, search its HTML for `__RequestVerificationToken`; if absent,
  those POSTs are unprotected. Current mitigations: `SameSite=Strict` session
  cookies and per-user data scoping. Revisit before leaving the LAN.
- Recipe lists (`Categories`, `Directions`, `Equipment`, `Tags`, `Cuisine`) are JSON
  string columns. **Assign whole lists** when saving; in-place list mutations may
  not be detected by EF8 change tracking.
- A `Meal` is one `(date, type)` slot and can hold several recipes through
  `MealRecipe`. `Meal.MealCalories` is computed from its recipes, never stored.
- Needed ingredients are computed on read, not stored; `EstimatedCost` is manual
  until a price source exists.
- **Stale artifacts**: `schema.PNG` predates the Oct 2026 refactor. Old
  `Product`, `PlanEntry`, `Instruction`, `Step` classes, `Recipe.Type`, and
  hour-based times no longer exist — the Models/ files are the source of truth.
- **AI used with project** Some updates are made by a homelab local AI named 'ALT'. ALT uses model `qwen3.8-flash-next-ud-iq4-xs` and runs on an `Intel Battlemage G21` GPU

## License

MIT — see [LICENSE](LICENSE).
