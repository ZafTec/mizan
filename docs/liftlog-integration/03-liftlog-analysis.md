# LiftLog Codebase Analysis

**Source:** https://github.com/LiamMorrow/LiftLog (LiamMorrow, **AGPL-3.0**: do NOT copy code verbatim into Mizan; that would obligate AGPL for the whole service. We sample *designs, domain models, UX flows, and data values* (program set/rep schemes are facts), re-implemented from scratch in Mizan's stack).
**Analyzed:** 2026-07-07, shallow clone at scratchpad `LiftLog/` (temp; re-clone with `git clone --depth 1 https://github.com/LiamMorrow/LiftLog` if needed).

## What LiftLog is

A production gym-tracking app (Play Store + App Store) built **local-first**: React Native + Expo + Redux Toolkit + expo-router + Drizzle/SQLite on device, `@js-joda` for dates, `BigNumber` for weights. A thin C# ASP.NET backend (`backend/LiftLog.Api`) exists ONLY for: the E2E-encrypted social feed, AI plan generation (Anthropic, via SignalR hubs `/ai-chat-v2`), purchase verification (RevenueCat), and rate limiting. All workout data lives on-device; the server never sees plaintext workouts.

This architecture is the OPPOSITE of Mizan (server-authoritative, Clean Architecture, EF Core). We are not adopting local-first or client-side E2E encryption; we are sampling its **domain model, UX flows, seed content, and social mechanics** and re-expressing them in Mizan's architecture. Doc 04 does that mapping explicitly.

## Repo layout

```
app/            React Native app (the product)
  src/app/      expo-router routes: (session)/ [active workout], history/, stats/, feed/, settings/
  src/models/   Domain: blueprint-models, session-models, feed-models, weight, built-in-programs
  src/store/    Redux slices + effects: current-session, program, stats, feed, ai-planner, settings
  src/services/ session-service, feed-* (identity/follow/inbox/encryption), notification, health-export, workout-worker
backend/
  LiftLog.Api/  Controllers (User(s), Events, Inbox, FollowSecret, SharedItem), Hubs (AI chat), Services
  LiftLog.Lib/  Shared models/serialization
  RevenueCat/   Generated RevenueCat API client
docs/           FeedProcess.md (E2E protocol), RemoteBackup.md, WorkoutWorker.md, Migrations.md
tests/          cypress-tests/, LiftLog.Tests.Api
```

## Domain model (the part worth sampling)

### Planning side: Program → Session → Exercise blueprints

```
ProgramBlueprint { name, sessions: SessionBlueprint[], lastEdited }
SessionBlueprint { name, exercises: ExerciseBlueprint[], notes }
ExerciseBlueprint = WeightedExerciseBlueprint | CardioExerciseBlueprint   (discriminated union)

WeightedExerciseBlueprint {
  name, sets: int, repsPerSet: int, weightIncreaseOnSuccess,
  restBetweenSets: Rest { minRest, maxRest, failureRest },   // Durations
  supersetWithNext: bool, notes, link,
  progressiveOverload: NoProgressiveOverload
                     | IncreaseAllEvenlyProgressiveOverload { amount }
                     | IncreaseLowestSetProgressiveOverload { amount, increaseStrategy: first|middle|last|all }
}

CardioExerciseBlueprint { name, sets: CardioExerciseSetBlueprint[], notes, link }
CardioExerciseSetBlueprint {
  target: { type: 'time', value: Duration } | { type: 'distance', value: { value, unit: metre|yard|mile|kilometre } },
  trackDuration, trackDistance, trackResistance, trackIncline, trackWeight, trackSteps  // per-set boolean toggles
}
```

Key ideas:
- **Plans are first-class.** Users don't assemble a workout from scratch each visit; they run a *program* whose sessions pre-populate the log. This is the single biggest UX difference from Mizan's current blank form.
- **Progressive overload is a strategy object** applied when a session succeeds: next time, weights auto-increase. Three strategies, all trivial pure functions (`applyProgressiveOverload(exercise) → exercise`).
- **Cardio is target-based** (time or distance target) with per-set toggles for which metrics to record — solves Mizan's D3/D4/D5 class of bugs by construction.
- **Supersets** are a boolean link to the next exercise.

### Recording side: Session → RecordedExercise → PotentialSet

```
Session { id, blueprint: SessionBlueprint, recordedExercises: RecordedExercise[], date: LocalDate, bodyweight?: Weight }
RecordedExercise = RecordedWeightedExercise | RecordedCardioExercise

RecordedWeightedExercise { blueprint, potentialSets: PotentialSet[], notes? }
PotentialSet { set?: RecordedSet, weight: Weight }        // a PLANNED set that may not have happened
RecordedSet { repsCompleted: int, completionDateTime: OffsetDateTime }

RecordedCardioExercise { blueprint, sets: RecordedCardioExerciseSet[], notes? }
RecordedCardioExerciseSet { completionDateTime?, duration?, distance?, resistance?, incline?, weight?, steps? }
```

Key ideas:
- **PotentialSet** = the planned set exists before it is performed; completing it stamps `RecordedSet` with reps + timestamp. Uncompleted sets stay visible (that IS the in-gym checklist UI).
- **Tap-to-cycle reps** (`withCycledRepCount`): tap an empty set → filled with target reps; each subsequent tap decrements (10 → 9 → 8 … → 0 → empty). One-thumb logging of "almost hit target". This plus per-set completion timestamps drives the rest timer.
- **Per-set completion timestamps** enable: rest-timer countdown (min/max rest from blueprint), session duration, and honest per-set history.
- **Bodyweight snapshot per session** feeds bodyweight stats.
- Immutable model classes with `.with({...})` updaters and `equals` — maps cleanly onto a `useReducer` draft state in React (the DEEP_DIVE §1.5 rebuild spec already proposed exactly this shape; LiftLog validates it).

### Session lifecycle & active workout

- Redux slice `current-session` holds the active workout; persisted continuously to SQLite (device) — survives app kills. Mizan equivalent: localStorage draft + (better) a server-side draft row.
- `WorkoutWorker` (docs/WorkoutWorker.md): a platform-specific background worker (Android ForegroundService) fed by an event/command stream for persistent notifications + rest timers. It is explicitly NOT the source of truth. Web analog: a rest-timer with the Notifications API + `document.visibilitychange`; keep the same "worker is disposable, store is authority" principle.
- Post-workout screen (`history/post-workout.tsx`): summary + share step after finishing.

### Stats (`store/stats/calculate-stats.ts` — pure function, unit-tested)

`calculateStats(sessions, preferredUnit, timeRange)` returns:
- `workoutsPerWeek`, `setsPerWeek`, `averageSessionLength`
- `heaviestLift`, `maxWeightLiftedInAWorkout`
- `bodyweightStats` over time (min/max/current)
- `weightedExerciseStats[]` per exercise-name (normalized): weight-over-time series, reps breakdown, one-rep-max estimates
- `sessionStats[]` grouped by session-blueprint name

All computed client-side from full history. Mizan should compute server-side (`GET /api/Workouts/stats`) but the *shape* of this return type is the spec for what the stats page shows. Exercise identity is by **normalized name** (`NormalizedName`), not ID — important because LiftLog has no exercise catalog; Mizan HAS one (Exercise table), which is strictly better — keep IDs, fall back to name-matching for imports.

### Built-in programs (`models/built-in-programs.ts`, ~970 lines)

Seeded, well-known programs: Starting Strength (A/B), StrongLifts 5x5 (A/B), PPL (Push/Pull/Legs), and more — each fully specified with sets/reps/rest/progression. **This file is the seed-data blueprint Mizan needs for B1** — both an exercise catalog (distinct exercise names + muscle groups implied by the programs) and starter workout templates.

## Social feed (docs/FeedProcess.md + store/feed + Controllers)

Mechanics (strip the crypto, keep the mechanics):
1. User opts in and creates a **profile** (name + picture, both optional).
2. User gets a **share URL** (`/feed/share?id=USER_ID&name=Name`) — link-based discovery, no search-by-default, no public directory.
3. Prospective follower opens the link → sends a **follow request**.
4. Owner sees pending requests, **accepts or rejects**; accept grants the follower read access to the owner's feed. **Following is one-way** (accept ≠ follow-back).
5. Owner can **revoke** a follower at any time (LiftLog: revoke the FollowSecret).
6. Feed items = published workouts (and plan updates). Publishing is per-workout opt-in at the post-workout screen (with a default preference).
7. Owner controls: display name, profile picture, and the follower list. That's the whole privacy surface — deliberately small.

E2E encryption details (AES-128 payloads, RSA-2048 inbox, server stores only ciphertext + timestamps, `FollowSecret` as capability token, random server-issued password per UserId) are documented in `docs/FeedProcess.md`. **We do not port the E2E layer** — Mizan is server-authoritative with real accounts — but we keep: request/approve follows, one-way follows, revocation, per-item publish opt-in, and the "server knows as little as necessary" posture translated to strict query-side access control.

Backend controllers worth reading for API shape: `UserController` (create/get/delete/put profile), `UsersController` (batch profile lookup), `EventsController` (feed items with expiry), `InboxController` (encrypted push-style messages), `FollowSecretController`, `SharedItemController` (shared plans/items with expiry). Plus `RateLimitService` (simple per-key consumption) and `CleanupExpiredDataHostedService` (feed items expire — a retention idea worth keeping: feed rows get a TTL, e.g. 90 days).

## AI planner

`GenericAiChatWorkoutPlanner` / `AnthropicChatPlannerV2` + SignalR streaming hub; produces a `ProgramBlueprint` from a goal/equipment/experience questionnaire (JSON-schema-constrained: `AiWorkoutPlan.json`, `AiSessionBlueprint.json`). Purchases (RevenueCat) gate usage + `RateLimitService`. Mizan analog: an "AI program generator" tool in the existing pro-gated AI chat that emits a WorkoutTemplate (fits the existing NutritionPlugin tool pattern and the new template tables). Phase 5 material, not core.

## Miscellaneous ideas worth stealing later

- **Plaintext export / full backup** (`docs/PlaintextExport.md`, `file-export-service`): user-owned data export — Mizan already has profile export (A12 notes it needs paging).
- **Health-platform export** (Apple Health / Health Connect adapters).
- **Remote backup with X-API-KEY** — not needed (server-authoritative).
- **Tolgee i18n** — out of scope.
- **Notification service** for rest timers — maps to web Notifications API.

## What we are NOT taking

| LiftLog aspect | Why not |
|---|---|
| Local-first SQLite storage | Mizan is server-authoritative; households/trainers/MCP all need server state |
| Client-side E2E feed encryption | Real accounts + server auth already exist; the value was "server can't read", which conflicts with trainer/admin/moderation requirements. Keep the *privacy posture* via allowlist follows instead |
| Anonymous identity (UUID + password) | Mizan has BetterAuth accounts |
| React Native / Expo / Redux | Mizan is Next.js; sample the component decomposition, not the framework |
| Name-keyed exercises | Mizan's Exercise catalog with IDs is better; keep it |
| RevenueCat | Paddle already integrated |
