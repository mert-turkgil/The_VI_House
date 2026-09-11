# Restoring the production database

Read this when a deploy has changed the database and the result is wrong: rows missing, a column
emptied, a migration that did more than it should. Nothing here is automatic — restoring is a
deliberate, manual act, and it overwrites whatever the site has written since the backup was taken.

## Where the backups are

| Backup | Where | Made when | Restored how |
|---|---|---|---|
| **Pre-migration `.bacpac`** (made by `deploy.yml`) | FTP root → `/Backup/vihouse-YYYYMMDD-HHMMSS-<commit>.bacpac` — next to `wwwroot/`, never inside it | Automatically, right before a deploy applies EF migrations (or every deploy if `BACKUP_ALWAYS` is `'true'`). The last `BACKUP_KEEP` (10) are kept. | Not by the panel — see Path A / B below |
| **Panel backup** (`.bak`) | MonsterASP panel → Databases → your database → *Backups management* | Only when you click **Create Backup** | One click: *Restore* (overwrites) |
| **MonsterASP daily backup** (`.zpaq`) | FTP, after enabling backup access in the panel | Daily, by MonsterASP | Their support / their tooling |

The `.bacpac` is a zip of the schema plus every row of every table. It contains members' personal
data: download it over FTP to your own machine only, and delete local copies when you are done.

**Before a deploy you already know is risky** (a migration with `DropColumn`, `DropTable`, or an
`AlterColumn` that changes a type), press **Create Backup** in the panel first. That is the only
backup with a one-click restore; everything below is the longer road for when you did not.

## Step 0 — stop the app

Restoring under a running app means it keeps writing while you overwrite. In WebFTP (or FileZilla),
upload `.github/deploy/app_offline.htm` into `wwwroot/`. IIS stops the app within seconds and shows
the maintenance page. The next deploy deletes the file again at the end; you can also delete it by
hand once you are finished.

## Path A — full restore, via the panel (recommended)

The panel only restores `.bak` files, so the `.bacpac` goes through your local SQL Server first.

1. Download the `.bacpac` you want from `/Backup/` — pick the one whose commit hash matches the
   deploy that went wrong (the run's summary page on GitHub names the file too).
2. In SSMS, connect to `DESKTOP-CO3CLF6\MSSQLSERVER01`. Right-click **Databases → Import
   Data-tier Application…**, choose the file, name the new database `VIHouse_Restore`, finish.
3. Sanity-check it: `SELECT COUNT(*) FROM VIHouse_Restore.dbo.AspNetUsers` and whichever table you
   are worried about. This is the moment to notice you picked the wrong file.
4. Right-click `VIHouse_Restore` → **Tasks → Back Up…** → type *Full*, destination *Disk*, save as
   `VIHouse_Restore.bak`.

   **Version check, once, before relying on this:** a `.bak` restores only onto the same or a newer
   SQL Server than the one that wrote it. Your local instance is SQL Server 2025 and MonsterASP
   advertises MSSQL 2025, but confirm the actual server behind `db67365` with
   `SELECT @@VERSION` (SSMS connected to `db67365.public.databaseasp.net`). If it is older than
   2025, the panel will refuse the file — use Path B instead.
5. Panel → Databases → your database → **Backups management → Upload Backup**, choose
   `VIHouse_Restore.bak`, then **Restore** and confirm the overwrite prompt.
6. **Fix the code before the next deploy.** The restore also rewinds `__EFMigrationsHistory`, so
   the migration that caused this is "pending" again and the next push to `master` would re-apply
   it. `git revert` the commit that added it (or replace it with a corrected migration), then push.
   That deploy takes a fresh backup, applies whatever is now pending, and removes `app_offline.htm`.

## Path B — full restore with SqlPackage (no `.bak`, no version constraint)

Use this if the panel rejects the `.bak`, or you would rather not go through SSMS twice. SqlPackage
refuses to import into a database that already has objects, so the database is emptied first.

1. Step 0, then download the `.bacpac` as above.
2. In SSMS connected to `db67365.public.databaseasp.net`, empty the database. This drops every
   table — it is the point of no return, so make sure the `.bacpac` you downloaded opens first
   (Path A step 2 is a good way to check):

   ```sql
   DECLARE @sql NVARCHAR(MAX) = N'';
   SELECT @sql += N'ALTER TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name)
                + N' DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';' + CHAR(10)
   FROM sys.foreign_keys fk
   JOIN sys.tables t ON t.object_id = fk.parent_object_id
   JOIN sys.schemas s ON s.schema_id = t.schema_id;
   SELECT @sql += N'DROP TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N';' + CHAR(10)
   FROM sys.tables t
   JOIN sys.schemas s ON s.schema_id = t.schema_id;
   EXEC sp_executesql @sql;
   ```
3. From your PC (`dotnet tool install --global microsoft.sqlpackage` once):

   ```
   sqlpackage /Action:Import /SourceFile:"vihouse-....bacpac" /TargetConnectionString:"<the PROD_CONNECTION_STRING value>"
   ```
4. Path A step 6: revert or fix the migration, push, and let the deploy bring the site back.

## Path C — surgical restore (one table or column, no downtime)

When a deploy emptied one column or dropped one table and everything else is fine, do not overwrite
the whole database — you would lose every sign-up and edit made since the backup.

1. Path A steps 1–2: get the `.bacpac` into `VIHouse_Restore` on your local instance.
2. Copy only what was lost back to production. For a whole table, SSMS's **Tasks → Export Data…**
   on `VIHouse_Restore` (destination: SQL Server `db67365.public.databaseasp.net`, SQL login
   `db67365`) can append rows to the production table. For a single column, write an `UPDATE`
   joined on the primary key against a linked copy, or script the values out of `VIHouse_Restore`
   and run them on production.
3. No `app_offline.htm` needed, and nothing to revert in git unless the migration itself is wrong.

## Afterwards

- Open the site and sign in to the admin. If you used Path A or B, the data-protection keys were
  never touched (they live in `App_Data`, not the database), so existing cookies still work.
- Delete `wwwroot/app_offline.htm` if no deploy has done it for you.
- Delete the `.bacpac` and `VIHouse_Restore` from your PC.
