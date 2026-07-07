# Current State: Combined Audit Synthesis

**Date:** 2026-07-07. This merges `docs/ANALYSIS_2026-06-10.md` (security, anti-patterns, data flow, UX, engagement) and `docs/DEEP_DIVE_2026-06-12.md` (feature audit, workout rebuild spec, engagement specs, visual overhaul) into one prioritized picture, re-verified where it matters for the LiftLog integration. Read the originals for full per-finding evidence; this file is the working summary the implementing agent should keep open.

## What changed since the audits (June 12 → July 7)

Git history since the audits is dominated by the Paddle billing integration (branch `paddle.integration`, now on master: billing tables migration `20260629163114_AddBillingTables`, pro entitlements, AI chat gated behind pro). Relevant deltas:

- **AI chat is now pro-gated with an upsell** (eec7035) — partially addresses the June "AI ungated" note (E13); billing exists now, so "Pro" is no longer fiction.
- **`GetAchievementAnalyticsQuery` exists** (new since the audit) — there is now an admin-side achievement analytics query; the gamification/admin integration below should extend it, not duplicate it.
- **The meal-type fix landed** (DEEP_DIVE §3 Spec 1 step 0): `Mizan.Domain/Constants/MealTypes.cs` now defines BREAKFAST/LUNCH/DINNER/SNACK/DRINK/MEAL and the diary/log-food validators accept them. Two stale tests (`CreateFoodDiaryEntryCommandTests.Validator_ShouldFail_WhenInvalidMealType`, `LogFoodCommandTests.Validator_ShouldFail_WhenInvalidMealType`) still assert the old 3-type behavior and are the only failures in the backend suite — update them.
- **Nothing in the workout, exercise, social, notification, household, or trainer areas changed.** All DEEP_DIVE findings in those areas were spot-checked and stand.
- **All 10 security findings stand** (see `SECURITY-FINDINGS.md`).
- **CLAUDE.md drift:** claims `bun run test` (Vitest) and `bun run test:e2e` (Playwright) exist — the frontend `package.json` has NO test script and no test framework installed. Also claims `codegen:types`/`codegen:zod` split — only a single `codegen` script exists. Also says JWT is ES256 — implementation is EdDSA/Ed25519. Fix CLAUDE.md when touching it.

## Feature status matrix (from DEEP_DIVE, still accurate)

| Feature | Status | Core problem |
|---|---|---|
| **Workouts** | BROKEN | Zero seeded exercises; logging impossible on fresh install; data-mangling form (one set value cloned N times, notes destroyed, cardio duration multiplied); no edit/delete endpoints |
| Shopping lists | BROKEN | Page crashes on data shape mismatch; no create UI; "generate from plan" doesn't exist |
| Households | BROKEN | Membership works; zero actual sharing behind the sharing claims |
| Trainers | BROKEN | Role unassignable via UI; dashboard shows hardcoded fake data; 3 links 404 |
| AI suggestions | BROKEN | Model fabricates recipe IDs → every card 404s |
| Meal plans / Goals / AI chat / Habits / Admin | PARTIAL | Connective tissue missing (see original) |
| Body measurements / **Achievements** / Profile | WORKS | Achievements is the best-audited feature — the LiftLog work builds ON it |
| Community / Notifications | STUB | 3 live features promise notifications that don't exist |

**Systemic diagnosis (unchanged):** missing connective tissue. Plan doesn't feed diary, diary doesn't feed goal progress, households affect no query, suggestions can't be acted on, four routes have zero inbound links.

## Why this matters for the LiftLog integration

The user's directive: rebuild workouts/exercises by sampling LiftLog, bring in its social features, integrate with the existing achievement gamification, and extend the admin area. The audits define the ground truth the new work must fix, not paper over:

### Workout flow debt the rebuild must clear (DEEP_DIVE §1)

Blockers: **B1** no exercise seed data (only achievements have `HasData` — re-verified); **B2** no UI path to create a custom exercise.
Data loss: **D1** one set value cloned N times (pyramids unrepresentable); **D2** per-exercise notes destroyed (no Notes column on `WorkoutExercise` — re-verified against the entity); **D3** cardio duration × sets; **D4** `reps: 1` garbage on cardio; **D5** `distanceMeters` has no input; **D6** UTC default date; **D7** 0kg coerced to null.
UX: **U1** gamification response discarded (`LogWorkoutResult { streak, unlockedAchievements }` ignored — the meals flow does it right); **U3** in-progress workout lives only in client state (refresh/JWT-expiry wipes an hour of logging); **U8** unbounded numerics both layers.
Backend gaps: **G1** no GET-by-id/UPDATE/DELETE for workouts; **G2** unvalidated exerciseId → 500; **G3** empty-exercise workouts farm achievements; **G4** category free-text case-sensitive; **G5** trainer `CanViewWorkouts` permission controls nothing.

The current `Workout`/`WorkoutExercise`/`ExerciseSet` entity model (re-read today) is actually per-set and mostly right — `ExerciseSet` has `Reps?`, `WeightKg?`, `DurationSeconds?`, `DistanceMeters?`, `Completed`. The breakage is the form flattening everything plus missing seed/validators/endpoints. The LiftLog-derived redesign (doc 04) keeps these tables and extends them; it does NOT require a from-scratch schema.

### Cross-cutting fixes the new work depends on

| Dependency | Finding | Why the LiftLog work needs it |
|---|---|---|
| Local-date handling (D5/D6 flow, E5) | UTC "today" breaks streaks/diary west of UTC | Workout streaks + feed timestamps inherit the same bug; fix once (client sends `toLocaleDateString('en-CA')`, backend accepts `ClientDate`) before adding more date-sensitive features |
| Transactions (D6 anti-pattern) | Meal log = 3 independent SaveChanges | Workout log + feed publish + achievement evaluation is even longer; wrap in a transaction behavior |
| Notifications v1 (DEEP_DIVE §3 Spec 3) | Stub page, no entity | Social features are dead without follow-request/accepted/reaction notifications; build Spec 3 first, social hooks write into it |
| Gamification toaster mounted everywhere (Spec 4c) | Workout POST response ignored | The whole point of integrating with achievements |
| Achievement evaluator gaps (§2.8) | `recipes_created`, `goal_progress_logged` criteria never trigger | New workout/social achievement criteria plug into the same evaluator; fix the trigger wiring pattern first |
| Error propagation (U1 UX) | `data/*.ts` swallows errors → fake empty states | New feed/workout pages must NOT copy this pattern; use `{ok, data|error}` returns |
| `data/*.ts` reads as server actions (A16) | Serialized POSTs for reads | New feature reads go through `clientApi` GETs or server components |

### Existing assets to build on (verified working)

- **Achievements**: seeded criteria, `AchievementEvaluator`, `UserAchievement`, progress bars toward locked badges, `GamificationToaster` pattern in the meals flow, `LogWorkoutResult` DTO already carries `streak` + `unlockedAchievements` in generated types.
- **Streaks**: `StreakService` with `workout` streak type already ticked by `LogWorkoutCommand`.
- **Admin shell**: `app/admin/*` with users/foods/exercises/moderation pages, `GetAchievementAnalyticsQuery` for analytics.
- **SignalR plumbing**: Redis backplane configured; ChatHub exists (though REST path never broadcasts — D3); reuse for feed/notification pushes only if D3 is resolved first, otherwise poll.
- **Trainer relationship model**: `TrainerClientRelationship` with `CanViewWorkouts` permission — the social "share with trainer" story should reuse it (G5 says the permission is currently enforced by nothing; the new `GetClientWorkoutsQuery` closes it).

## Priority order (combined, updated)

1. **Security batch 1** (`01-security-remediation.md` R1-R4) — prerequisite for everything.
2. **Workout P0** (seed exercises, toaster, local dates, `?tab=log`, validators) — un-breaks the feature the redesign replaces, ships value immediately, and de-risks the rebuild.
3. **Notifications v1** (Spec 3) — social prerequisite.
4. **Workout rebuild sampled from LiftLog** (doc 04 §2-4) — templates, per-set logging, active session, stats.
5. **Social layer** (doc 04 §5) — profiles, follows, feed, reactions.
6. **Gamification + admin integration** (doc 04 §6-7).
7. Remaining audit sprints (shopping lists, households, trainers, visual overhaul) continue in parallel per DEEP_DIVE §5 — out of scope for this pack but not superseded by it.
