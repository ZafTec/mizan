# LiftLog → Mizan Feature Mapping & Design

This is the design document for sampling LiftLog's workout/exercise/social features into Mizan, integrated with the existing achievement gamification and admin area. **License constraint:** LiftLog is AGPL-3.0. Everything below is a clean-room re-implementation of concepts and data values; never copy LiftLog source into this repo.

Backend follows Mizan's Clean Architecture (Domain entity → Command/Query + FluentValidation → Controller → `bun run codegen`); frontend follows the server-component + client-island split, `clientApi`/`serverApi` conventions, and generated types. No local-first, no E2E crypto (see doc 03 for rationale).

Naming note: application content uses no em-dashes; user-facing strings follow the existing copy tone.

---

## 1. Schema overview (all new/changed EF Core entities)

Existing tables kept: `Exercise`, `Workout`, `WorkoutExercise`, `ExerciseSet` (per-set model is already right). Changes marked Δ; new tables marked +.

```
Δ Exercise                 + IsApproved bool (default true for seeds, false for user submissions surfaced publicly)
                           Category becomes enum-backed string: Strength|Cardio|Flexibility|Balance (G4 fix)
Δ WorkoutExercise          + Notes varchar(500)   (D2 fix)
                           + SupersetWithNext bool
Δ Workout                  + TemplateId Guid? -> WorkoutTemplate (which template produced it, null = ad-hoc)
                           + BodyweightKg decimal?          (LiftLog: session bodyweight snapshot)
                           + StartedAt/CompletedAt DateTime? (per-set timestamps make duration derivable; keep DurationMinutes for manual entry)
Δ ExerciseSet              + CompletedAt DateTime?          (LiftLog: per-set completion timestamp; Completed bool stays)
                           + ResistanceLevel decimal?, InclinePercent decimal?, Steps int?  (cardio trackables; optional, phase 2)

+ WorkoutTemplate          { Id, UserId?, Name, Notes, IsBuiltIn bool, SortOrder, CreatedAt, UpdatedAt }
                           UserId null = built-in (Starting Strength etc.); user templates owned
+ WorkoutTemplateExercise  { Id, TemplateId, ExerciseId, SortOrder, Sets int, RepsPerSet int?, TargetWeightKg decimal?,
                             RestSecondsMin int?, RestSecondsMax int?, RestSecondsFailure int?,
                             SupersetWithNext bool, Notes,
                             ProgressionType enum { None, IncreaseAllEvenly, IncreaseLowestSet },
                             ProgressionAmountKg decimal?,
                             TargetType enum { Reps, Time, Distance }, TargetSeconds int?, TargetDistanceMeters decimal? }
                           (flattened LiftLog blueprint: weighted = TargetType.Reps, cardio = Time|Distance)
+ WorkoutDraft             { UserId PK, Payload jsonb, UpdatedAt }   (active-session persistence; one draft per user)

+ SocialProfile            { UserId PK, Handle varchar(30) unique nullable, DisplayName, Bio varchar(200)?, AvatarUrl?,
                             IsDiscoverable bool default false, DefaultPublishWorkouts bool default false,
                             ShareToken varchar(32) unique (rotatable), CreatedAt }
                           Row exists only after opt-in. ShareToken powers LiftLog-style share links: /u/share?t=TOKEN
+ Follow                   { Id, FollowerUserId, FolloweeUserId, Status enum { Pending, Accepted }, CreatedAt, RespondedAt?,
                             unique (FollowerUserId, FolloweeUserId) }   one-way, request/approve (LiftLog model)
+ FeedItem                 { Id, UserId, Type enum { WorkoutCompleted, AchievementUnlocked, StreakMilestone, TemplateShared },
                             WorkoutId?, AchievementId?, TemplateId?, Caption varchar(280)?, CreatedAt, ExpiresAt? }
                           index (UserId, CreatedAt DESC). ExpiresAt: LiftLog-style retention (default null; configurable, e.g. 90d)
+ FeedReaction             { Id, FeedItemId, UserId, Emoji varchar(8), CreatedAt, unique (FeedItemId, UserId, Emoji) }
+ FeedComment              { Id, FeedItemId, UserId, Body varchar(500), CreatedAt, DeletedAt?, DeletedByUserId? }  (soft delete for moderation)
+ ContentReport            { Id, ReporterUserId, TargetType enum { FeedItem, FeedComment, SocialProfile, Exercise },
                             TargetId, Reason varchar(500), Status enum { Open, Actioned, Dismissed },
                             CreatedAt, ResolvedAt?, ResolvedByUserId?, ResolutionNote? }
+ Notification             (from DEEP_DIVE §3 Spec 3, unchanged: Id, UserId, Type, Title, Body?, LinkUrl?, CreatedAt, ReadAt?;
                             index (UserId, ReadAt, CreatedAt DESC))
```

Account deletion (ANALYSIS D1) must extend to all new tables: cascade `SocialProfile`, `Follow` (both directions), `FeedItem`+reactions+comments; SetNull `ContentReport.ReporterUserId`/`ResolvedByUserId`.

## 2. Exercise catalog (fixes B1/B2/S6/G4)

- **Seed migration:** ~120 exercises across Strength/Cardio/Flexibility/Balance with `MuscleGroup` and `Equipment`. Derive the strength list from LiftLog's `built-in-programs.ts` names (Squat, Bench Press, Deadlift, Overhead Press, Barbell Row, ...) plus standard accessories per muscle group; category casing must match frontend literals exactly (`"Strength"` etc.).
- **Custom exercises:** keep `POST /api/Exercises` `[Authorize]`, but `GetExercisesQuery` returns global (`IsCustom == false`) + own custom rows only (closes S6). Inline "Can't find it? Create exercise" in the picker (B2).
- **Promotion path (admin):** admin can flip a custom exercise to global (`IsCustom = false`) from the admin exercises page; `ContentReport` covers abusive custom exercises.
- **Category hardening (G4):** enum-constrain in validator, normalize casing on write, case-insensitive filter on read; replace hardcoded frontend dropdowns with the facets the query already computes (G6).

## 3. Workout templates & programs (LiftLog's core value)

- Seed `WorkoutTemplate` built-ins from LiftLog's programs: Starting Strength A/B, StrongLifts 5x5 A/B, PPL (3 sessions) with real sets/reps/rest/progression values (transcribe from `built-in-programs.ts`).
- API: `GET /api/WorkoutTemplates` (built-ins + own), `POST/PUT/DELETE` (own only), `POST /api/WorkoutTemplates/{id}/duplicate` (copy built-in to own for editing).
- **Start-from-template:** `GET /api/WorkoutTemplates/{id}/next-session` returns the template hydrated with last-performed weights per exercise, with the template's progression rule applied when the previous session hit all target reps (LiftLog `applyProgressiveOverload`, implemented as pure functions in `Mizan.Domain` — unit-test heavily, they are the "functional core" poster child).
- Progression strategies: None, IncreaseAllEvenly(amount), IncreaseLowestSet(amount, strategy first|middle|last|all) — port the semantics from `blueprint-models/index.ts:315-430`.

## 4. Workout logging rebuild (merges DEEP_DIVE §1.5 spec with LiftLog UX)

Component tree per DEEP_DIVE §1.5 (server `page.tsx` reading `?tab=`, `WorkoutTabs`, `history/*`, `log/*`) with these LiftLog-derived upgrades to the log form:

- **Start options:** "From template" (template picker, pre-populated sets/weights via next-session endpoint) | "Empty workout" | "Repeat last".
- **PotentialSet model in the reducer:** `DraftSet { uid, targetReps, repsCompleted?, weightKg, durationSeconds?, distanceMeters?, completedAt? }`. Uncompleted sets render as an in-gym checklist.
- **Tap-to-cycle reps** (LiftLog's killer interaction): tap set → repsCompleted = targetReps; tap again → decrement; at 0 → back to uncompleted. Long-press (or edit icon) opens the per-set weight/reps editor with "apply weight to: this set | uncompleted sets | all sets" (LiftLog `WeightAppliesTo`).
- **Rest timer:** on set completion start a countdown from the template's rest range; show in a sticky bar; fire a web Notification when max rest elapses (permission-gated, degrade silently). Store authority in the reducer, timer is disposable (WorkoutWorker principle).
- **Draft persistence (U3):** debounce-save the reducer state to `PUT /api/Workouts/draft` (jsonb) + localStorage fallback; on mount offer "Resume workout?". Server draft survives device switches and the 15-min JWT redirect.
- **Bodyweight field** on the form (optional, prefills last body measurement; on save also writes a `BodyMeasurement` row — closes the "two weight stores" gap for this flow).
- **Finish flow:** POST corrected per-set payload (DEEP_DIVE §1.5 shape + `completedAt` per set) → read `LogWorkoutResult` into `GamificationToaster` (U1) → **post-workout screen** (LiftLog): summary stats, new PRs, achievements unlocked, and (if opted into social) a "Share to feed" toggle defaulted from `DefaultPublishWorkouts` + optional caption.
- All P1 correctness fixes from DEEP_DIVE §1.6 are subsumed (per-set rows kill D1/D3/D4/D5/D7; local dates D6; bounds U8; uid keys U4/U5).

Backend work: notes column (D2), validator bounds (U8/G2/G3: Exercises NotEmpty, <=30 exercises, 1-50 sets, reps 1-1000, weight 0-1000, pre-validate exerciseIds → 400), `GET/PUT/DELETE /api/Workouts/{id}` ownership-scoped (G1), `GET /api/Workouts/stats?days=` (G1/U10), draft endpoints, next-session endpoint. Then `bun run codegen`.

## 5. Stats page (sampled from LiftLog `calculate-stats.ts`)

New `GET /api/Workouts/stats?from=&to=` returning the LiftLog shape, computed server-side with grouped queries (not client-side over full history):
- totals: workoutsPerWeek, setsPerWeek, averageSessionMinutes, totalVolumeKg
- heaviestLift { exerciseId, name, weightKg, date }, maxSessionVolume
- perExercise[]: weight-over-time series (top set + estimated 1RM via Epley: `w * (1 + reps/30)`), reps breakdown, PR flags
- bodyweight series (from Workout.BodyweightKg union BodyMeasurements)
- perMuscleGroup weekly set counts (drives a "weekly muscle coverage" widget; possible because Mizan has a real catalog with MuscleGroup — better than LiftLog)

Frontend `/workouts?tab=stats` (or `/stats` page): charts follow the MACRO_COLORS/`--chart-*` token rules from DEEP_DIVE §4.4. PRs computed here feed FeedItem generation and achievement criteria.

## 6. Social layer (LiftLog mechanics, Mizan architecture)

Privacy model (translate LiftLog's posture, drop the crypto):
- **Opt-in:** no `SocialProfile` row → invisible to every social query.
- **Link-based discovery by default:** share URL `/u/share?t=SHARE_TOKEN`; token rotatable ("reset link" revokes old links). `IsDiscoverable` opt-in adds handle search later (phase 2).
- **Request/approve, one-way follows, revocation** exactly as LiftLog (doc 03). Revoke = delete Follow row; feed access disappears at query time.
- **Per-item publishing:** feed items are created ONLY from the post-workout share toggle / explicit share actions. Nothing auto-publishes. Achievement/streak feed items are offered as a toggle in the same flow, not auto.

API surface:
```
POST   /api/Social/profile                 create/update own profile (opt-in)
DELETE /api/Social/profile                 leave social entirely (cascade own feed items + follows)
POST   /api/Social/profile/rotate-token
GET    /api/Social/share/{token}           public-ish: display name + avatar only (rate-limited, anonymous-allowed)
POST   /api/Social/follows                 request by share token
GET    /api/Social/follows?direction=in|out&status=pending|accepted
POST   /api/Social/follows/{id}/respond    accept|reject (followee only)
DELETE /api/Social/follows/{id}            unfollow (follower) or revoke (followee)
GET    /api/Social/feed?page=              own items + accepted followees', newest first (join through Follow Accepted, enforced in the query handler)
POST   /api/Social/feed                    publish { type, workoutId|templateId|achievementId, caption } (ownership-validated)
DELETE /api/Social/feed/{id}               own items
POST   /api/Social/feed/{id}/reactions     add emoji (allowlist ~8 emoji); DELETE to remove
POST   /api/Social/feed/{id}/comments      (rate-limited); DELETE soft-deletes (author or admin)
POST   /api/Social/reports                 report content
```
Feed workout rendering: a `WorkoutSummaryDto` snapshot (name, date, duration, exercise names, per-exercise top set, total volume, PR badges) embedded at publish time into FeedItem (jsonb column or computed on read from WorkoutId — decide at implementation; snapshot-at-publish is simpler and immune to later edits; recommended).

Frontend: `/social` (feed), `/social/profile` (own profile + follower management + pending requests), `/u/share` (landing for share links: shows profile stub + "Request to follow" or login redirect). Follow requests/accepts write `Notification` rows; the bell (Spec 3) is the re-engagement channel.

Notifications integration (extends Spec 3 hook list): follow_request, follow_accepted, feed_reaction (batched per item per day), feed_comment, content_report_actioned (to reporter).

## 7. Gamification integration (the user's stated goal)

Wire the new features into the EXISTING evaluator/streak/toaster system, not a parallel one:

- **New achievement criteria** (seed rows, same `HasData` pattern; extend `AchievementEvaluator` with grouped stats per A13 guidance):
  `workouts_logged` (exists) plus: `workout_streak_days`, `total_volume_kg` tiers, `pr_count`, `template_completed_count`, `programs_finished`, `followers_count` tiers, `reactions_given`, `workouts_shared`, `comments_made` (small numbers; anti-farm: only on distinct feed items).
- **Triggers:** LogWorkoutCommand (exists) + new: RespondToFollowCommand (accepted), PublishFeedItemCommand, AddReactionCommand, AddCommentCommand. Fix the existing dead criteria wiring (`recipes_created`, `goal_progress_logged`) in the same pass so the trigger pattern is uniform.
- **Anti-farming guards (prereq):** G3 fix (no empty workouts), one workout-streak tick per local date, reaction/comment achievements capped per day, `unique (FeedItemId, UserId, Emoji)`.
- **Streaks:** `workout` streak type exists; apply local-date fix (Spec 4a) and freezes (4b) here too. Streak milestones {7,30,100} offer a feed share.
- **Toaster everywhere (Spec 4c):** workout finish, follow accepted ("First follower" badge), publish.
- **PR celebrations:** stats endpoint marks PRs; post-workout screen surfaces "New PR: Bench 85kg"; PR unlocks feed via `pr_count` criteria.

## 8. Admin integration (the user's stated goal)

Extend `app/admin/*`:
- **Exercises page** (exists, read-only): add approve/promote custom→global, edit, delete-with-usage-guard; submission queue when `IsApproved=false` flow is enabled.
- **New Moderation queue** (`admin/moderation` currently claims to review user foods that can't exist — make it real): list `ContentReport` open items with target preview; actions: dismiss, delete content (soft-delete comment / remove feed item), warn user (Notification), ban user (existing ban fields; NOTE ANALYSIS D8: bans are written frontend-side via Drizzle and the backend's `UserStatusService` caches for 2 min — add the backend invalidation endpoint as part of this).
- **New Social overview:** counts (profiles, follows, feed items, reports open/actioned), recent reports; extend the existing `GetAchievementAnalyticsQuery` pattern with a `GetSocialAnalyticsQuery`.
- **Templates admin:** CRUD over built-in `WorkoutTemplate` rows (they're data, not migrations, after the seed).
- **Fix the trainer-role no-op** (DEEP_DIVE §2.6) in the same area since admin/users is being touched: real backend role-update endpoint, delete fake TrainerStats/RecentMessages fallbacks.
- All admin endpoints `[Authorize(Roles = "admin")]`, JWT-only (post-R2 verify ApiKey scheme cannot reach them).

## 9. Trainer tie-in (closes G5)

`GetClientWorkoutsQuery` + `GET /api/Trainers/clients/{id}/workouts` honoring `CanViewWorkouts`; trainer client view gets a workouts tab. This makes the existing permission toggle real and is the trainer-side payoff of the rebuild. (Full trainer-feature repair remains in the DEEP_DIVE roadmap, out of scope here.)

## 10. Explicitly out of scope (phase 5+ / later)

- AI program generator tool in the pro AI chat emitting WorkoutTemplates (LiftLog's planner concept; fits the NutritionPlugin tool pattern) — design when the AI transcript-memory fix (§2.7) lands.
- Handle search / discoverability, follower-of-follower suggestions.
- Health-platform export, plaintext workout export additions.
- SignalR live feed updates (poll first; D3 must be resolved before any new SignalR investment).
- i18n.
