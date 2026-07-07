# Security Remediation Plan

**Re-verified:** 2026-07-07 against `master` @ `d154eea`. Source audits: `docs/ANALYSIS_2026-06-10.md` §1-2, cross-checked file-by-file today. **None of the June security findings have been fixed.** The canonical status table lives in `SECURITY-FINDINGS.md` (repo root); this file is the remediation spec for the implementing agent.

All of these must land BEFORE the LiftLog feature work in this folder, because the feature work adds new endpoints, a social surface, and new user-generated content that would inherit these weaknesses (S2 especially: every new `[Authorize]` endpoint is impersonable; S6 is the exact pattern the new exercise catalog will extend).

---

## Batch 1: CRITICAL/HIGH (do first, in this order)

### R1. Redact sensitive commands from the audit log (S1, CRITICAL)

`Mizan.Application/Behaviors/AuditBehavior.cs:67` does `Details = JsonSerializer.Serialize(request)` for every `*Command`. `ValidateTokenCommand` carries the raw MCP bearer token; `SendChatMessageCommand` carries private message content.

Implementation:
1. Add marker interface `ISkipAudit` in `Mizan.Application/Interfaces/` (empty interface).
2. In `AuditBehavior.Handle`, after the `EndsWith("Command")` check: `if (request is ISkipAudit) return response;` (skip only the log write, still execute).
3. Mark `ValidateTokenCommand` and `SendChatMessageCommand` with `ISkipAudit`.
4. Data cleanup migration/script: `UPDATE audit_logs SET details = '{"redacted":true}' WHERE action IN ('ValidateTokenCommand','SendChatMessageCommand');` — existing rows hold live secrets.
5. Rotate all MCP tokens after deploy (they are burned; every validation since the feature shipped wrote the raw token to the DB).
6. Test: unit test asserting no `AuditLog` row is created for an `ISkipAudit` command; grep test that `ValidateTokenCommand` implements it.

Optional hardening (second pass): `[SensitiveData]` attribute + a redacting `JsonConverter` for commands that should be audited but have one secret field.

### R2. Scope the service-key impersonation (S2, HIGH)

`Mizan.Api/Authentication/ApiKeyAuthenticationHandler.cs` + `Program.cs:101-107`: the ApiKey scheme is in `DefaultPolicy`, so the static `Mcp:ServiceApiKey` + `X-Impersonate-User: <guid>` header works on EVERY `[Authorize]` endpoint and mints the target's real role (admin included).

Implementation:
1. Remove `ApiKeyAuthenticationSchemeOptions.DefaultScheme` from `DefaultPolicy` in `Program.cs`. Add a named policy `"McpService"` that requires the ApiKey scheme.
2. Apply `[Authorize(Policy = "McpService")]` ONLY to the endpoints the MCP server actually calls (enumerate from `Mizan.Mcp.Server`'s HTTP client usage; they are the tool-backing endpoints).
3. In the handler: replace `string.Equals(extractedApiKey, Options.ApiKey, StringComparison.Ordinal)` with `CryptographicOperations.FixedTimeEquals` over UTF-8 bytes.
4. Refuse impersonation when `status.Role == "admin"` (log + fail).
5. Require email-verified for impersonated principals (JWT path enforces it; this path skips it). `IUserStatusService` may need an `EmailVerified` field.
6. Rotate `Mcp:ServiceApiKey` in all environments after deploy.
7. Tests: integration test that a business endpoint (e.g. `GET /api/Recipes`) returns 401 for ApiKey auth; that an MCP endpoint accepts it; that impersonating an admin fails.

### R3. Remove weak fallback secrets from docker-compose (S3, HIGH)

`docker-compose.yml` lines 13, 60, 123, 137, 166, 170, 194.

Implementation: replace `${VAR:-weak_default}` with `${VAR:?VAR is required}` for `DB_PASSWORD`, `BETTER_AUTH_SECRET`, `MCP_SERVICE_KEY` (compose fails fast, matching `Program.cs:97-98` behavior). Bind dev-only published ports to loopback: `"127.0.0.1:5432:5432"`, `"127.0.0.1:6379:6379"`. Add `--requirepass ${REDIS_PASSWORD:?}` to the redis command and thread it into both connection strings. Update `.env.example` accordingly. Verify `docker-compose.prod.yml` stays clean (it was already).

### R4. Fail closed on JWKS errors; kill the sync-over-async validator (A1 + A2, HIGH availability)

- `JwksProvider.cs:60-72`: the catch-all returns `""` which HybridCache caches, producing platform-wide 401s for the TTL after one blip. Throw instead (HybridCache does not cache failures), and keep a last-known-good in-memory snapshot to serve stale keys while refresh fails.
- `EdDsaJwtSignatureValidator.cs:27`: `.GetAwaiter().GetResult()` over Redis+HTTP per request on cache miss. Move key resolution to an async path: a hosted service that pre-warms and refreshes the key snapshot, with the validator reading a volatile in-memory field only.
- Tests: kill the JWKS endpoint mid-test-run; assert requests keep authenticating from the stale snapshot.

## Batch 2: MEDIUM

### R5. Mandatory JWT issuer/audience validation (S4)
`JwtAuthenticationExtensions.cs:41-44`: set `ValidateIssuer = true`, `ValidateAudience = true` unconditionally and validate `JwtOptions` at startup (`ValidateOnStart`), throwing when `Issuer`/`Audience` are empty.

### R6. Bound the AI image upload (S5)
`NutritionController.cs` analyze-image action: `[RequestSizeLimit(10_000_000)]`, content-type allowlist (`image/jpeg`, `image/png`, `image/webp`), reject before buffering when `image.Length > 8MB`. Frontend spec (deep-dive §3 Spec 1 Photo tab) already plans client-side downscale; the server guard is the real control.

### R7. Pagination clamping (A10)
`QueryableExtensions.cs`: `pageSize = Math.Clamp(pageSize, 1, 100); page = Math.Max(1, page);`. Remove the now-redundant local clamp in `GetAchievementAnalyticsQuery.cs:131-132` or leave it; either way the extension is the enforcement point. This becomes more important with the social feed (public, enumerable surface).

### R8. Bump vulnerable NuGet packages (S11, HIGH dependency)
Every Docker build prints NU1902/NU1903 warnings: MessagePack 2.5.187 (multiple HIGH, incl. GHSA-hv8m-jj95-wg3x), Microsoft.OpenApi 2.4.1 (HIGH GHSA-v5pm-xwqc-g5wc), OpenTelemetry 1.15.2 (moderate). Bump to patched versions, run the full suite, and consider `<WarningsAsErrors>NU1903</WarningsAsErrors>` so HIGH advisories fail the build.

## Batch 3: LOW (bundle into one PR)

- **S6:** Scope `GetExercises` to `IsCustom == false || CreatedByUserId == currentUser` (this is also required by the LiftLog-derived exercise catalog design in `04-feature-mapping.md`, which makes user exercises first-class). Validate `VideoUrl`/`ImageUrl` as https URLs.
- **S7:** IP rate limit on `POST /api/McpTokens/validate` (ASP.NET `AddRateLimiter`, fixed window, keyed by IP). LiftLog's backend has a worked `RateLimitService` example.
- **S8:** Delete `/api/Users/me/debug` or wrap in `#if DEBUG` / `env.IsDevelopment()` gate.
- **S9:** In `AddRecipeToMealPlanCommand`, require the recipe be public, owned, or household-visible before attaching.
- **S10:** Re-check conversation membership in `ChatHub.TypingIndicator`.

## New attack surface introduced by the LiftLog work (design-time requirements)

The feature plan in this folder adds a social feed, user-created exercises, and admin moderation. Constraints the implementing agent must carry:

1. **Feed privacy is allowlist-only.** Follow-request/approve flow (LiftLog model) — no public-by-default profiles. Feed read queries must filter by an accepted-follow join, enforced server-side in the query handler, not the controller.
2. **All user-generated strings rendered in other users' UIs** (workout names, exercise names, profile display names, comments) are plain text — never rendered as HTML; enforce length caps in FluentValidation (name <= 100, notes <= 500).
3. **No IDOR regressions:** every new query handler takes `_currentUserService.UserId` and scopes by ownership or accepted-follow, same pattern as existing handlers (audit found existing handlers consistently clean — keep it that way).
4. **Reaction/comment write endpoints rate-limited** per user (spam + harassment control), and every social row carries `CreatedByUserId` for moderation.
5. **Admin moderation endpoints are `[Authorize(Roles = "admin")]`** via the JWT path only — explicitly NOT reachable via the ApiKey scheme (falls out of R2, verify with a test).
6. Workout gamification triggers must validate payloads (existing G3: empty-exercise workouts still tick streaks/achievements — fix `RuleFor(Exercises).NotEmpty()` before social visibility makes farming attractive).
