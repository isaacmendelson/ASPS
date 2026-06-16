---
name: ef-migrate
description: Add and apply an EF Core migration in the ASPS Business project. Avoids the DLL-lock + cross-branch traps the team has been bitten by repeatedly.
---

# /ef-migrate

Wraps `dotnet ef migrations add` + `database update` with the safety steps that have burned us before.

## When to invoke
- User wants to add a new EF migration (entity, column, FK, index, schema change).
- User says "migrate", "add migration", "apply migration", "update DB schema".

## Before any command — verify

1. **ASPSBackend is NOT running.** If it is, the DLLs are locked and `dotnet ef` will either fail or — worse — succeed using stale assemblies (this happened in SCRUM-904 Step B). Check with:
   ```bash
   tasklist | grep -i ASPSBackend
   ```
   If running, **ask the user to stop it** before proceeding. Do not kill processes yourself.

2. **Branch is correct.** Migrations are branch-scoped. Confirm:
   ```bash
   git branch --show-current
   ```
   Surface the branch name to the user before adding a migration on it.

3. **DB state matches the branch.** If the user just switched branches, the local MySQL state may not match this branch's expected migration history. A quick check:
   ```bash
   "C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe" -h 127.0.0.1 -P 3306 -uroot -pzappa22 ASPSBackend2DB \
     -e "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC LIMIT 5"
   ```
   If the latest applied migration looks foreign to this branch, **stop and tell the user** — don't add on top of an unknown state.

## Add a migration

Name format: `SCRUM###_<ShortDescription>` (matches existing convention — e.g. `20260606152402_SCRUM904_AddUserRiskProfileTable`).

```bash
cd ASPSBackend14_J
dotnet ef migrations add SCRUM###_<Name> --project Business --startup-project ASPSBackend
```

After it succeeds:

1. **Open the generated `<timestamp>_<Name>.cs`** under `Business/Data/EF/Migrations/`.
2. **Read the `Up()` method.** Verify the generated SQL is what the user asked for — no unintended drops, renames, or data loss.
3. **Show the user the migration filename + a 1-2 line summary** of what it does (which columns / tables / FKs change).
4. Wait for confirmation before applying.

## Apply

```bash
dotnet ef database update --project Business --startup-project ASPSBackend
```

⚠️ Do **NOT** pass `--no-build`. The session memory documents that `--no-build` combined with branch switching has caused EF to use stale assemblies and apply a migration that didn't reflect the current code. Always let it build.

After apply:
- Check `__EFMigrationsHistory` to confirm the new MigrationId is at the top.
- Run a quick `DESCRIBE <table>` or equivalent to confirm the schema matches the migration's intent.

## Never

- Edit a migration file after applying it. Create a follow-up migration instead.
- Delete a migration file that's been pushed (someone else's local DB has applied it).
- Run `database update` while ASPSBackend or WebApi is running.
- Apply the seed dump (`aspsbackend2db_*.sql`) — that's user-owned per CLAUDE.md.

## Output convention

When you've finished:
```
Migration: <timestamp>_<Name>.cs
Changes: <one-line summary>
Applied: yes/no
__EFMigrationsHistory head: <MigrationId>
```
