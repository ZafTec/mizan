# Route Map (Next.js App Router)

All routes under `frontend/app/`. Root layout (`app/layout.tsx`) wraps everything — Navbar, max-w-7xl main container, branded footer. Route-group layouts in this codebase are **auth guards only**, not visual wrappers.

## Layout chain

```
app/layout.tsx                 Root: <html>, Navbar, Footer, Toaster, page-transition container
├── (auth)/                    No layout (just route group)
│   ├── login/layout.tsx       Adds <Suspense> for useSearchParams
│   └── verifyemail/layout.tsx Adds <Suspense> for useSearchParams
├── (dashboard)/layout.tsx     Redirects to /login if no session
├── admin/layout.tsx           Redirects to / if not admin role
```

There is **no sidebar / dashboard chrome** layer — every authed page sits directly inside the root shell.

---

## Public routes (root layout only)

| URL | File | Summary |
|-----|------|---------|
| `/` | `app/page.tsx` | Landing + dashboard. Shows hero CTA for anon users, `DashboardStats` + `DailyOverviewChart` for authed. Always renders quick actions, popular recipes, CTA/feature/testimonial sections (anon only). |
| `/recipes` | `app/recipes/page.tsx` | Public recipe browse. Paginated list with `RecipeFilters`, hero stats, quick "Create Recipe" CTA for authed. |
| `/recipes/[recipeId]` | `app/recipes/[recipeId]/page.tsx` | Public recipe detail. Hero image, nutrition card (kcal/P/C/F/fiber/P-ratio), ingredients list, numbered instructions, tags, owner actions (edit/favorite/delete via `RecipeActions`). |
| `/privacy` | `app/privacy/page.tsx` | Static privacy policy. |
| `/terms` | `app/terms/page.tsx` | Static terms of service. |

---

## Auth route group `(auth)`

No layout wrapping except Suspense for the two pages that read `useSearchParams`.

| URL | File | Summary |
|-----|------|---------|
| `/login` | `app/(auth)/login/page.tsx` | Email/password login form. |
| `/register` | `app/(auth)/register/page.tsx` | Sign-up form with name/email/password. |
| `/forgot-password` | `app/(auth)/forgot-password/page.tsx` | Request password reset email. |
| `/reset-password` | `app/(auth)/reset-password/page.tsx` | Set new password via token. |
| `/verify` | `app/(auth)/verify/page.tsx` | Email verification flow. |
| `/verifyemail` | `app/(auth)/verifyemail/page.tsx` | Variant of verify with token-from-search-params. |

---

## Dashboard route group `(dashboard)` — requires authentication

All guarded by `app/(dashboard)/layout.tsx`. Visual chrome still comes from root layout.

### Meals & Nutrition

| URL | File | Summary |
|-----|------|---------|
| `/meals` | `app/(dashboard)/meals/page.tsx` | Food diary. Date paginator, streak pill, goal progress bars (kcal/P/C/F), three Recharts (calorie trend + macro stacked bars + P/Cal trend + optional fiber), meal list with per-entry delete. |
| `/meals/add` | `app/(dashboard)/meals/add/page.tsx` | Manual meal log form (name, meal type, timestamp, macros) with server-action submission and warning hints. |
| `/meals/add/[recipeId]` | `app/(dashboard)/meals/add/[recipeId]/page.tsx` | Log a meal derived from a recipe. Uses `AddMealFromRecipe` component with serving-size selector that scales macros live. |
| `/goal` | `app/(dashboard)/goal/page.tsx` | Set nutrition goals. Collapsible TDEE calculator (Mifflin-St Jeor, metric/imperial), macro target inputs, body composition + target date fields, warnings panel. |
| `/goal/dashboard` | `app/(dashboard)/goal/dashboard/page.tsx` | Goal tracking dashboard. Day-range selector (7/14/30), today's radial chart, calorie-trend line with goal `ReferenceLine`, macro-breakdown bar chart, recent entries list. |
| `/goal/progress` | `app/(dashboard)/goal/progress/page.tsx` | Log today's actual macros against goal. |

### Recipes (auth-only)

| URL | File | Summary |
|-----|------|---------|
| `/recipes/add` | `app/(dashboard)/recipes/add/page.tsx` | Create recipe form (ingredients, instructions, visibility, image upload via Cloudinary). |
| `/recipes/[recipeId]/edit` | `app/(dashboard)/recipes/[recipeId]/edit/page.tsx` | Edit existing recipe (owner-only, enforced server-side). |
| `/recipes/favorites` | `app/(dashboard)/recipes/favorites/page.tsx` | User's saved favorites list. |
| `/suggestions` | `app/(dashboard)/suggestions/page.tsx` | AI coach recipe suggestions based on saved goal + pantry. |
| `/suggestions/regenerate` | `app/(dashboard)/suggestions/regenerate/page.tsx` | Trigger regeneration of AI suggestions with new prompt. |

### Ingredients / Foods

| URL | File | Summary |
|-----|------|---------|
| `/ingredients` | `app/(dashboard)/ingredients/page.tsx` | Paginated user ingredient library with search. |
| `/ingredients/add` | `app/(dashboard)/ingredients/add/page.tsx` | Create custom food (per-100g macros). |
| `/ingredients/[ingredientId]` | `app/(dashboard)/ingredients/[ingredientId]/page.tsx` | Food detail view. |
| `/ingredients/[ingredientId]/edit` | `app/(dashboard)/ingredients/[ingredientId]/edit/page.tsx` | Edit custom food. |

### Meal Plans

| URL | File | Summary |
|-----|------|---------|
| `/meal-plan` | `app/(dashboard)/meal-plan/page.tsx` | List of active/archived meal plans. |
| `/meal-plan/create` | `app/(dashboard)/meal-plan/create/page.tsx` | Create new meal plan (name, date range). |
| `/meal-plan/add` | `app/(dashboard)/meal-plan/add/page.tsx` | Add recipe(s) to a plan on specific day+slot. |
| `/meal-plan/[id]` | `app/(dashboard)/meal-plan/[id]/page.tsx` | Meal plan detail — calendar grid with drag-assigned recipes per day. |
| `/meal-plan/[id]/edit` | `app/(dashboard)/meal-plan/[id]/edit/page.tsx` | Edit plan metadata. |
| `/meal-plan/shopping-list` | `app/(dashboard)/meal-plan/shopping-list/page.tsx` | Aggregated shopping list for the active plan. |

### Fitness

| URL | File | Summary |
|-----|------|---------|
| `/workouts` | `app/(dashboard)/workouts/page.tsx` | Three-tab shell: History (collapsible workout cards), Log Workout (form with exercise search), Exercise Library (grid + category filter). |
| `/exercises` | `app/(dashboard)/exercises/page.tsx` | Alternate exercise library view. |
| `/body-measurements` | `app/(dashboard)/body-measurements/page.tsx` | Body measurement log: `AddMeasurementForm`, `MeasurementChart` with target-weight `ReferenceLine`, paginated + sortable table. |
| `/achievements` | `app/(dashboard)/achievements/page.tsx` | Earned vs unearned achievements grid + streak summary. |

### Trainer features

| URL | File | Summary |
|-----|------|---------|
| `/trainers` | `app/(dashboard)/trainers/page.tsx` | Trainer discovery grid. Search input, trainer cards (avatar, specialties, client count), "Send Request" button per card, how-it-works info card. |
| `/trainers/my-trainer` | `app/(dashboard)/trainers/my-trainer/page.tsx` | Client's view of their assigned trainer (profile, messaging link). |
| `/trainers/requests` | `app/(dashboard)/trainers/requests/page.tsx` | Pending incoming/outgoing trainer requests. |
| `/trainer` | `app/(dashboard)/trainer/page.tsx` | Trainer dashboard (client list, pending requests). Requires `trainer` role. |
| `/trainer/clients/[id]` | `app/(dashboard)/trainer/clients/[id]/page.tsx` | Trainer's view of a specific client's nutrition/progress. |

### Profile

| URL | File | Summary |
|-----|------|---------|
| `/profile` | `app/(dashboard)/profile/page.tsx` | Profile hub — stat cards (role/joined/streak/goal), quick-link grid (Goals/Diary/Workouts/Measurements/Achievements/MCP), current-read insight rows. |
| `/profile/settings` | `app/(dashboard)/profile/settings/page.tsx` | Account, appearance (light/dark/compact/reduce-motion), data export, delete account. |
| `/profile/sessions` | `app/(dashboard)/profile/sessions/page.tsx` | Active sessions list with revoke per-device. |
| `/profile/mcp` | `app/(dashboard)/profile/mcp/page.tsx` | MCP token management + usage analytics for the Mizan MCP server. |

---

## Admin route group — requires admin role

Guarded by `app/admin/layout.tsx`. No separate sidebar — stays inside root Navbar.

| URL | File | Summary |
|-----|------|---------|
| `/admin` | `app/admin/page.tsx` | Admin dashboard — 6 stat cards (users/trainers/banned/ingredients/audit/sessions), Recent Users list, `LiveAuditLog` feed, Quick Actions grid with emoji tiles. |
| `/admin/users` | `app/admin/users/page.tsx` | User management table. |
| `/admin/users/[id]` | `app/admin/users/[id]/page.tsx` | Single user detail / role + ban controls. |
| `/admin/users/create` | `app/admin/users/create/page.tsx` | Admin-create-user form. |
| `/admin/sessions` | `app/admin/sessions/page.tsx` | System-wide active sessions. |
| `/admin/relationships` | `app/admin/relationships/page.tsx` | Trainer-client relationships oversight. |
| `/admin/ingredients` | `app/admin/ingredients/page.tsx` | Public ingredient moderation queue. |
| `/admin/ingredients/add` | `app/admin/ingredients/add/page.tsx` | Create public ingredient. |
| `/admin/ingredients/[id]/edit` | `app/admin/ingredients/[id]/edit/page.tsx` | Edit public ingredient. |
| `/admin/exercises` | `app/admin/exercises/page.tsx` | Exercise library management. |
| `/admin/recipes` | `app/admin/recipes/page.tsx` | Recipe moderation view. |
| `/admin/audit-logs` | `app/admin/audit-logs/page.tsx` | Full audit log browser with filters. |

---

## API routes (not pages — enumerated for completeness)

Under `frontend/app/api/`:
- `/api/auth/[...all]` — BetterAuth catchall
- `/api/csrf` — Double-submit CSRF token
- `/api/health` — Frontend healthcheck

All domain APIs (foods, recipes, meals, workouts, etc.) are **proxied or direct to the .NET backend**, not exposed through Next.js route handlers.

---

## Error & status pages

- `app/error.tsx`, `app/not-found.tsx` — root error boundaries
- `app/(dashboard)/error.tsx`, `app/admin/error.tsx` — group-scoped error boundaries

Skipped in design context; these are minimal error handlers.
