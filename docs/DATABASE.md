# Database & schema

In Docker the deployed database is **MariaDB 11** in container `recipebook-db`,
database `recipe_book_db`, application user `recipebook`. The app creates its own
schema at startup, so there is no SQL to run by hand.

## How the schema gets created

`Program.cs` calls `db.Database.EnsureCreated()` on startup:

- Creates the **database and any missing tables** if they do not exist yet.
- **Never alters an existing table** — no `ADD COLUMN`, no type changes.
- No EF Core migrations are checked in, so `dotnet ef database update` is not the
  workflow here.

Consequence: after **any data-model change** (new property, renamed column, new
enum stored shape) you must drop the affected tables and let `EnsureCreated()`
rebuild them. Left in place, old tables linger and startup either fails or the app
reads columns that are no longer mapped.

## Choosing a reset method

| You want | Use |
| --- | --- |
| Clean slate after a schema change | **A** — wipe the volume |
| Rebuild schema, keep the container running | **B** — drop/recreate the database |
| Empty the data, keep the schema | **C** — truncate every table |

### A. Nuclear — wipe the whole volume (cleanest)

```bash
docker compose down                                    # stop app + db, volumes kept
docker volume rm recipebook-csharp_recipebook-db-data  # delete the data volume
docker compose up -d --build                           # EnsureCreated() rebuilds empty schema
```

> Confirm names before deleting anything: `docker volume ls | grep recipebook`.
> Volume names are prefixed with the Compose **project directory name**, so adjust
> if yours is not `recipebook-csharp`. Never run `volume rm` blind.

> `docker compose down -v` also deletes `recipebook-dpkeys` (the session-cookie
> encryption key ring). That is harmless but produces extra log noise on the next
> boot. To wipe only the data, use the explicit `volume rm` above.

### B. Drop just the database, keep the container

```bash
docker exec -it recipebook-db mariadb -urecipebook -p"$RECIPEBOOK_DB_PASSWORD" \
  -e "DROP DATABASE recipe_book_db; CREATE DATABASE recipe_book_db;"
docker compose restart app                             # EnsureCreated() recreates tables
```

### C. Clear rows only, keep the schema

```bash
docker exec -it recipebook-db sh -c \
  'mariadb -urecipebook -p"$RECIPEBOOK_DB_PASSWORD" recipe_book_db \
   -e "SET FOREIGN_KEY_CHECKS=0; $(mariadb -N -B -urecipebook -p\"$RECIPEBOOK_DB_PASSWORD\" recipe_book_db -e \"SHOW TABLES\" | sed "s/.*/TRUNCATE TABLE \`&\`;/")"'
```

## After a full reset (A or B)

The `Users` table is gone too. Open the app, hit **register**, and re-create your
account — there is no seeded/default user.

## Verify what exists

```bash
docker exec -it recipebook-db mariadb -urecipebook -p"$RECIPEBOOK_DB_PASSWORD" \
  recipe_book_db -e "SHOW TABLES; SELECT COUNT(*) AS users FROM Users;"
```

## Local (non-Docker) equivalent

Create the empty database once and point the connection string at it:

```sql
CREATE DATABASE recipe_book_db;
```

To rebuild the local schema after a model change:

```bash
mysql -uroot -p -e "DROP DATABASE recipe_book_db; CREATE DATABASE recipe_book_db;"
dotnet run --project RecipeBook        # EnsureCreated() recreates the tables
```
