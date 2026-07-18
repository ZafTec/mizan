# Security Findings

Persistent log of security review findings. Each entry records the finding, where it lives, and its status. Re-verified on the date shown; update status when fixed.

## 2026-07-18 LiftLog integration remediation

Re-verified on branch `feature/liftlog-integration`. The code findings from the June and July audits are closed. MCP bearer tokens and service keys still require operational rotation when this branch is deployed.

| ID | Severity | Resolution | Status |
|----|----------|------------|--------|
| S1 | CRITICAL | `ISkipAudit` excludes token validation and chat content; the LiftLog migration redacts existing rows | FIXED (`2d481b3`, `65a8728`) |
| S2 | HIGH | JWT is the default policy; MCP service access is explicit; key comparison is constant-time; regular service keys cannot impersonate admins; admin MCP uses a separate key | FIXED (`2d481b3`) |
| S3 | HIGH | Compose secrets fail closed, database ports bind to loopback, and Redis requires authentication | FIXED (`2d481b3`) |
| S4 | MEDIUM | Issuer, audience, and JWKS URL validate at startup; issuer and audience validation are mandatory | FIXED (`2d481b3`, `a467662`) |
| S5 | MEDIUM | AI image requests are capped at 10 MB, files at 8 MB, with a JPEG/PNG/WebP allowlist | FIXED (`2d481b3`) |
| S6 | LOW | Global and user-owned exercises are scoped; custom exercises require approval; media URLs require HTTPS | FIXED (`65a8728`, `9d3c3e6`) |
| S7 | LOW | Token validation uses a configurable IP fixed-window limit with a 10/minute production default | FIXED (`2d481b3`, `a467662`) |
| S8 | LOW | The claims-debug endpoint was removed | FIXED (`2d481b3`) |
| S9 | LOW | Meal-plan recipe attachment requires public, owner, or household access | FIXED (`2d481b3`) |
| S10 | LOW | Typing indicators re-check conversation membership before broadcasting | FIXED (`2d481b3`) |
| S11 | HIGH | Backend and MCP dependencies were upgraded; `dotnet list package --vulnerable --include-transitive` reports no vulnerable packages | FIXED (`bbeba8f`) |
| A1/A2 | HIGH availability | JWKS refresh keeps a last-known-good in-memory snapshot and signature validation reads synchronously from that snapshot | FIXED (`2d481b3`) |
| A10 | Availability | Shared pagination clamps page and page size | FIXED (`2d481b3`) |

### Deployment actions

1. Rotate all existing MCP user tokens because historical audit rows may have contained plaintext tokens.
2. Rotate `MCP_SERVICE_KEY` and `MCP_ADMIN_SERVICE_KEY` and deploy distinct values.
3. Set `REDIS_PASSWORD`; Compose no longer starts Redis without it.

## 2026-07-07 re-verification of ANALYSIS_2026-06-10 security findings

All findings below were re-checked against the working tree on 2026-07-07 (branch `master`, HEAD `d154eea`). None have been fixed since the June audit.

| ID | Severity | Finding | Location | Status 2026-07-07 |
|----|----------|---------|----------|-------------------|
| S1 | CRITICAL | Raw MCP bearer tokens (and chat message content) serialized in plaintext into `AuditLogs.Details` for every `*Command`, defeating the SHA-256 `TokenHash` design | `backend/Mizan.Application/Behaviors/AuditBehavior.cs:67` | STILL OPEN (verified: `JsonSerializer.Serialize(request)` unredacted) |
| S2 | HIGH | Service API key + `X-Impersonate-User` header mints a principal with the target user's real role (admin included) on every `[Authorize]` endpoint; non-constant-time key compare; skips email-verified check | `backend/Mizan.Api/Authentication/ApiKeyAuthenticationHandler.cs:45-83`, `backend/Mizan.Api/Program.cs:101-107` (ApiKey scheme folded into DefaultPolicy) | STILL OPEN (verified: `string.Equals(..., StringComparison.Ordinal)`, role claim from `status.Role`, DefaultPolicy includes ApiKey scheme) |
| S3 | HIGH | Weak fallback secrets in dev compose: `mizan_dev_password`, `dev_secret_change_in_production` (BetterAuth signing), `dev_mcp_key_change_in_production`; Postgres/Redis published on host | `docker-compose.yml:13,60,123,137,166,170,194` | STILL OPEN (verified: all fallbacks present) |
| S4 | MEDIUM | JWT issuer/audience validation silently self-disables when config empty | `backend/Mizan.Infrastructure/Auth/BetterAuth/JwtAuthenticationExtensions.cs:41` | STILL OPEN (verified: `ValidateIssuer = !string.IsNullOrWhiteSpace(jwt.Issuer)`) |
| S5 | MEDIUM | AI image upload unbounded: only `Length == 0` check, full buffer to memory, forwarded to OpenAI (memory DoS + spend) | `backend/Mizan.Api/Controllers/NutritionController.cs:65` | STILL OPEN (verified: no `[RequestSizeLimit]`, no content-type allowlist) |
| S6 | LOW | `CreateExercise` plain `[Authorize]` while Foods creation is admin-only; attacker-controlled `VideoUrl`/`ImageUrl` in shared catalog | `backend/Mizan.Api/Controllers/ExercisesController.cs:28` | STILL OPEN (verified) |
| S7 | LOW | `/api/McpTokens/validate` is `[AllowAnonymous]`, unthrottled token oracle | `backend/Mizan.Api/Controllers/McpTokensController.cs:72` | STILL OPEN (verified) |
| S8 | LOW | `/api/Users/me/debug` dumps full claim set in prod | `backend/Mizan.Api/Controllers/UsersController.cs` | STILL OPEN (endpoint still present) |
| S9 | LOW | `AddRecipeToMealPlanCommand` doesn't verify recipe access; private recipe metadata leaks via meal-plan read | `backend/Mizan.Application/Commands/AddRecipeToMealPlanCommand.cs` | NOT RE-VERIFIED (assume open) |
| S10 | LOW | `ChatHub.TypingIndicator` broadcasts without membership re-check | `backend/Mizan.Api/Hubs/ChatHub.cs` | NOT RE-VERIFIED (assume open) |

Related availability/correctness items re-verified as still open: A1 JWKS failure cached (`JwksProvider.cs:71` returns `""`), A2 sync-over-async in JWT hot path (`EdDsaJwtSignatureValidator.cs:27`), A10 no pagination clamping in `QueryableExtensions` (only `GetAchievementAnalyticsQuery` clamps locally).

## 2026-07-07 new findings

| ID | Severity | Finding | Location | Status |
|----|----------|---------|----------|--------|
| S11 | HIGH (dependency) | NuGet audit: MessagePack 2.5.187 has multiple known HIGH advisories (e.g. GHSA-hv8m-jj95-wg3x, GHSA-vh6j-jc39-fggf); Microsoft.OpenApi 2.4.1 HIGH (GHSA-v5pm-xwqc-g5wc); OpenTelemetry 1.15.2 moderate | `backend/Mizan.Api` package graph (warnings printed on every Docker build) | OPEN: bump packages |

Remediation plan and sequencing: see `docs/liftlog-integration/01-security-remediation.md`.
