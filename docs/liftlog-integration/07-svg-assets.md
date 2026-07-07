# SVG Asset Pack & Integration Guide

**Created:** 2026-07-07. Companion to `04-feature-mapping.md`; every placement below references the feature it decorates. All assets live in `docs/liftlog-integration/SVGs/`. At implementation time, copy them to `frontend/public/illustrations/` (sprites and marketing) and/or convert to React components under `frontend/components/illustrations/` (theme-aware illustrations). The files in this folder are the design source of truth; do not fork styles per page.

## Design language

- Brand palette from `app/globals.css`: verdigris (primary/teal), tuscan sun (#E8B849 gold), burnt peach (#D4654A), sandy brown, charcoal blue neutrals. Matches `MACRO_COLORS` and the `--chart-*` tokens (DEEP_DIVE §4.4).
- Flat geometric shapes, rounded corners, 1 to 2 accent colors per scene, small "fun" details (sleeping plates, happy flame, runaway plate). No gradients in app assets; gradients only in marketing.
- **Theme-aware:** app assets color via CSS custom properties with light-theme fallbacks, e.g. `fill="var(--primary, #2E837B)"`. They adapt to dark mode automatically **only when inlined** (vars do not cascade into `<img>`). Gold and peach are fixed because they read well on both themes.
- **Animated assets** use CSS keyframes on `transform`/`opacity` only (compositor-friendly, no layout/paint churn except the timer arc), loop gently, and every one ships a `@media (prefers-reduced-motion: reduce)` block that disables motion. No SMIL, no scripts.

## Inventory and placement

### Empty states (theme-aware; inline as components)

| File | Where | When shown |
|---|---|---|
| `empty-workouts.svg` | `/workouts?tab=history` (doc 04 §4 component tree) | User has zero logged workouts. Pair with CTA "Start your first workout" |
| `empty-templates.svg` | Template picker + `/workouts` start options (§3) | No user templates yet; render above the built-in program list, CTA "Browse programs" |
| `empty-exercise-search.svg` | Exercise picker (§2) | Search returns nothing; sits above the "Can't find it? Create exercise" inline CTA (B2) |
| `empty-stats.svg` | `/workouts?tab=stats` (§5) | Fewer than 2 workouts logged; caption "Log two workouts to unlock trends" |
| `empty-feed.svg` | `/social` feed (§6) | No feed items (new profile or no follows); CTA "Share your profile link" |
| `empty-followers.svg` | `/social/profile` follower management (§6) | Zero accepted follows; sits above the share-token link row |
| `empty-notifications.svg` (animated: bell sway, floating z) | Notification bell dropdown / page (Spec 3) | Empty notification list |
| `empty-moderation.svg` | `admin/moderation` queue (§8) | Zero open `ContentReport` rows; the reward screen for moderators |
| `error-generic.svg` | Any workout/social page error boundary | Query failure states; replaces the swallowed-error fake-empty anti-pattern (U1 UX): show this + retry button, never a fake empty list |
| `resume-workout.svg` | "Resume workout?" prompt (§4 draft persistence, U3) | Server/local draft found on mount; pair with Resume / Discard buttons |

### Celebrations and moments (theme-aware; inline)

| File | Where | When shown |
|---|---|---|
| `celebration-pr.svg` (animated: trophy bounce, confetti drift, ray pulse) | Post-workout screen (§4 finish flow) | Stats endpoint flags a new PR; headline "New PR: Bench 85 kg" |
| `celebration-achievement.svg` (animated: badge pop, star wiggle, burst) | `GamificationToaster` big-unlock variant + achievements page modal (§7) | `LogWorkoutResult.unlockedAchievements` non-empty |
| `celebration-streak.svg` (animated: flame flicker, day-dot pop) | Post-workout screen + streak milestone toast (§7) | Streak milestones {7, 30, 100}; swap the "7 DAYS" chip text per milestone |
| `confetti-burst.svg` (animated: looping fall) | Overlay layer behind any celebration content | Absolutely position on top of post-workout summary; pointer-events none |
| `success-check.svg` (animated: draw-on, plays once) | Workout saved, profile created, follow accepted confirmations | Brief confirmation before navigating to the post-workout screen |
| `rest-timer.svg` (animated: draining arc) | Rest timer education/empty state, onboarding coach mark (§4) | Decorative only. The REAL rest bar renders live state from the reducer; this illustration is for docs/onboarding, not the functional timer |
| `loading-barbell.svg` (animated: barbell doing reps) | Suspense fallbacks on `/workouts`, `/social`, stats tab | Any loading state in the new pages; drop-in replacement for spinners |

### Heroes / onboarding (theme-aware; inline)

| File | Where |
|---|---|
| `hero-tap-to-cycle.svg` (animated: tap ripple + hand) | First-run coach mark for the log form explaining tap-to-cycle reps (§4); also the `/workouts` marketing section for logged-out users |
| `hero-social-share.svg` | `/social` opt-in screen (before `SocialProfile` exists, §6 privacy model): shows what sharing looks like next to the "Create profile" CTA |
| `hero-progression.svg` | Template detail page explaining progressive overload (§3); pairs with the progression strategy picker |

### Icon sprites (currentColor; reference via `<use>`)

| File | Symbols | Where |
|---|---|---|
| `icons-workout.svg` | `wi-barbell` `wi-dumbbell` `wi-kettlebell` `wi-plate` `wi-rest-timer` `wi-flame` `wi-trophy` `wi-medal` `wi-pr-star` `wi-superset` `wi-rep-check` `wi-bodyweight` `wi-template` `wi-share-link` `wi-follow-add` `wi-comment` `wi-heart` `wi-cat-strength` `wi-cat-cardio` `wi-cat-flexibility` `wi-cat-balance` | Exercise catalog rows (category icons map to the §2 enum), template cards, set rows (`wi-rep-check`), superset badge (`wi-superset`), PR badges in feed/stats (`wi-pr-star`), social actions (`wi-heart`, `wi-comment`, `wi-follow-add`, `wi-share-link`), streak chip (`wi-flame`), bodyweight field (`wi-bodyweight`) |
| `icons-muscle-groups.svg` | `mg-chest` `mg-back` `mg-shoulders` `mg-arms` `mg-core` `mg-glutes` `mg-legs` `mg-calves` `mg-cardio` `mg-fullbody` | Exercise picker filter chips, exercise detail header, weekly muscle coverage widget (§5 perMuscleGroup) |

Note: lucide-react stays the default icon set for generic UI (chevrons, edit, trash). This sprite covers domain glyphs lucide lacks or renders generically. Do not duplicate lucide icons here.

### Marketing and social media (fixed colors; safe as `<img>` or raster export)

| File | Size | Use |
|---|---|---|
| `marketing-og-card.svg` | 1200x630 | Open Graph / link preview for the launch blog post and `/workouts` landing. Export to PNG for `og:image` (crawlers do not render SVG): `bunx sharp-cli -i marketing-og-card.svg -o og.png resize 1200 630` or any SVG-to-PNG step |
| `marketing-instagram-square.svg` | 1080x1080 | IG/Facebook feed post announcing workout tracking. Export PNG |
| `marketing-instagram-story.svg` | 1080x1920 | IG/WhatsApp story + TikTok cover. Export PNG |
| `marketing-banner-wide.svg` | 1440x480 | Landing page hero band for logged-out users; fine to inline on the web |
| `logo-badge-workouts.svg` | 512x512 | Feature icon: app store style badge, favicon-adjacent uses, blog thumbnails, release notes |

Marketing text uses the system font stack; if pixel-identical output matters across export environments, convert text to outlines in an editor before rasterizing. Update the `mizan.euaell.me` footer if the domain changes.

## Integration patterns

### 1. Theme-aware illustrations: inline, not `<img>`

CSS variables only resolve when the SVG is in the DOM. Create one wrapper and import raw SVG (Next 16 + `@svgr/webpack`, or copy markup into a `.tsx`):

```tsx
// components/illustrations/EmptyState.tsx
export function EmptyState({ art: Art, title, action }: {
  art: React.ComponentType<React.SVGProps<SVGSVGElement>>;
  title: string;
  action?: React.ReactNode;
}) {
  return (
    <div className="flex flex-col items-center gap-4 py-12 text-center">
      <Art className="w-64 max-w-full" aria-hidden="true" />
      <p className="text-muted-foreground text-sm">{title}</p>
      {action}
    </div>
  );
}
```

When the illustration is decorative next to explanatory text (the normal case), pass `aria-hidden="true"` and keep the message in real text. The built-in `<title>` elements cover the standalone case.

### 2. Sprites: served from `/public`, referenced with `<use>`

Copy both sprite files to `frontend/public/illustrations/`. Reference:

```tsx
export function WorkoutIcon({ id, className }: { id: string; className?: string }) {
  return (
    <svg className={className ?? "size-5"} aria-hidden="true">
      <use href={`/illustrations/icons-workout.svg#${id}`} />
    </svg>
  );
}
// <WorkoutIcon id="wi-pr-star" className="size-4 text-tuscan-sun-500" />
```

One HTTP fetch per sprite, cached forever (immutable asset headers via next.config if desired). Icons inherit `color`, so Tailwind `text-*` classes theme them. External `<use>` works in all evergreen browsers; no polyfill needed.

### 3. Animated assets

- All motion is CSS inside the SVG; they animate both inline and via `<img>` (but theme vars need inline, so inline them).
- `success-check.svg` plays once on mount (forwards fill). Remount (change React `key`) to replay.
- `confetti-burst.svg` and `loading-barbell.svg` loop; unmount them when done, do not `display:none`-and-keep (browsers keep ticking some animations).
- Reduced motion is handled inside each file; no JS gating needed. Do not add extra animation wrappers (framer-motion etc.) on top of already-animated assets.
- Keep at most one looping animated asset visible per viewport (perf and taste).

### 4. Performance rules (hold the line)

- No raster embeds, no `<filter>`/blur, no fonts loaded by SVGs (system stack only), no scripts. Files are 1 to 6 KB; brotli takes them further.
- Inline illustrations render zero-request; sprites are one cached request each.
- Animations restricted to `transform`/`opacity` (+ the timer's `stroke-dasharray`, which is cheap at this size).
- Marketing SVGs for social MUST ship as PNG exports (most platforms and OG crawlers ignore SVG). Keep the SVG as the editable master.
- Lint check: no `id` collisions when inlining multiple illustrations on one page. IDs are prefixed per file (`ew-`, `cs-`, `rt-`, ...) precisely for this; keep the convention for new assets.

### 5. Accessibility

- Every illustration has `role="img"` + `<title>`; decorative placements add `aria-hidden="true"` on the consuming element.
- Sprites are `aria-hidden` by design; pair icons with visible text or `sr-only` labels.
- Reduced-motion behavior is mandatory for any new animated asset.

## Coverage map (doc 04 section → assets)

| Doc 04 feature | Assets |
|---|---|
| §2 Exercise catalog | `empty-exercise-search`, `icons-workout` (cat-*), `icons-muscle-groups` |
| §3 Templates + progression | `empty-templates`, `hero-progression`, `wi-template`, `wi-superset` |
| §4 Logging rebuild | `empty-workouts`, `resume-workout`, `hero-tap-to-cycle`, `rest-timer`, `loading-barbell`, `success-check`, `wi-rep-check`, `wi-bodyweight` |
| §5 Stats | `empty-stats`, `wi-pr-star`, `icons-muscle-groups` (coverage widget) |
| §6 Social | `empty-feed`, `empty-followers`, `hero-social-share`, `wi-heart` `wi-comment` `wi-follow-add` `wi-share-link` |
| §7 Gamification | `celebration-pr`, `celebration-achievement`, `celebration-streak`, `confetti-burst`, `wi-flame` `wi-trophy` `wi-medal` |
| §8 Admin | `empty-moderation` |
| Spec 3 Notifications | `empty-notifications` |
| Launch/marketing | `marketing-og-card`, `marketing-instagram-square`, `marketing-instagram-story`, `marketing-banner-wide`, `logo-badge-workouts` |

## Adding new assets

Keep the recipe: 400x300 viewBox for empty states, 480x320 for heroes, prefixed ids, brand palette, CSS vars with light fallbacks for theme colors, fixed gold/peach accents, `<title>` + `role="img"`, transform/opacity-only animation with a reduced-motion block, one fun detail per scene. Marketing assets: fixed colors, gradients allowed, dark charcoal background family.
