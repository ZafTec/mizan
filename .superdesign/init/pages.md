# Page Dependency Trees

Complete recursive local-import trees for the 10 most important pages. These are the files you must pass via `--context-file` when designing a page to reproduce it faithfully. External imports (`react`, `next/*`, `recharts`, `lucide-*`, `@radix-ui/*`, `sonner`, `twMerge`, etc.) are **not** listed.

Convention:
- Each node is a local path beginning with `@/` (alias for `frontend/`) or relative.
- Indentation = import depth. Duplicates (files reached by multiple paths) are only expanded once.
- `[shared]` marks a file that shows up in many pages, include it once per context set.

Shared foundational files (used by nearly every page):
- `@/components/ui/animated-icon.tsx`
- `@/components/Loading/index.tsx` (+ `.module.css`)
- `@/components/Navbar/index.tsx` → `@/components/Navbar/NavbarContent.tsx`
- `@/app/globals.css`
- `@/app/layout.tsx`
- `@/lib/utils.ts`, `@/lib/auth.ts`, `@/lib/auth-client.ts`, `@/lib/api.client.ts`, `@/lib/api.server.ts`, `@/lib/toast.ts`
- `@/helper/session.ts`, `@/helper/FormErrorHandler.ts`
- `@/public/logo_transparent.png`

---

## 1. Home / Landing, `app/page.tsx` → `/`

```
@/app/page.tsx
├── @/public/placeholder-recipe.jpg
├── @/components/DailyOverviewChart/index.tsx
│   ├── @/lib/api.client.ts
│   └── @/components/ui/animated-icon.tsx
├── @/components/Dashboard/DashboardStats.tsx
│   ├── @/lib/api.client.ts
│   └── @/data/achievement.ts
├── @/components/ui/animated-icon.tsx   [shared]
├── @/helper/session.ts
├── @/data/recipe.ts
│   ├── @/lib/api.server.ts
│   └── @/lib/logger.ts
├── @/components/Landing/FeatureSection.tsx
│   └── @/components/ui/animated-icon.tsx
├── @/components/Landing/TestimonialCard.tsx
│   └── @/components/ui/animated-icon.tsx
└── @/components/Landing/CTASection.tsx
    └── @/components/ui/animated-icon.tsx
```

Also wrapped by: `@/app/layout.tsx` → `@/components/Navbar/*`, `@/components/ui/sonner.tsx`, `@/components/appearance/AppearanceSync.tsx`, footer inline.

---

## 2. Dashboard snapshot, lives inside `/` but is worth isolating

When designing the authed home, the authed-only blocks are:

```
@/components/Dashboard/DashboardStats.tsx
├── @/lib/api.client.ts → @/lib/api.ts
├── @/data/achievement.ts → @/lib/api.client.ts, @/lib/api.server.ts
└── (Link from next/link)

@/components/DailyOverviewChart/index.tsx
├── @/lib/api.client.ts
├── @/components/ui/animated-icon.tsx
└── @/components/DailyOverviewChart/PieChart.tsx (if imported, verify)
```

Pair these with `app/page.tsx` when recreating the authed dashboard-snapshot card.

---

## 3. Meals / Food diary, `app/(dashboard)/meals/page.tsx` → `/meals`

```
@/app/(dashboard)/meals/page.tsx
├── @/data/meal.ts                            (getMeal, getDailyTotals, getNutritionRange, deleteMeal, types MealEntry, DailyNutritionSummary)
│   ├── @/lib/api.client.ts
│   └── @/lib/api.server.ts
├── @/data/goal.ts                            (getCurrentGoal, getGoalHistory, UserGoal type)
│   ├── @/lib/api.client.ts
│   └── @/lib/api.server.ts
├── @/data/achievement.ts                     (getStreak, StreakInfo)
│   └── @/lib/api.client.ts
├── @/lib/auth-client.ts                      (useSession)
├── @/components/DeleteConfirmModal.tsx
│   └── @/components/Loading/index.tsx
└── @/components/Loading/index.tsx
    └── @/components/Loading/index.module.css
```

Guard: `@/app/(dashboard)/layout.tsx` → `@/helper/session.ts`.

---

## 4. Recipes list, `app/recipes/page.tsx` → `/recipes`

```
@/app/recipes/page.tsx
├── @/public/placeholder-recipe.jpg
├── @/data/recipe.ts → @/lib/api.server.ts, @/lib/logger.ts
├── @/helper/session.ts
├── @/lib/utils/list-params.ts
├── @/components/Pagination.tsx
├── @/app/recipes/RecipeFilters.tsx
│   └── @/lib/hooks/useDebounce.ts
└── @/components/illustrations/AppFeatureIllustration.tsx
```

---

## 5. Recipe detail, `app/recipes/[recipeId]/page.tsx` → `/recipes/:id`

```
@/app/recipes/[recipeId]/page.tsx
├── @/data/recipe.ts → @/lib/api.server.ts, @/lib/logger.ts
├── @/helper/session.ts
├── @/public/placeholder-recipe.jpg
└── @/app/recipes/[recipeId]/RecipeActions.tsx
    ├── @/lib/api.client.ts
    ├── @/lib/toast.ts
    └── @/components/ConfirmationModal.tsx
```

---

## 6. Goal setup, `app/(dashboard)/goal/page.tsx` → `/goal`

```
@/app/(dashboard)/goal/page.tsx
├── @/components/FieldError/index.tsx
│   └── @/helper/FormErrorHandler.ts
├── @/components/Loading/index.tsx
├── @/helper/FormErrorHandler.ts  (EMPTY_FORM_STATE)
└── @/data/goal.ts                (createGoal server action, getCurrentGoal)
    ├── @/lib/api.client.ts
    └── @/lib/api.server.ts
```

Notable behaviors to preserve: TDEE calculator (Mifflin-St Jeor) collapsible card, unit toggles (kg/lb, cm/ft), auto-protein suggestion from P/Cal ratio, warnings panel for non-blocking hints.

---

## 7. Goal dashboard, `app/(dashboard)/goal/dashboard/page.tsx` → `/goal/dashboard`

```
@/app/(dashboard)/goal/dashboard/page.tsx
├── @/lib/api.client.ts → @/lib/api.ts
├── @/types/goal.ts     (GoalData type, may live at @/types/goal)
└── @/components/Loading/index.tsx
```

Only one real child (Loading). The page renders Recharts `RadialBarChart`, `LineChart`, `BarChart` with `ReferenceLine` for goal targets. Macro color palette is inlined (`MACRO_COLORS`).

Empty state includes `next/image` referencing `/assets/dashboard-overview.svg` from `frontend/public/assets/`.

---

## 8. Workouts, `app/(dashboard)/workouts/page.tsx` → `/workouts`

```
@/app/(dashboard)/workouts/page.tsx
├── @/lib/api.client.ts
├── @/lib/toast.ts
└── @/components/Loading/index.tsx
```

Self-contained page (no other local components). Three-tab interface (History / Log Workout / Exercise Library), inlined category badge function, inline render helpers for Strength/Cardio/Flexibility fields.

---

## 9. Trainers discovery, `app/(dashboard)/trainers/page.tsx` → `/trainers`

```
@/app/(dashboard)/trainers/page.tsx
├── @/lib/auth-client.ts
├── @/lib/api.client.ts
├── @/lib/toast.ts
├── @/components/Loading/index.tsx
├── @/lib/hooks/useDebounce.ts
├── @/types/api-contracts.ts     (TrainerPublicDto, TrainerPublicPagedResultDto, getPagedItems)
└── @/components/illustrations/AppFeatureIllustration.tsx
```

---

## 10. Profile hub, `app/(dashboard)/profile/page.tsx` → `/profile`

```
@/app/(dashboard)/profile/page.tsx
├── @/lib/auth-client.ts
├── @/components/Loading/index.tsx
├── @/components/ui/animated-icon.tsx
└── @/lib/api/profile.ts                    (getProfileObservations, ProfileObservations type)
    └── @/lib/api.client.ts
```

---

## Bonus: Meals / Add manual, `app/(dashboard)/meals/add/page.tsx` → `/meals/add`

```
@/app/(dashboard)/meals/add/page.tsx
├── @/components/FieldError/index.tsx → @/helper/FormErrorHandler.ts
├── @/components/Loading/index.tsx
├── @/data/meal.ts → @/lib/api.client.ts, @/lib/api.server.ts
└── @/helper/FormErrorHandler.ts
```

## Bonus: Admin dashboard, `app/admin/page.tsx` → `/admin`

```
@/app/admin/page.tsx
├── @/lib/auth.ts                 (BetterAuth server config)
├── @/db/client.ts                (Drizzle client, frontend auth schema only)
├── @/db/schema.ts                (users, sessions tables)
├── @/app/admin/LiveAuditLog.tsx
│   └── @/data/audit.ts → @/lib/api.client.ts
└── @/data/audit.ts → @/lib/api.client.ts
```

Admin page uses inline `StatCard` and `QuickActionCard` function components (not shared). Uses Tailwind `bg-card` / `border` utilities rather than the glass `.card` class, so it looks stylistically different from the rest of the app, **worth flagging when redesigning for visual consistency.**

---

## Global wrapping chain (always include for any page)

```
@/app/layout.tsx                           Root shell
├── @/app/globals.css                      All theme tokens + utility classes
├── @/components/Navbar/index.tsx          Sticky top pill nav
│   └── @/components/Navbar/NavbarContent.tsx
│       ├── @/lib/auth.ts (User type)
│       ├── @/lib/auth-client.ts (signOut)
│       ├── @/lib/toast.ts
│       ├── @/lib/utils.ts (cn)
│       └── @/components/ui/animated-icon.tsx
├── @/components/ui/animated-icon.tsx      For footer social icons
├── @/components/ui/sonner.tsx             Global toast
├── @/components/appearance/AppearanceSync.tsx
├── @/helper/session.ts                    getUserOptionalServer
├── @/lib/appearance.ts                    getServerAppearanceClasses / getAppearanceSettingsFromUser
├── @/public/logo_transparent.png
└── `remixicon/fonts/remixicon.css`        Icon font used everywhere (ri-* classes)
```

---

## Frequently-imported shared chunks, pass these alongside page context

| File | What it gives |
|------|---------------|
| `@/components/Pagination.tsx` | Page-number buttons for listing routes |
| `@/components/SortableHeader.tsx` | Column header with asc/desc toggle |
| `@/components/FieldError/index.tsx` | Inline form error display, used in every server-action form |
| `@/components/ConfirmationModal.tsx` | Generic confirm dialog |
| `@/components/DeleteConfirmModal.tsx` | Variant with destructive styling |
| `@/components/illustrations/AppFeatureIllustration.tsx` | Empty-state SVG illustrations (variants: `recipes`, `trainers`, etc.) |
| `@/components/Landing/*` | Feature / Testimonial / CTA sections on home |
| `@/lib/utils.ts` | `cn`, `cva` helpers |
| `@/lib/toast.ts` | `appToast` (wraps sonner with error-object handling) |
| `@/lib/hooks/useDebounce.ts` | Search input debouncing |
| `@/lib/utils/list-params.ts` | `parseListParams`, `buildListUrl` for paginated lists |
| `@/helper/FormErrorHandler.ts` | `FormState`, `EMPTY_FORM_STATE`, `getFieldError` (server-action form plumbing) |
| `@/types/api-contracts.ts` + `@/types/api.generated.ts` | OpenAPI-generated DTOs (backend) |
