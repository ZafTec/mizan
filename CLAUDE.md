# Contributor guidance

Read [README](README.md) for setup and verification, and [architecture](docs/ARCHITECTURE.md) for system boundaries. Before frontend framework changes, follow [frontend/AGENTS.md](frontend/AGENTS.md) and read the relevant installed Next.js guide. Read [planning](docs/PLANNING.md) before meal plan, shopping list, or price work.

## Product and ownership

- Mizan logs meals, workouts, and body measurements. Today, History, Progress, More, and Log entry form the navigation. Secondary features belong under More or appear when relevant to the user's data.
- ASP.NET Core owns identity, authorization, business rules, and the entire PostgreSQL schema. Next.js has no database access or independent authentication authority.
- Web, MCP, and Telegram use the same backend commands and permissions. Keep transport adapters thin. Put shared request records in `backend/Mizan.Contracts`.
- Keep pure calculations in Domain/Application and external I/O in Infrastructure/API. Follow existing language and component conventions.
- Ship a feature as one vertical slice: command, API, MCP tool, Telegram command, web page, documentation, and tests in the same pull request. A surface that is left out is named in the pull request.
- Adapters hold no rules. A rule that Telegram, MCP, or the web needs lives in Domain or Application once, and every adapter calls it. Wire records live in `backend/Mizan.Contracts`; an adapter does not declare its own copy.
- The frontend has no nutrition or price arithmetic. It shows what the API returns and sends what the person typed. A write that the person may retry sends an `Idempotency-Key` header.

## Changes that cross boundaries

- Preserve `InitialCreate` and the additive EF migration chain on `master`. Add migrations for later model changes; never reset the baseline as a routine fix. An unmerged branch carries one migration: remove it with the EF tools and generate it again after a model change. Never edit a migration file by hand. Run `has-pending-model-changes` after entity or DbContext edits, as documented in README.
- After API/DTO changes, run the API and `bun run codegen` in `frontend`. It generates `types/api.generated.ts`, the only source of API types in the frontend: pages and `lib/api/*` modules import from it and declare no response types of their own. Maintain Zod form validation separately. The API clients already normalize response keys to camelCase.
- Browser authentication uses the backend's opaque session cookie. Server API calls forward it; internal service credentials must never enter browser code.
- Cache keys retain viewer and household scope. Writes invalidate affected tags; cached reads must not bypass current consent or grants.
- Fetch AI context only through `IDataAccessPolicy`. Every provider call must reserve and settle through `IAiQuotaService`, including failures. Read [AI](docs/AI.md) before changing consent, tools, prompts, or usage limits.
- Use `IStorageService` for uploads and the transactional outbox for durable background work. Read [MCP](docs/MCP.md) or [Telegram](docs/TELEGRAM.md) before changing their service boundaries.

## Verification and delivery

- Test observable behavior and public contracts. Write one integration test per user story, named for the story, through the API. Unit-test Domain rules with tables of cases. Do not write a test that restates an implementation, and delete a test when a story test covers its behavior. Use PostgreSQL for SQL, migrations, locking, and transactions; an InMemory provider cannot verify them.
- Security defaults fail safe. A command with a secret-bearing property is redacted in the audit log unless a test proves it is not, and every controller is classified for impersonation. Tests enforce both by reflection.
- Backend integration tests truncate tables. `TEST_DB_CONNECTION` must point to a disposable database with `test` in its name. Prefer the isolated Compose test service.
- Frontend fixture tests run real Next.js against a synthetic API typed from `types/api.generated.ts`. Every spec asserts the fixture saw no unhandled call. They verify browser behavior; backend and live external-service checks remain separate. Use a browser for UI changes, including keyboard and narrow-screen behavior.
- Run the relevant checks from README. Do not hide errors, log secrets, or commit without explicit authorization.
- Keep documentation concise: update the retained setup, architecture, design, or integration guide when behavior changes. Avoid persistent delivery reports, duplicated catalogues, and historical task plans.
