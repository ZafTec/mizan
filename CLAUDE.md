# Contributor guidance

Read [README](README.md) for setup and verification, and [architecture](docs/ARCHITECTURE.md) for system boundaries. Before frontend framework changes, follow [frontend/AGENTS.md](frontend/AGENTS.md) and read the relevant installed Next.js guide.

## Product and ownership

- Mizan logs meals, workouts, and body measurements. Today, History, Progress, More, and Log entry form the navigation. Secondary features belong under More or appear when relevant to the user's data.
- ASP.NET Core owns identity, authorization, business rules, and the entire PostgreSQL schema. Next.js has no database access or independent authentication authority.
- Web, MCP, and Telegram use the same backend commands and permissions. Keep transport adapters thin. Put shared request records in `backend/Mizan.Contracts`.
- Keep pure calculations in Domain/Application and external I/O in Infrastructure/API. Follow existing language and component conventions.

## Changes that cross boundaries

- Preserve `InitialCreate` and the existing additive EF migration chain. Add migrations for later model changes; never reset the baseline as a routine fix. Run `has-pending-model-changes` after entity or DbContext edits, as documented in README.
- After API/DTO changes, run the API and `bun run codegen` in `frontend`. It generates `types/api.generated.ts`; maintain Zod form validation separately. The API clients already normalize response keys to camelCase.
- Browser authentication uses the backend's opaque session cookie. Server API calls forward it; internal service credentials must never enter browser code.
- Cache keys retain viewer and household scope. Writes invalidate affected tags; cached reads must not bypass current consent or grants.
- Fetch AI context only through `IDataAccessPolicy`. Every provider call must reserve and settle through `IAiQuotaService`, including failures. Read [AI](docs/AI.md) before changing consent, tools, prompts, or usage limits.
- Use `IStorageService` for uploads and the transactional outbox for durable background work. Read [MCP](docs/MCP.md) or [Telegram](docs/TELEGRAM.md) before changing their service boundaries.

## Verification and delivery

- Test observable behavior and public contracts. Use PostgreSQL for SQL, migrations, locking, and transactions; an InMemory provider cannot verify them.
- Backend integration tests truncate tables. `TEST_DB_CONNECTION` must point to a disposable database with `test` in its name. Prefer the isolated Compose test service.
- Frontend fixture tests run real Next.js against a synthetic API. They verify browser behavior; backend and live external-service checks remain separate. Use a browser for UI changes, including keyboard and narrow-screen behavior.
- Run the relevant checks from README. Do not hide errors, log secrets, or commit without explicit authorization.
- Keep documentation concise: update the retained setup, architecture, design, or integration guide when behavior changes. Avoid persistent delivery reports, duplicated catalogues, and historical task plans.
