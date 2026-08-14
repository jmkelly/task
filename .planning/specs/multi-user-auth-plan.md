# Multi-User Plan: Accounts, Per-User API Keys, and Per-User Task Isolation

Status: Revised draft (direction confirmed 2026-08-14 — per-user isolation)
Created: 2026-08-12 (original draft), rewritten 2026-08-14
Supersedes: the "Option A (shared board, authenticated access)" draft of 2026-08-12

## 0. Why This Plan Exists (Current Gap)

The system currently has **no authentication at all**:

- The API (`Task.Api/ApiHost.cs`, `ServerHost.cs`) registers `UseCors("AllowAll")` and calls
  `UseAuthorization()`, but **no authentication/authorization services are ever registered**.
  Anyone who can reach the port can read, create, edit, and delete every task.
- "Users" exist only as a **free-text `assignee` string** on tasks (`TaskItem.Assignee`).
  There is no user table, no accounts, no per-user data scoping.
- **Lesson from history:** API-key support was added to the CLI in commit `b0e7f56`
  (2026-03-03) but was silently dropped by the refactor in `3809004` (2026-03-06).
  This plan puts auth in the **API layer** with tests, so future refactors cannot drop it silently.

## 1. Direction (Confirmed Decisions)

Per-user isolation was chosen over the shared-board option. Locked decisions:

1. **Users have accounts** with a signup/login flow (username + password, web page).
2. **Each user manages their own API keys** on a web page ("API keys" page): create, list, revoke.
3. **Keys are DB-backed** (`api_keys` table) — the DB is the single source of truth.
   No server-level `Auth:ApiKeys` config key. `task server run` needs no key supplied.
4. **Tasks are per-user**: each user sees and operates only on their own tasks.
5. **The CLI authenticates with a user's API key**, supplied at install/configure time
   (`task config set api.key <key>`), closing the loop: sign up → create key on web page →
   configure CLI → CLI works against that user's board.
6. **No anonymous escape hatch.** The old `RequireAuth=false` idea is dropped — it would
   contradict isolation. Upgrading is a deliberate, documented breaking change.

### Defaults chosen (2026-08-14, open questions resolved by author)

| Question | Default |
|---|---|
| Web auth mechanism | Username/password, PBKDF2-hashed (built-in `Rfc2898DeriveBytes`, no new deps), ASP.NET Core cookie sessions |
| Board page auth | Same session cookie; board becomes the logged-in user's board |
| User creation | Self-registration (`Auth:AllowSignup=true` default); **first registered user becomes admin**; `Auth:AllowSignup=false` + `task users create` for closed setups |
| Admin powers (v1) | View any board, list users, revoke any key. No user deletion in v1 |
| Sharing | **Strict isolation in v1.** `assignee` remains a free-text label with zero cross-user visibility. Shared/team tasks are future work |
| Legacy tasks | `user_id = NULL` (unowned) → visible only to admin; **best-effort auto-claim at signup**: tasks whose `assignee` matches the new username (case-insensitive, exact) are claimed by that user |
| Key format | `tk_` + base64url(32 CSPRNG bytes); SHA-256 hash stored; plaintext shown exactly once at creation |
| Revocation | Immediate (checked per request); a user revoking their last key locks themselves out of the CLI until they create another via the web page — accepted |
| CLI key input | `task config set api.key <key>` persists; `TASK_API_KEY` env var overrides; no per-command flag |
| 401 UX | Actionable message including the server's keys-page URL |
| Public endpoints | `GET /api/health`, `/api/auth/*` (signup/login/logout/me); everything else requires auth (session or key) |
| Attribution | `created_by`/`updated_by` (user_id) columns included in the migration — audit only, no filtering |
| Out of scope (v1) | Password reset, MFA, rate limiting, key expiry, shared tasks, teams, user deletion |

## 2. Current Architecture Summary

```
┌─────────────────┐    HTTP (no auth)   ┌─────────────────┐   SQL   ┌──────────────┐
│   Task CLI      │────────────────────▶│   Task API      │────────▶│ SQLite / PG  │
│  (ApiClient.cs) │                     │  (ApiHost.cs)   │         │ tasks table  │
└─────────────────┘                     │  TasksController│         └──────────────┘
                                        │  Razor Pages UI │
                                        └─────────────────┘
```

- **API**: ASP.NET Core minimal host (`ApiHost.cs` / `ServerHost.cs`), one controller
  (`Task.Api/TasksController.cs`, routes under `/api/tasks/*`), Scalar/OpenAPI in dev.
- **Web UI**: Razor Pages board (`Task.Api/Pages/Index.cshtml` + partials) driven by
  htmx. Every board action (`Index.cshtml.cs` handlers: refresh, create, edit, delete,
  update-status, clear-board) calls `ITaskService` **directly** — the browser never
  fetches `/api/tasks/*`. Each request already carries an antiforgery token.
- **Database providers**: SQLite (`Task.Core/Database.cs`) and PostgreSQL
  (`Task.Core/Providers/Postgres/PostgresTaskService.cs`), behind `ITaskService`.
  Migrations keyed by name (`RunSqliteMigration`, Postgres equivalent).
- **CLI**: `Task.Cli/ApiClient.cs` talks to the API over HTTP with no credentials.
- **Assignees**: free-text string column (`tasks.assignee`), no identity semantics.

## 3. Target Architecture

```
┌─────────────────┐  X-Api-Key header  ┌────────────────────────┐  SQL  ┌──────────────┐
│   Task CLI      │───────────────────▶│  Task API              │──────▶│ users        │
│  config api.key │                    │  ApiKeyAuthentication  │       │ api_keys     │
└─────────────────┘                    │  Cookie Authentication │       │ tasks        │
┌─────────────────┐  session cookie    │  TasksController       │       │ (user_id)    │
│  Browser (Razor)│───────────────────▶│  AuthController        │       └──────────────┘
│  board, login,  │                    │  KeysController        │
│  signup, keys   │                    └────────────────────────┘
└─────────────────┘
```

- **Two auth schemes, one principal model.** Web browser: cookie session (ASP.NET Core
  cookie auth). CLI/AI agents: `X-Api-Key` header via a custom authentication handler.
  Both resolve to the same claims identity carrying `user_id` + `username` (+ `is_admin`).
  `[Authorize]` on every data route; controllers never see a credential, only the identity.
- **User isolation is enforced in the data layer**, not just the controller: every
  `ITaskService` method gains a `userId` parameter and both providers scope queries with
  `WHERE user_id = @userId`. A controller passing the wrong user can't leak rows.

## 4. Data Model

New migration (one keyed migration, both providers, same commit):

**`users`**
| column | type | notes |
|---|---|---|
| `id` | TEXT (uid) | pk |
| `username` | TEXT | unique, case-insensitive |
| `password_hash` | TEXT | PBKDF2, per-user salt, constant-time compare |
| `is_admin` | INTEGER | 0/1 |
| `created_at` | TEXT | ISO-8601 |
| `disabled_at` | TEXT | nullable; disabled users rejected at login and per-request |

**`api_keys`**
| column | type | notes |
|---|---|---|
| `id` | TEXT (uid) | pk |
| `user_id` | TEXT | FK → users.id, indexed |
| `name` | TEXT | e.g. `cli-laptop`, `ci-bot` |
| `key_hash` | TEXT | SHA-256 of `tk_...` key; unique; never plaintext |
| `created_at` | TEXT | ISO-8601 |
| `last_used_at` | TEXT | nullable, updated opportunistically (not per-request hot path) |
| `revoked_at` | TEXT | nullable; revoked keys rejected immediately |

**`tasks`** — add:
| column | type | notes |
|---|---|---|
| `user_id` | TEXT | nullable (legacy rows); indexed; new rows always set |
| `created_by` | TEXT | nullable, user_id; audit only |
| `updated_by` | TEXT | nullable, user_id; audit only |

Legacy `tasks` rows get `user_id = NULL` during migration. `assignee` stays a free-text label.

## 5. Access Rules

- **Anonymous** (no session, no key): `GET /api/health`, `POST /api/auth/signup`,
  `POST /api/auth/login`. Everything else → 401 with a JSON body.
- **Authenticated user**: all `/api/tasks/*` routes scoped to `user_id`; own API keys
  (`/api/keys/*`); `/api/auth/me`, `/api/auth/logout`.
- **Admin**: additionally `GET /api/admin/tasks` (any board, read-only in v1),
  `GET /api/admin/users`, `POST /api/admin/keys/{id}/revoke`.
- **Unowned tasks** (`user_id = NULL`): visible to admins only (data-recovery path).
- **Disabled users**: rejected at login and at every authenticated request (checked in the
  API-key handler and a cookie-auth event hook).

## 6. Implementation Phases

### Phase 1: Schema & Security Primitives (no behavior change)
1. Migration (SQLite `Database.cs` + Postgres provider, same commit): `users`,
   `api_keys`, `tasks.user_id` + `created_by`/`updated_by` + index on `user_id`.
2. `Task.Core` utilities: `PasswordHasher` (PBKDF2, salt, constant-time compare),
   `ApiKeyGenerator` (`tk_` + base64url(32 bytes), SHA-256 hash, validate format),
   `AuthModels` (claims helpers: `user_id`, `username`, `is_admin`).
3. Unit tests: migration idempotency (re-run on existing DB), password hash round-trip,
   key generate/hash/validate, hash uniqueness, revoked-key parsing.

### Phase 2: Accounts & Sessions (web auth)
1. `AuthController` (`/api/auth/*`): `signup` (validate username, hash password, create
   user; **first user ever → admin** — the COUNT check and the insert run in one
   transaction so two concurrent signups can't both become admin), `login` (verify,
   issue cookie session), `logout`, `me` (current identity).
2. Register cookie authentication in `ApiHost.ConfigureServices` / `ServerHost`:
   HttpOnly, SameSite=Lax, Secure when available; `/api/*` returns 401 JSON (no
   redirect), Razor pages redirect to `/login`.
3. `Auth:AllowSignup` setting (default `true`); when false, signup returns 403 with an
   actionable message ("ask an admin to create your account").
4. Replace the `AllowAll` CORS policy: `AllowAnyOrigin` cannot carry credentials, and
   the board is same-origin — remove CORS or restrict it to explicit origins with
   `AllowCredentials`.
5. Add `GET /api/health` (anonymous) — no health endpoint exists today.
6. Integration tests: signup → login → me; bad password → 401; duplicate username → 409;
   AllowSignup=false → 403; disabled user → 401; first-user-is-admin (including a
   concurrent-signup check).

### Phase 3: API Keys (CLI/agent auth)
1. `ApiKeyAuthenticationHandler`: read `X-Api-Key` → validate format → hash → lookup →
   check `revoked_at` and user `disabled_at` → claims identity or 401. Timing-safe.
2. `KeysController` (`/api/keys/*`, auth required): `GET` (own keys, **hash never
   returned**, only id/name/created/revoked/last_used), `POST` (create: generate, store
   hash, return plaintext **exactly once**), `DELETE {id}` (revoke, idempotent).
3. Revocation is immediate — handler checks per request. Never log the key; redact in any
   config dump.
4. Integration tests: no key → 401; malformed key → 401; unknown key → 401; valid → 200;
   revoked → 401 immediately; key of disabled user → 401; create returns plaintext once,
   subsequent GET has no plaintext; key scoped to owner (A's key cannot list B's keys).

### Phase 4: Per-User Isolation (the security boundary)
1. Thread `userId` through `ITaskService`: every method gains a `userId` parameter
   (`GetAllTasksAsync`, `GetTaskByUidAsync`, `AddTaskAsync`, `UpdateTaskAsync`,
   `DeleteTaskAsync`, `CompleteTaskAsync`, `SearchTasksAsync`, unique tags/projects/
   assignees, `GetTasksDependingOnAsync`, `ValidateDependenciesAsync`,
   `ArchiveAllTasksAsync`, export/import paths). Both providers add
   `WHERE user_id = @userId` to every query (aliased in joins — e.g. `t.user_id` in the
   FTS query); writes set `user_id`; `GetTaskByUidAsync` returns null when the uid
   belongs to another user.
2. `TasksController` **and the board's Razor handlers (`Index.cshtml.cs`)** resolve the
   user from claims (never from the body/query) and pass it down — the board bypasses
   the controller entirely, so scoping only the controller would leave every board
   action unscoped. `UpdateTaskAsync` refuses cross-user updates (uid not found → 404).
3. **Legacy claim**: on signup, tasks with `assignee` == new username (case-insensitive,
   exact) are claimed (`user_id` set). Runs in the signup transaction.
4. Integration tests (**both SQLite and Postgres**): user B cannot get/list/search/edit/
   delete/complete/export/import/archive any of user A's tasks; A's board contains exactly
   A's tasks; uid lookups for foreign tasks → 404 (not 403, no existence leak); dependency
   validation cannot reference foreign uids; unowned tasks invisible to normal users,
   visible to admin; claim-at-signup correctness; migration idempotency on a DB with data.

### Phase 5: Web UI
1. Razor Pages: `Login`, `Signup`, `Keys` pages (styled like the existing board).
   Keys page: list own keys (name, created, last used, revoked badge), create (show
   plaintext once in a copy box), revoke with confirm.
2. Board (`Index.cshtml`): htmx requests carry the session cookie automatically; the
   cookie-auth middleware redirects page/handler requests to `/login` while `/api/*`
   returns 401 JSON. htmx follows the 302 and would swap the login page into the board,
   so add a small `htmx:responseError` handler that sends the browser to `/login`; the
   board renders the signed-in user's tasks only.
3. Nav: login/signup links when anonymous; username + logout + "API keys" link when
   authenticated.
4. Manual/ghostchrome tests: signup → create key → board CRUD → revoke key → board still
   works (session) but CLI/API key calls fail; logout → board redirects to login.

### Phase 6: CLI Client
1. `Task.Cli/Config.cs`: `api.key` setting (`task config set api.key <key>`); environment
   variable `TASK_API_KEY` takes precedence; neither configured → requests sent without
   the header (server decides).
2. `Task.Cli/ApiClient.cs`: attach `X-Api-Key` header when configured; map 401 to a
   clear error: "Your API key is missing, invalid, or revoked. Create one at
   {server}/keys or run `task config set api.key <key>`."
3. Update `HelpCommand.cs` and README: signup → keys page → `config set api.key` walkthrough.
4. CLI tests: header sent when configured; omitted when not; env var overrides config;
   401 maps to the actionable message; commands work end-to-end against a secured server.

### Phase 7: Admin & Ops
1. `Auth:AllowSignup=false` documented flow: `task users create <username> [--admin]`
   (server-host CLI, local-only). It writes users through the same DB
   (`DatabaseConnectionSettings`), reusing the API's exact PBKDF2 hasher so hashes are
   interchangeable — no API call and no admin key required (works before first signup).
2. Admin Razor page: list users, revoke any key, read-only view of any board.
3. README server-setup section: first-run behavior (signup enabled, first user is admin),
   closed-signup configuration, backup note (`api_keys` hashes are not recoverable —
   backing up the DB preserves keys; losing the DB means issuing new keys).

### Phase 8: (Out of v1 — noted for later)
Rate limiting on auth endpoints, key expiry/rotation, password reset + email, MFA,
shared/team tasks, user deletion, per-user export of own data. Each is a separate gated plan.

## 7. Backward Compatibility & Migration

- **Upgrade path for existing installs** (documented in README/CHANGELOG):
  1. Deploy the new server. Migration runs: `users`/`api_keys` created empty,
     `tasks.user_id` = NULL (unowned), `created_by`/`updated_by` backfilled to NULL.
  2. First visitor signs up → becomes admin → sees all unowned legacy tasks.
  3. Other users sign up; tasks whose `assignee` matches their username are auto-claimed.
  4. Unmatched tasks stay visible to the admin, who can reassign (edit assignee) or leave
     them.
- **Breaking change (by design):** anonymous read/write access is removed; the board page
  now requires login; scripts/agents must use a key. There is **no** `RequireAuth=false`
  escape hatch — isolation is the point. The 401 error paths make the remediation obvious.
- **CLI**: with no `api.key`, requests go out unauthenticated and fail with the actionable
  message on a secured server (which is every server, post-upgrade).
- **Postgres parity**: every schema/query change lands in both providers in the same
  commit; the test matrix runs both providers locally.

## 8. Testing Plan

- **Unit (Task.Core):** password hash round-trip + wrong-password rejection, key
  generate/hash/validate, migration idempotency on fresh and populated DBs.
- **Integration (Task.Api.Tests, SQLite + Postgres):** auth matrix (no/bad/revoked/valid
  key, session flows, disabled user); signup/login/logout/me; keys CRUD + revocation +
  single-plaintext-display; **isolation matrix per route** (Phase 4); admin endpoints;
  Scalar/OpenAPI still load.
- **CLI:** header injection, env-var precedence, 401 mapping, `config set api.key`.
- **UI (ghostchrome):** signup → login → board CRUD → keys page create/revoke → logout;
  board 401-redirect behavior.
- **Existing suites:** `ApiIntegrationTests`, `BlockedStatusUiTests`,
  `KanbanDoneOrderingUiTests` currently hit `/api/tasks` and `/` anonymously — migrate
  them to authenticated flows (shared helper: signup/login per test or a test key), and
  extend the `ClearDatabase()` fixture to the new tables.
- **Manual E2E:** fresh server → signup (admin) → create key → `config set api.key` →
  CRUD via CLI → revoke via web → CLI fails with actionable message → second user signup
  sees only their tasks; legacy DB upgrade scenario with claim matching.
- **Security checklist:** no plaintext keys/passwords in DB or logs; keys shown once;
  timing-safe compares; 404 not 403 for foreign uids; `user_id` never client-supplied.

## 9. Risks / Mitigations

| Risk | Mitigation |
|---|---|
| Auth dropped again in a future refactor (happened with `b0e7f56`) | Auth + isolation live in API layer and both providers; integration tests fail the build if `[Authorize]` or user scoping is removed |
| Breaking existing scripted/AI-agent integrations | Documented breaking change, actionable 401 messages, clear upgrade steps; keys page makes the fix a 2-minute task |
| Key/password leakage (logs, git history, backups) | Hashes at rest; plaintext key shown once; never log headers; redact config dumps |
| Isolation bug leaks another user's tasks | Scoping in the data layer (not just controller) + per-route isolation test matrix on both providers; foreign uids → 404 |
| Legacy data stranded after upgrade | Admin sees unowned tasks; best-effort claim at signup; documented reassignment path |
| Signup abuse (open registration) | `Auth:AllowSignup=false` + `task users create`; rate limiting deferred to Phase 8 |
| SQLite vs Postgres drift | Same-commit dual-provider rule + local test matrix for both |
| Scope creep (sharing, teams, MFA…) | Phase 8 gate; assignee stays a label in v1 |

## 10. Definition of Done (v1)

- [ ] Signup/login/logout works; first user becomes admin; `me` returns identity
- [ ] No unauthenticated access to any data route; only `/api/health` and `/api/auth/*` are anonymous
- [ ] Users create/revoke their own keys on the web page; plaintext shown exactly once; revocation takes effect immediately
- [ ] Every user sees and operates only on their own tasks — via both the API controller **and the board handlers** (isolation matrix green on SQLite **and** Postgres)
- [ ] Legacy tasks are unowned → admin-visible; claim-at-signup works; upgrade path documented
- [ ] CLI works with `task config set api.key <key>` (and `TASK_API_KEY`); 401 produces the actionable message
- [ ] Board page requires login and shows only the signed-in user's tasks
- [ ] Existing test suites (API integration, board UI tests) migrated to authenticated flows; `ClearDatabase()` covers the new tables
- [ ] Admin can view any board, list users, revoke any key; `Auth:AllowSignup=false` + `task users create` supported
- [ ] Help/README/CHANGELOG updated with the upgrade path and setup steps
