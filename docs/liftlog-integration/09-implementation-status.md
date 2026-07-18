# LiftLog Integration Implementation Status

**Completed:** 2026-07-18  
**Branch:** `feature/liftlog-integration`

## Product decisions applied

- Workout summaries in the social feed read live workout data.
- Feed items have no automatic retention expiry.
- Workout and social features are available on the free plan.
- Reactions are fixed to `👍 ❤️ 💪 🔥 👏 🎉 🏆`.
- Handles and public discovery search are deferred.
- MCP query-string tokens remain supported.
- Nutrition AI is not exposed through MCP.
- Free users receive 15 successful MCP tool calls per calendar month.
- Admin MCP calls use a separate admin service key.

## Delivered scope

### Security and platform hardening

- Sensitive MCP-token and chat commands are excluded from audit serialization, with historical audit cleanup in the migration.
- JWT is the default authentication path; MCP service authentication is explicitly scoped.
- Constant-time service-key comparison, verified-user checks, and a separate admin MCP service key are implemented.
- JWT issuer, audience, and JWKS configuration fail closed.
- JWKS uses a refreshed last-known-good in-memory snapshot.
- AI image uploads, pagination, recipe access, chat membership, and token validation rate limits are bounded.
- API errors expose stable `errorCode` values.
- Frontend, backend, MCP, and test dependencies were upgraded to current compatible versions.

### Workout and exercise system

- Exercise catalog migration with 100+ strength, cardio, flexibility, and balance exercises.
- Starting Strength, StrongLifts 5x5, and Push Pull Legs built-in templates.
- Workout create, read, update, delete, history, stats, duplicate, next-session, and draft APIs.
- Template create, update, delete, duplicate, and progression APIs.
- Pure progression logic for even and lowest-set progression.
- Per-set reps, weight, time, distance, resistance, incline, steps, completion, and completion timestamps.
- Tap-to-cycle logging, scoped weight editing, rest timing, draft persistence, finish summaries, history, and stats UI.
- Custom exercise creation and admin promotion/deletion.
- Trainer client-workout reads.

### Notifications and social

- Notification storage, writer, list/read endpoints, bell badge, and inbox.
- Opt-in social profiles with private share tokens.
- Link-based follow requests, acceptance, rejection, and revocation.
- Accepted-follow-only feed authorization.
- Live workout feed cards, reactions, comments, reports, and comment soft deletion.
- Public share-link route without handle discovery.
- Social analytics and moderation queue.
- Per-user social write rate limits.

### Gamification and admin

- Workout, volume, template, personal-record, follower, share, reaction, and comment criteria.
- Local-date workout streaks and streak freezes.
- Achievement evaluator triggers for workout and social actions.
- Admin moderation, exercise management, template management, social analytics, and achievement analytics.
- MCP achievement get/create/update/delete/analytics controls.

### MCP

- Workout CRUD, history, stats, drafts, templates, progression, and exercises.
- Social profiles, follows, feed, reactions, comments, reports, and notifications.
- Subscription lookup and existing nutrition, recipe, meal, shopping, goal, household, trainer, profile, and measurement features.
- Admin social analytics, reports, audit logs, exercise promotion, built-in templates, and achievement management.
- Structured backend error parsing with upgrade, validation, not-found, authorization, and rate-limit handling.
- Unlimited-plan token-validation cache, live quota checks for limited plans, and hashed token logging.
- Duplicate nutrition tools removed and canonical meal types retained.
- No nutrition AI tools.

## Central configuration

- Free MCP quota: `backend/Mizan.Application/Common/McpUsagePolicy.cs` (`FreeMonthlyToolCalls`).
- Pricing copy: `frontend/components/Landing/PricingSection.tsx`.
- MCP validation rate limit: `RateLimits:McpTokenValidation:PermitLimit` and `RateLimits:McpTokenValidation:WindowMinutes`, defaulting to 10 requests per minute.
- Service keys: `Mcp:ServiceApiKey` and `Mcp:AdminServiceApiKey`.

## Migrations

- `20260718052538_LiftLogIntegration`
- `20260718054855_AddStreakFreezes`

## Commit sequence

- `cce7e96` Add frontend test infrastructure
- `2d481b3` Harden API and expand MCP tools
- `65a8728` Build workout and social backend
- `bbeba8f` Update project dependencies
- `a467662` Stabilize MCP integration tests
- `e537d53` Test workout and social access
- `9d3c3e6` Complete workout and social UI

## Verification

| Gate | Result |
|---|---|
| `dotnet build backend/Mizan.sln --no-restore` | Passed |
| `dotnet test backend/Mizan.sln --no-restore` | 233 passed, 0 failed |
| MCP integration suite | 48 passed, 0 failed |
| Workout/social access contracts | 6 passed, 0 failed |
| `bun run --cwd frontend test` | 2 files, 4 tests passed |
| `bun run --cwd frontend lint` | Passed with 11 existing `no-img-element` warnings and no errors |
| `bun run --cwd frontend build` | Passed on Next.js 16.2.10 |
| OpenAPI type generation | Passed |
| NuGet vulnerability audit | No vulnerable direct or transitive packages |
| Docker Compose test profile | Not completed: the SDK base-image pull timed out after 15 minutes before tests started |

The successful backend suite used PostgreSQL 18 Testcontainers through Docker Desktop. This verifies migrations and database behavior even though the separate Compose image-build gate was blocked by registry throughput.

## Validation limitations

- Playwright infrastructure is installed and configured, but authenticated browser-flow tests were not added without a runnable full-stack Compose environment. API integration tests cover the workout and social authorization contracts.
- `.env.example` is protected by the editor's private-file policy and could not be updated in this run. It must include `REDIS_PASSWORD` and `MCP_ADMIN_SERVICE_KEY`; `docker-compose.yml` already requires and consumes both.

## Deployment actions

1. Back up the production database and apply both migrations.
2. Rotate existing MCP user tokens because historical audit entries may have contained plaintext values.
3. Rotate `MCP_SERVICE_KEY` and `MCP_ADMIN_SERVICE_KEY` and configure distinct values.
4. Configure `REDIS_PASSWORD`.
5. Confirm pricing copy and the centrally configured 15-call free MCP quota before release.
6. Run the Compose profile once the .NET SDK image is available locally, then execute the authenticated Playwright flows against the full stack.