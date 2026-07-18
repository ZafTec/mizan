# Implementation Roadmap & Handoff

Phased plan for the implementing agent(s). Each phase is independently shippable, ordered by dependency. Follow repo conventions: CLAUDE.md workflow, Clean Architecture layering, FluentValidation, `bun run codegen` after every backend DTO change, generated types only (no hand-written DTO interfaces — ANALYSIS D11), reads via server components/`clientApi` (not server actions — A16), error propagation not swallowing (U1), no comments unless asked, no em-dashes in content.

**Read first:** `02-current-state-audit.md` (ground truth + dependency table), `04-feature-mapping.md` (the design), `01-security-remediation.md` (blocking), `06-test-baseline.md` (current suite state). LiftLog reference: re-clone `https://github.com/LiamMorrow/LiftLog` (shallow) into a temp folder; key files are cited in `03-liftlog-analysis.md`.

## Phase 0: Baseline hygiene (half a day)

1. Fix the 2 stale meal-type tests (see 06) so the suite is green before any work starts.
2. Update CLAUDE.md drift: remove `bun run test`/`test:e2e`/`codegen:zod` claims or add the scripts; ES256 → EdDSA note.
3. Decide test infrastructure for the frontend: add Vitest + Playwright now (recommended: the phases below specify tests that need somewhere to live). Minimal setup, no retrofitting old pages.

**Gate:** `docker-compose --profile test up test` fully green; `bun run lint` green.

## Phase 1: Security batch (BLOCKING, ~2-3 days)

Execute `01-security-remediation.md` R1-R4 in order, then R5-R7. Batch 3 (LOW) items S6/S7 land inside Phases 2/4 where noted; S8-S10 as a cleanup PR.

**Gate:** integration tests listed per item in 01 pass; MCP tokens + ServiceApiKey rotated; SECURITY-FINDINGS.md statuses updated to FIXED with commit hashes.

## Phase 2: Workout P0 + catalog + templates (backend-heavy, ~1 week)

1. Exercise seed migration (~120 rows) + category enum hardening (G4) + scoped GetExercises (S6) + custom-exercise create UI hook point (B2).
2. Schema Δ: WorkoutExercise.Notes + SupersetWithNext; Workout.TemplateId/BodyweightKg/StartedAt/CompletedAt; ExerciseSet.CompletedAt. One migration.
3. Validators: LogWorkoutCommand bounds + NotEmpty + exerciseId pre-validation (G2/G3/U8).
4. Workout CRUD: GET-by-id/PUT/DELETE ownership-scoped (G1); fix the fabricated CreatedAtAction Location.
5. WorkoutTemplate + WorkoutTemplateExercise tables, CRUD, duplicate endpoint; built-in seeds transcribed from LiftLog `built-in-programs.ts` (Starting Strength, StrongLifts 5x5, PPL).
6. Progression strategies as pure functions in `Mizan.Domain` + `next-session` endpoint.
7. Quick frontend wins inside the CURRENT page while the rebuild is pending: read LogWorkoutResult into GamificationToaster (U1, ~15 lines), local-date default (D6), `?tab=log` deep link + QuickActions target, delete dead `data/workout.ts logWorkout`.
8. `bun run codegen`.

**Tests:** unit tests for progression functions (re-derive equivalent cases to LiftLog's `blueprint-models/index.spec.ts`; LiftLog is AGPL-3.0, do not copy code or tests verbatim); integration tests for validator bounds, ownership on PUT/DELETE, next-session weight progression, seed presence.
**Gate:** fresh `docker-compose up` install can log a real workout through the UI; empty-exercise POST returns 400.

## Phase 3: Notifications v1 (~3 days, prerequisite for social)

Implement DEEP_DIVE §3 Spec 3 verbatim (entity, INotificationWriter sharing the caller's transaction, 4 endpoints, NotificationBell polling, page rewrite). Hook existing events: household invite/response, trainer request/response, achievement unlocks, streak milestones. Skip the streak-at-risk email job for now (independent).

**Gate:** accepting a household invite produces a bell badge + notification row for the inviter.

## Phase 4: Workout logging rebuild (frontend-heavy, ~1.5 weeks)

Per 04 §4: component tree, useReducer PotentialSet draft, tap-to-cycle, WeightAppliesTo editor, rest timer, server-side draft (WorkoutDraft table + endpoints) with localStorage fallback, bodyweight field, finish flow → post-workout summary screen. Stats endpoint + stats tab (04 §5). Trainer GetClientWorkoutsQuery (04 §9).

**Tests:** reducer unit tests (every action, incl. cycle semantics at 0 and weight-applies-to scopes); Playwright: template → log 3 sets → finish → toaster → history shows per-set data; draft resume after reload.
**Gate:** DEEP_DIVE §1.2 data-loss table all closed (D1-D7); U3 draft survives refresh + JWT expiry redirect.

## Phase 5: Social layer (~1.5-2 weeks)

Per 04 §6: schema (SocialProfile, Follow, FeedItem, FeedReaction, FeedComment, ContentReport), API surface, share-link flow, feed page, profile/follower management, post-workout share toggle, notification hooks, rate limits (S7 pattern), snapshot-at-publish WorkoutSummary.

**Tests (access control is the point):** integration tests proving: no profile → invisible; pending follow sees nothing; revoked follower loses feed access; non-follower cannot read feed items by id; reaction uniqueness; comment soft-delete hides from non-admins. Playwright: share link → request → accept → item appears in follower feed.
**Gate:** the access-control matrix passes; anonymous share-link endpoint rate-limited.

## Phase 6: Gamification + admin integration (~1 week)

Per 04 §7-8: new achievement criteria seeds + evaluator triggers (fix the two dead existing criteria in the same pass), anti-farm guards, streak local-dates/freezes if not already landed via the parallel roadmap, toaster on all new flows; admin moderation queue + social overview + exercise promotion + templates admin + trainer-role fix + ban invalidation endpoint (D8).

**Gate:** unlocking `first_workout_shared` fires the toaster and appears in admin achievement analytics; a reported comment can be soft-deleted from the moderation queue and disappears for users.

## Verification sweep (end of each phase)

1. `docker-compose --profile test up test` green.
2. `bun run lint` + `bun run build` green.
3. Playwright suite green (once Phase 0 adds it).
4. Manual browser pass (Chrome DevTools MCP / claude-in-chrome) on the changed flows: console clean of errors, no 4xx/5xx in network tab on happy paths, no hydration warnings.
5. Update SECURITY-FINDINGS.md and this folder's docs with status changes.

## Effort summary

| Phase | Effort | Ships |
|---|---|---|
| 0 Baseline | 0.5d | green suite |
| 1 Security | 2-3d | closed CRITICAL/HIGH |
| 2 Catalog+templates | 1w | workouts usable on fresh install; templates |
| 3 Notifications | 3d | bell + real notifications |
| 4 Logging rebuild | 1.5w | LiftLog-grade logging + stats |
| 5 Social | 1.5-2w | profiles, follows, feed |
| 6 Gamification+admin | 1w | integrated achievements + moderation |

Total ≈ 6-7 weeks single-agent. Phases 2/3 parallelize; 5 depends on 3; 6 depends on 4+5.

## Product decisions resolved 2026-07-18

1. Workout summaries use live reads.
2. Feed items are retained indefinitely.
3. Social features are free.
4. Reactions are fixed to `👍 ❤️ 💪 🔥 👏 🎉 🏆`.
5. Handles and public discovery search are deferred.
