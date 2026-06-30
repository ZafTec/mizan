# Paddle Billing Integration Plan

**Status:** Backend complete (Phases 1-5) + webhook e2e verified + Phase 6 frontend core done. Remaining: real Paddle-delivered webhook test, backend tests, entitlement-aware UI hiding, production go-live.

Webhook e2e (2026-06-30): self-signed HMAC payload through the real backend + DB proved valid→200+row, duplicate→idempotent, bad-sig→401; test rows cleaned up. Real Paddle→ngrok delivery still pending (sandbox MCP was disconnected).

Phase 6 done: `@paddle/paddle-js`, `lib/paddle.ts`, `/billing` page (checkout + post-purchase polling + `?checkout=` auto-open), `useSubscription` hook, Billing nav link, signup `?plan=` → `/login?callbackUrl=/billing?checkout=<plan>`. api types regenerated; tsc clean.

- Phase 1 sandbox catalog: done.
- Phase 2 data model: done, migration applied to shared prod DB (`alpha.euaell.me`) on 2026-06-29 (also applied the previously-pending household-invitations migration).
- Phase 3 webhook ingestion: `POST /api/webhooks/paddle`, HMAC verify, idempotent, subscription upsert + lifetime detection.
- Phase 4 entitlements: `IEntitlementService` (HybridCache) + `GET /api/Subscriptions/me`.
- Phase 5 enforcement: policy `RequirePro` on `POST /api/Nutrition/ai/chat`, `POST /api/Nutrition/ai/analyze-image`, `GET /api/Goals/history`, `GET /api/Goals/progress`. Quota throws `ForbiddenAccessException` (403) in `CreateMealPlanCommand` (free=1), `CreateShoppingListCommand` (free=1), `InviteHouseholdMemberCommand` (free=0; Pro capped at 6 members). Trainer-chat gating deferred per decision.

Still pending before go-live: create the Paddle webhook destination (ngrok for sandbox) + set `Paddle__WebhookSecret`; backend tests; frontend.
**Decisions locked:** Sandbox-first build; backend-enforced entitlements; 7-day card-required trial on Pro; free caps = 1 meal plan / 1 shopping list / 0 household invites; downgrade keeps read, blocks create, never deletes; 3-day past_due grace.
**Last updated:** 2026-06-29

## Sandbox catalog (created)

| Item | Paddle ID |
|------|-----------|
| Product: Mizan Pro | `pro_01kwa2qsdqeyvbhbweh9x0kd6b` |
| Product: Mizan Lifetime | `pro_01kwa2qsy579p9bbpd0r5hfw1t` |
| Price: Pro Monthly ($1.99/mo, 7-day trial) | `pri_01kwa2qsjb9zryjeynbb7se47p` |
| Price: Pro Yearly ($15/yr, 7-day trial) | `pri_01kwa2qsqm28sf7z6hpenc7jrd` |
| Price: Lifetime ($48 one-time) | `pri_01kwa2qt2c0q0ntgp0ntrn3tbt` |

Trial config on both Pro prices: `{ interval: "day", frequency: 7, requires_payment_method: true }` (card captured at checkout, auto-converts to paid on day 7). Tax category `standard`; switch to `saas` in production once approved.

---

## 1. Readiness verdict

| Area | State |
|------|-------|
| Paddle account verification | Done (seller verified, MCP live and working) |
| Paddle catalog (products/prices) | Empty: 0 products, 0 prices |
| Paddle webhook destinations | Count reports 2, list returns empty: verify in dashboard before creating |
| App billing surface | None exists |
| Entitlement / feature gating | None exists (gating is role-based only) |

**Bottom line:** this is a build, not a configuration finish. Wiring Paddle checkout is the easy ~20%. The bulk is the entitlement layer, because today a free user can already use every feature the pricing page sells as Pro.

---

## 2. Pricing model (from `PricingSection.tsx`)

| Tier | Price | Billing type | Paddle shape |
|------|-------|--------------|--------------|
| Free | $0 | none | not in Paddle |
| Pro | $1.99 / month, $15 / year | recurring | one product, two recurring prices |
| Lifetime | $48 | one-time | one product, one one-time price |

Two billing types means two webhook code paths: Pro is driven by `subscription.*` events, Lifetime by `transaction.completed` (no subscription object). The entitlement model treats "lifetime = permanent Pro" as distinct from "active subscription."

---

## 3. Target architecture

```
Browser (Next.js)
  - /billing page: Paddle.js overlay checkout
  - passes customData: { userId } into checkout
  - reads GET /api/Subscriptions/me for display only (never trusts it for enforcement)
        |
        v  (Paddle-hosted checkout)
Paddle (sandbox -> production)
        |
        v  webhook POST (signed)
Backend (.NET, always-on)
  - WebhooksController [AllowAnonymous], HMAC-verified raw body
  - MediatR command upserts Subscription row (EF Core, backend-owned)
  - invalidates entitlement cache for that user
        |
        v
Entitlement enforcement (every gated /api/* call)
  - IEntitlementService (HybridCache-backed, mirrors IUserStatusService)
  - feature gates: [Authorize(Policy="RequirePro")] on controllers/actions
  - quota gates: explicit IEntitlementService check inside command handlers,
    throw ForbiddenAccessException (already mapped to 403)
```

**Why backend owns subscription state:** the backend validates every JWT and is the single enforcement point for `/api/*`. Drizzle/BetterAuth owns auth only and is read-only from the backend's perspective. Putting entitlements in EF Core keeps enforcement co-located with the data and avoids a second write-owner on the auth schema.

---

## 4. Paddle catalog spec (create in SANDBOX first)

The connected MCP is **live-only**, so it cannot build the sandbox catalog. Use a sandbox API key with the script in Appendix A, or create these by hand in the sandbox dashboard.

**Product: Mizan Pro**
- `name`: "Mizan Pro"
- `tax_category`: `standard` (confirm correct category for digital SaaS in your jurisdiction)
- Prices:
  - Pro Monthly: `unit_price` `{ amount: "199", currency_code: "USD" }`, `billing_cycle` `{ interval: "month", frequency: 1 }`
  - Pro Yearly: `unit_price` `{ amount: "1500", currency_code: "USD" }`, `billing_cycle` `{ interval: "year", frequency: 1 }`

**Product: Mizan Lifetime**
- `name`: "Mizan Lifetime"
- Prices:
  - Lifetime: `unit_price` `{ amount: "4800", currency_code: "USD" }`, no `billing_cycle` (one-time)

> Amounts are in the lowest denomination (cents). $1.99 = "199", $15 = "1500", $48 = "4800".

Record the resulting `pri_...` ids; they become frontend env vars.

**Webhook destination (notification setting):** one HTTPS destination pointing at `https://api.mizan.euaell.me/api/webhooks/paddle`, subscribed to:
`subscription.created`, `subscription.activated`, `subscription.updated`, `subscription.canceled`, `subscription.past_due`, `subscription.paused`, `subscription.resumed`, `transaction.completed`.
Save the signing secret as `Paddle__WebhookSecret`.

---

## 5. Data model (backend, EF Core)

New entity `Subscription` (backend-owned, writable). Keyed by `UserId` (the Mizan user Guid from the `sub` claim). `User.cs` stays read-only; do not add a navigation that EF would try to migrate onto the users table beyond a shadow FK.

```csharp
// Mizan.Domain/Entities/Subscription.cs
public class Subscription
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }                 // Mizan user (sub claim)
    public string Plan { get; set; } = "free";       // free | pro | lifetime
    public string Status { get; set; } = "none";     // none | trialing | active | past_due | paused | canceled
    public bool IsLifetime { get; set; }             // one-time purchase, permanent Pro

    public string? PaddleCustomerId { get; set; }     // ctm_...
    public string? PaddleSubscriptionId { get; set; } // sub_... (null for lifetime)
    public string? PaddlePriceId { get; set; }        // pri_...

    public DateTime? CurrentPeriodEnd { get; set; }   // renewal / access expiry for recurring
    public DateTime? CanceledAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

- Add `DbSet<Subscription> Subscriptions` to `IMizanDbContext` and `MizanDbContext`.
- Configure unique index on `UserId` (one active subscription record per user; supersede on plan change).
- Migration: `dotnet ef migrations add AddSubscriptions --project Mizan.Infrastructure --startup-project Mizan.Api`. This is a backend-owned table, so Drizzle must NOT see it. Do not mirror it into `frontend/db/schema.ts`.

---

## 6. Webhook handling

**Endpoint:** `POST /api/webhooks/paddle` in a new `WebhooksController`, decorated `[AllowAnonymous]` (Paddle calls without a JWT). It must read the **raw** request body (buffer it) because signature verification runs over the exact bytes.

**Signature verification (no SDK dependency needed):**
1. Read header `Paddle-Signature`, format `ts=<unix>;h1=<hex>`.
2. Build signed payload string `"{ts}:{rawBody}"`.
3. Compute `HMAC-SHA256(Paddle__WebhookSecret, signedPayload)`, hex-encode.
4. Constant-time compare against `h1`. Reject 400 on mismatch.
5. Reject if `ts` is older than a tolerance (default 5s, make configurable) to block replay.

**Event -> state mapping** (dispatched as a MediatR `ProcessPaddleWebhookCommand`):

| Event | Action |
|-------|--------|
| `subscription.created` / `activated` / `resumed` | upsert: plan=pro, status=active, store sub/customer/price ids, set CurrentPeriodEnd |
| `subscription.trialing` | plan=pro, status=trialing |
| `subscription.updated` | refresh status, price, CurrentPeriodEnd |
| `subscription.past_due` | status=past_due (entitlement keeps Pro through grace window, see section 7) |
| `subscription.paused` | status=paused |
| `subscription.canceled` | status=canceled; Pro access until CurrentPeriodEnd, then free |
| `transaction.completed` where price is the Lifetime price | plan=lifetime, IsLifetime=true, status=active |

**Idempotency:** webhooks can be redelivered. Key dedup on Paddle's `event_id`; ignore already-processed events. Map customer -> Mizan user via `custom_data.userId` on first event, then via `PaddleCustomerId` thereafter.

**After any state write:** invalidate the user's entitlement cache entry (see section 7).

---

## 7. Entitlement resolution and enforcement

### Resolution service (mirror `IUserStatusService`)

`IEntitlementService` with `Task<Entitlement> GetAsync(Guid userId, CancellationToken)`, HybridCache-backed (same pattern already used for user ban/verify status), short TTL with explicit invalidation on webhook writes.

```
Entitlement {
  string Plan;          // free | pro | lifetime
  bool IsPro;           // true if lifetime, or active/trialing, or past_due within grace
  DateTime? AccessUntil;
}
```

Resolution rules, in order:
1. `IsLifetime` -> Pro, permanent.
2. status `active` or `trialing` -> Pro.
3. status `past_due` and `now < CurrentPeriodEnd + graceDays` -> Pro (grace).
4. status `canceled` and `now < CurrentPeriodEnd` -> Pro until period end.
5. otherwise -> Free.

### Enforcement is two-tier

**Tier 1, feature gates (whole capability is Pro-only):** add a policy in `Program.cs`:
```csharp
options.AddPolicy("RequirePro", policy => policy
    .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, ApiKeyAuthenticationSchemeOptions.DefaultScheme)
    .RequireAuthenticatedUser()
    .AddRequirements(new ProRequirement()));
```
with an `IAuthorizationHandler` that calls `IEntitlementService`. Apply `[Authorize(Policy="RequirePro")]` to the AI controller, trainer-chat actions, and advanced-analytics endpoints.

**Tier 2, quota gates (free is allowed but capped):** enforce inside the relevant command handler, throw `ForbiddenAccessException` (already mapped to 403) when over the free cap. Example points:
- `CreateMealPlanCommand`: free capped at 1 meal plan.
- `CreateShoppingListCommand`: free capped at 1 shopping list.
- `InviteToHouseholdCommand` (HouseholdsController): free capped at solo / Pro allows up to 6 members.

Tier 2 cannot be a blanket attribute because the limit depends on current row counts, which is exactly the kind of check the project keeps explicit in handlers rather than hidden in middleware.

### Feature -> enforcement matrix (PROPOSED, needs your sign-off)

| Marketed as | Tier | Enforcement point | Free limit |
|-------------|------|-------------------|------------|
| Meal logging | Free | none | unlimited |
| Recipe browser | Free | none | unlimited |
| Achievements / streaks | Free | none | unlimited |
| Meal plans | quota | `CreateMealPlanCommand` | 1 |
| Shopping lists | quota | `CreateShoppingListCommand` | 1 |
| Household invitations | quota | `InviteToHouseholdCommand` | 0 (Pro: up to 6) |
| AI coach + food-image analysis | feature | AI controller `[Authorize(Policy="RequirePro")]` | blocked |
| Trainer + client chat/goals | feature | trainer endpoints + ChatHub | blocked |
| Advanced analytics / trends | feature | analytics endpoints | blocked |

Open question: do you want a hard wall on existing data when someone downgrades (e.g. they had 3 meal plans on Pro, then lapse)? Recommended: read stays allowed, **create** is blocked while over cap. Don't delete user data on downgrade.

---

## 8. Frontend work

1. Add dependency `@paddle/paddle-js`.
2. Initialize Paddle.js once (client) with `NEXT_PUBLIC_PADDLE_CLIENT_TOKEN` and `NEXT_PUBLIC_PADDLE_ENV` (`sandbox` | `production`).
3. New `/billing` page under `(dashboard)`:
   - shows current plan from `GET /api/Subscriptions/me`,
   - "Upgrade" buttons call `Paddle.Checkout.open({ items: [{ priceId }], customData: { userId }, customer: { email } })`,
   - success callback routes to a thank-you / refresh state.
4. Pricing CTAs (`PricingSection.tsx`):
   - logged-out -> `/register?plan=pro` (capture below),
   - logged-in -> open checkout directly.
5. Register `?plan=` capture: today the param is ignored. Persist it (cookie or query passthrough) and after first login redirect to `/billing?checkout=<plan>` to open checkout.
6. Entitlement-aware UI: hide/disable Pro features for free users using the same `me` payload. This is presentation only. The backend is the source of truth; never gate solely in the client.
7. Price ids come from env (`NEXT_PUBLIC_PADDLE_PRICE_PRO_MONTHLY`, `_PRO_YEARLY`, `_LIFETIME`) so sandbox vs production swap is config, not code.

---

## 9. Environment variables to add

**Backend (`.env`, appsettings):**
```
Paddle__Environment=sandbox            # sandbox | production
Paddle__ApiKey=...                     # server-side API key (sandbox key first)
Paddle__WebhookSecret=...              # from the notification destination
Paddle__GraceDays=3                    # past_due grace window
```

**Frontend (`.env.local`):**
```
NEXT_PUBLIC_PADDLE_ENV=sandbox
NEXT_PUBLIC_PADDLE_CLIENT_TOKEN=...    # client-side token (sandbox)
# sandbox price ids (created Phase 1):
NEXT_PUBLIC_PADDLE_PRICE_PRO_MONTHLY=pri_01kwa2qsjb9zryjeynbb7se47p
NEXT_PUBLIC_PADDLE_PRICE_PRO_YEARLY=pri_01kwa2qsqm28sf7z6hpenc7jrd
NEXT_PUBLIC_PADDLE_PRICE_LIFETIME=pri_01kwa2qt2c0q0ntgp0ntrn3tbt
```
Add all of the above to `.env.example` with placeholder values.

---

## 10. Build sequence (each phase independently testable)

1. **Catalog + sandbox config** (no app code): create sandbox products/prices, webhook destination, capture ids + secrets.
2. **Data model:** `Subscription` entity, `IMizanDbContext`/`MizanDbContext`, migration.
3. **Webhook ingestion:** `WebhooksController` + signature verify + `ProcessPaddleWebhookCommand`. Test with Paddle's "send test event" and `paddle.notifications.replay` against a tunneled local endpoint.
4. **Entitlement read path:** `IEntitlementService` + `GET /api/Subscriptions/me`.
5. **Enforcement:** `RequirePro` policy + handler (tier 1), then quota checks in the three command handlers (tier 2).
6. **Frontend:** Paddle.js init, `/billing`, pricing CTA wiring, register `?plan` capture, entitlement-aware UI.
7. **Codegen:** `bun run codegen` after the new DTOs land.
8. **Promote to production:** swap env to production key/token/price ids, point webhook at the live destination, smoke-test one real purchase + refund.

---

## 11. Testing plan

- **Webhook signature:** unit test valid/invalid/expired-ts/replay.
- **Event mapping:** integration test each event -> expected Subscription row (Testcontainers Postgres, the project's existing pattern).
- **Entitlement resolution:** unit tests for each rule (lifetime, active, trialing, past_due grace boundary, canceled-until-period-end, free).
- **Enforcement:** integration test that a free JWT gets 403 on a Pro endpoint and on the 2nd meal plan; a Pro JWT succeeds.
- **E2E (Playwright):** sandbox checkout with Paddle test card, assert UI flips to Pro after webhook. Use Paddle's sandbox test card numbers.

---

## 12. Decisions (resolved)

1. **Trial:** Yes, 7-day trial on both Pro prices, card required (`requires_payment_method: true`), auto-converts to paid on day 7. Entitlement during `trialing` = Pro.
2. **Free-tier caps:** 1 meal plan, 1 shopping list, 0 household invites. Confirmed.
3. **Downgrade behavior:** read keeps working, create blocks while over cap, never delete user data. Confirmed.
4. **Grace window:** 3 days for `past_due` before downgrade. Confirmed (`Paddle__GraceDays=3`).

### Trial infrastructure implications
- Event flow on signup: `subscription.created` -> `subscription.trialing` -> (day 7) `subscription.activated` on successful charge, or `subscription.past_due` if the captured card fails at conversion.
- `IEntitlementService` treats `trialing` as full Pro. No separate trial gating needed.
- Optional polish (not required for v1): surface "trial ends in N days" in the UI from `CurrentPeriodEnd`, and dunning email on `subscription.past_due`. Paddle sends its own trial-ending and payment-failed emails by default, so this is additive.
- Still open (minor, UI only): which Pro price the billing page highlights by default (monthly vs yearly).

---

## Appendix A: sandbox catalog creation script

Run with a **sandbox** API key (the live MCP cannot touch sandbox). Requires `@paddle/paddle-node-sdk`.

```js
// scripts/paddle-sandbox-seed.mjs
import { Paddle, Environment } from '@paddle/paddle-node-sdk';
const paddle = new Paddle(process.env.PADDLE_SANDBOX_API_KEY, { environment: Environment.sandbox });

const pro = await paddle.products.create({ name: 'Mizan Pro', taxCategory: 'standard' });
const proMonthly = await paddle.prices.create({
  productId: pro.id, description: 'Pro Monthly',
  unitPrice: { amount: '199', currencyCode: 'USD' },
  billingCycle: { interval: 'month', frequency: 1 },
});
const proYearly = await paddle.prices.create({
  productId: pro.id, description: 'Pro Yearly',
  unitPrice: { amount: '1500', currencyCode: 'USD' },
  billingCycle: { interval: 'year', frequency: 1 },
});

const lifetime = await paddle.products.create({ name: 'Mizan Lifetime', taxCategory: 'standard' });
const lifetimePrice = await paddle.prices.create({
  productId: lifetime.id, description: 'Lifetime',
  unitPrice: { amount: '4800', currencyCode: 'USD' },
});

console.log({ proMonthly: proMonthly.id, proYearly: proYearly.id, lifetime: lifetimePrice.id });
```
