# Extractable Components

Reusable components worth registering in the design system. Only **state / navigation / visibility / count** props are listed — not every presentation prop. Hardcoded elements (specific icons, text, brand-coded classes) are called out so the design agent knows what's intentional vs parameterizable.

Categories:
- **layout** — shell, navigation, header/footer
- **basic** — small reusable primitives used across many pages

---

## Layout components

### Navbar

**Source:** `frontend/components/Navbar/index.tsx` (+ `NavbarContent.tsx`)
**Category:** layout
**Description:** Sticky top glass-pill navigation with logo, primary links, user menu, mobile sheet, and sign-out modal.
**Props to extract:**
- `user: User | null` — controls sign-in vs signed-in UI, avatar, role pill, admin link visibility
- (derived from `usePathname`) active link highlight — no external prop needed
- `userMenuOpen: boolean`, `menuOpen: boolean`, `showLogoutModal: boolean` — internal state

**Hardcoded elements:**
- Logo: `@/public/logo_transparent.png` (Mizan wordmark image)
- Brand: "Mizan" wordmark + "ሚዛን • Balance" Amharic tagline
- Primary links: `/ingredients` (Foods), `/recipes` (Recipes), `/meals` (Meals, auth-only), `/admin` (admin-only)
- Account links: `/profile`, `/profile/settings`, `/profile/mcp`, `/profile/sessions`
- Icons (from AnimatedIcon): `search`, `cookingPot`, `flame`, `shieldCheck`, `home`, `user`, `bot`, `lock`, `logout`, `menu`, `x`, `arrowRight`
- Classes: `nav-glass`, `btn-primary`, `btn-secondary`, `btn-danger`, `btn-ghost`, `icon-chip`, `surface-panel` (all from globals.css)
- Logout confirmation copy: "Sign out / Your session will end on this device immediately."

---

### Footer

**Source:** inlined in `frontend/app/layout.tsx` (no dedicated file)
**Category:** layout
**Description:** Three-column glass-panel footer — brand block with logo + Amharic tagline, copyright + privacy/terms links, social icon chips (GitHub, Twitter).
**Props to extract:**
- `year: number` — currently `new Date().getFullYear()`, could be passed in

**Hardcoded elements:**
- Logo image `@/public/logo_transparent.png`
- Brand name + Amharic line ("ሚዛን • Balanced nutrition, training, and coaching.")
- Links: `/privacy`, `/terms`
- Social links: `#` (GitHub + Twitter, both currently placeholders)
- Icons: `github`, `twitter` (AnimatedIcon)
- Classes: `surface-panel`, `icon-chip`, `footer-link`

---

### RootShell (via `app/layout.tsx`)

**Source:** `frontend/app/layout.tsx`
**Category:** layout
**Description:** The entire page chrome — `<html>` with appearance classes, `<body>` flex column, global Toaster, Navbar, max-w-7xl main container with page-transition animation, Footer.
**Props to extract:**
- `user: User | null` (from `getUserOptionalServer()`) — threads into Navbar and appearance classes
- `htmlClasses: string[]` — controls theme mode (`dark`, `compact`, `reduce-motion`)

**Hardcoded elements:**
- Page title/metadata: "Mizan - Balanced Nutrition & Fitness"
- `lang="en"`
- Body selection color: `bg-brand-500/15`
- Remixicon font import (`'remixicon/fonts/remixicon.css'`) — enables all `ri-*` icon classes across pages
- Toaster position: `top-right`

---

### AppShellContainer (just the `<main>` wrapper)

Not a component yet, but worth isolating from `app/layout.tsx`:

```
<main className="grow pb-8">
  <div className="page-transition max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-6 sm:py-8 lg:py-10">
    {children}
  </div>
</main>
```

**Props to extract:** none (static wrapper). The `page-transition` class triggers a one-time enter animation (`fade-up`/translate from `globals.css @keyframes page-enter`).

---

### DashboardGuard

**Source:** `frontend/app/(dashboard)/layout.tsx`
**Category:** layout
**Description:** Invisible server-side guard — redirects unauthenticated users to `/login`. No visual output; wraps children in fragment.
**Props to extract:** none (uses `getUserOptionalServer()` internally).

---

### AdminGuard

**Source:** `frontend/app/admin/layout.tsx`
**Category:** layout
**Description:** Same pattern as DashboardGuard but additionally requires `user.role === "admin"`, else redirects to `/`.
**Props to extract:** none.

---

## Basic / primitive components

### UserAvatar

**Source:** inlined in `frontend/components/Navbar/NavbarContent.tsx`
**Category:** basic
**Description:** Square-rounded 36×36 avatar — renders user's `image` if set, otherwise brand-filled initial letter of email.
**Props to extract:**
- `user: { image?: string | null; name?: string | null; email?: string | null }`
- `size: number` — currently hardcoded `h-9 w-9`

**Hardcoded elements:**
- Ring: `ring-1 ring-brand-500/15`
- Fallback bg: `bg-brand-600` (dark: `bg-brand-500`)
- Border radius: `rounded-2xl`

---

### ProfileAvatar (larger variant)

**Source:** inlined in `frontend/app/(dashboard)/profile/page.tsx`
**Category:** basic
**Description:** 80×80/96×96 profile-page avatar with text-2xl initial. Same pattern as UserAvatar but larger and rounded-[28px].
**Props to extract:**
- `image?: string | null`, `email?: string | null`, `name?: string | null`

---

### NavLink

**Source:** inlined in `frontend/components/Navbar/NavbarContent.tsx`
**Category:** basic
**Description:** Pill-style nav link with icon + label. Active state driven by `usePathname`. Has `mobile` variant with full-width rounded-2xl, icon-chip wrapper.
**Props to extract:**
- `item: { href: string; label: string; icon: AnimatedIconName }`
- `mobile: boolean` — switches between compact desktop and wide mobile layouts
- `onClick?: () => void` — optional close-menu handler

**Hardcoded elements:**
- Active text: `text-slate-950 dark:text-white`
- Inactive text: `text-slate-600 hover:text-slate-950`
- Icon active color: `text-brand-600 dark:text-brand-300`

---

### Card (glass panel)

**Source:** `.card` class in `frontend/app/globals.css` + `frontend/components/ui/card.tsx`
**Category:** basic
**Description:** 28px rounded white/slate-950 glass panel with backdrop-blur(20px) and `--shadow-panel`. Most pages use the CSS class directly rather than the `<Card>` component.
**Props to extract:**
- `hover: boolean` — switches to `.card-hover` (lift + border accent on hover)
- `className` — padding is typically added per-instance (`p-4`, `p-6`, `p-7`, `p-8`)

**Hardcoded elements:**
- Radius: `rounded-[28px]`
- Border: `border-charcoal-blue-200 dark:border-white/10`
- Backdrop blur: 20px
- Shadow: `var(--shadow-panel)`

---

### SurfacePanel

**Source:** `.surface-panel` class in `globals.css`
**Category:** basic
**Description:** Larger variant of Card — 32px radius, blur(22px). Used for the navbar container, big hero panels, the footer wrapper, modal dialogs.
**Props to extract:** none (CSS-only utility).

---

### Button (class-based and React component)

**Source:** `.btn-*` classes in `globals.css` + `frontend/components/ui/button.tsx`
**Category:** basic
**Description:** Primary action button. Two interfaces:
1. `<Button variant="..." size="...">` React component (preferred for new code)
2. `<Link className="btn-primary">` / `<button className="btn-secondary">` legacy CSS classes (used by ~90% of existing pages)

**Props to extract (component):**
- `variant`: `default` | `destructive` | `outline` | `secondary` | `ghost` | `link`
- `size`: `default` | `sm` | `lg` | `icon`
- `asChild`: boolean (Radix Slot pattern for wrapping `<Link>`/`<a>`)

**Variant-class mapping (for legacy usage):**
- `btn-primary` ≈ `<Button variant="default">` (brand-600 filled)
- `btn-secondary` ≈ `<Button variant="outline">` (white bg, border)
- `btn-danger` ≈ `<Button variant="destructive">` (red-600 filled)
- `btn-accent` (sandy-brown filled) — NO equivalent component variant
- `btn-ghost` ≈ `<Button variant="ghost">` (transparent)
- Modifier classes: `btn-sm`, `btn-lg`

---

### Badge (pill label)

**Source:** `frontend/components/ui/badge.tsx` + inline pill patterns across pages
**Category:** basic
**Description:** Colored pill for tags, statuses, counts. Variants: default (primary), secondary, destructive, outline.
**Props to extract:**
- `variant`: default | secondary | destructive | outline
- `className`

**Domain-specific inline pills** (not using the component) — catalog these for meal/recipe pages:
- Calorie chip: `bg-orange-50 text-orange-700` + `ri-fire-line`
- Protein chip: `bg-red-50 text-red-700` + `ri-heart-pulse-line`
- Carbs chip: `bg-amber-50 text-amber-700` + `ri-bread-line`
- Fat chip: `bg-yellow-50 text-yellow-700` + `ri-drop-line`
- Fiber chip: `bg-green-50 text-green-700` + `ri-leaf-line`
- P/Cal chip: `bg-indigo-50 text-indigo-700` + `ri-percent-line`

---

### Eyebrow label

**Source:** `.eyebrow` class in `globals.css`
**Category:** basic
**Description:** Uppercase tracked-out section label with border + backdrop blur. Used above big section headings (e.g. "Daily snapshot" on home, "Profile hub" on profile).
**Props to extract:**
- `icon: AnimatedIconName`
- `label: string`

**Hardcoded elements:**
- Tracking: `0.18em`
- Text size: `text-xs`, font `font-semibold`
- Classes: rounded-full border charcoal-blue-200 backdrop-blur

---

### IconChip

**Source:** `.icon-chip` class in `globals.css`
**Category:** basic
**Description:** Square-rounded icon container with inset highlight. Used in navbar mobile menu, footer social icons, profile quick-link cards, etc.
**Props to extract:**
- `size` — typically `h-9 w-9`, `h-11 w-11`, `h-12 w-12`
- `tone` — default/brand/red/accent (wraps class with text-color utility)

**Hardcoded elements:**
- Border: `border-charcoal-blue-200 dark:border-white/10`
- Inset: `inset 0 1px 0 rgba(255, 255, 255, 0.5)`
- Blur: 16px

---

### StatCard

**Source:** inlined in `frontend/app/(dashboard)/profile/page.tsx` AND `frontend/app/admin/page.tsx` (two different versions)
**Category:** basic
**Description:** Label + big value + helper-text rounded panel.
**Props to extract:**
- `label: string`
- `value: string | number`
- `helper?: string`
- `link?: string` (admin variant) — renders a `linkText` affordance

Note: the two implementations diverge — profile uses glass-card styling, admin uses plain `bg-card border` Tailwind. Consolidate before extracting.

---

### QuickActionCard (home + admin)

**Source:** inline in `frontend/app/page.tsx` (home quick actions) and `frontend/app/admin/page.tsx` (admin tiles)
**Category:** basic
**Description:** Large clickable card with colored icon tile, title, short description. Icon uses AnimatedIcon on home, emoji on admin.
**Props to extract:**
- `href: string`
- `title: string`
- `description: string`
- `icon: AnimatedIconName | string (emoji)`
- `iconClass: string` (color token for the icon tile — e.g. `bg-brand-600`, `bg-accent-600`, `bg-slate-900`)

**Hardcoded elements:**
- Tile: `h-12 w-12 rounded-2xl` with white icon + brand-coded bg
- Card class: `card-hover stagger-item group p-6 sm:p-7` (home) vs `bg-card rounded-lg border p-6 hover:border-primary` (admin)

---

### MacroProgressBar

**Source:** inlined multiple times — `frontend/app/(dashboard)/meals/page.tsx` (Daily Goals grid) and `frontend/app/(dashboard)/goal/dashboard/page.tsx` (Today's Progress macro list)
**Category:** basic
**Description:** Label + `actual / target` numeric + thin colored fill bar.
**Props to extract:**
- `label: string` (Calories/Protein/Carbs/Fat/Fiber)
- `actual: number`
- `target: number`
- `color: string` (hex for bar fill)
- `unit: string` ("kcal" | "g")

**Hardcoded elements:**
- Track: `h-2 bg-slate-100 dark:bg-slate-800 rounded-full overflow-hidden`
- Fill: `h-full transition-all duration-500` with bg via inline style
- Macro color map: calories=`#f97316`, protein=`#ef4444`, carbs=`#f59e0b`, fat=`#eab308`, fiber=`#22c55e`

---

### DateNavigator

**Source:** inlined in `frontend/app/(dashboard)/meals/page.tsx`
**Category:** basic
**Description:** Prev/Next arrow buttons + formatted date label centered between them. Navigates via query-string `?date=YYYY-MM-DD`.
**Props to extract:**
- `date: string` (ISO YYYY-MM-DD)
- `onPrev: () => void`
- `onNext: () => void`
- `onToday?: () => void`

**Hardcoded elements:**
- Classes: `flex items-center rounded-xl border border-slate-200 bg-white p-1 shadow-sm`
- Icons: `ri-arrow-left-s-line`, `ri-arrow-right-s-line`

---

### RangeSelector (7/14/30-day toggle)

**Source:** inlined in `frontend/app/(dashboard)/meals/page.tsx` and `goal/dashboard/page.tsx`
**Category:** basic
**Description:** Segmented pill group to pick a time range for the history charts.
**Props to extract:**
- `options: number[]` — currently `[7, 14, 30]`
- `value: number`
- `onChange: (days: number) => void`

**Hardcoded elements:**
- Container: `flex gap-1 rounded-lg bg-slate-100 p-1 dark:bg-slate-900/80`
- Active: `bg-white text-brand-600 shadow` (light) / `bg-slate-950 text-brand-300` (dark)

---

### StreakPill

**Source:** inlined in `frontend/app/(dashboard)/meals/page.tsx`
**Category:** basic
**Description:** Small orange fire-icon pill showing `<n> day streak`.
**Props to extract:**
- `days: number` — hidden when 0

**Hardcoded elements:**
- Classes: `inline-flex items-center gap-1 rounded-2xl bg-orange-100 px-2.5 py-0.5 text-sm font-medium text-orange-700 dark:bg-orange-500/15 dark:text-orange-300`
- Icon: `ri-fire-fill`

---

### EmptyState

**Source:** inlined across pages (meals, recipes, workouts, goal dashboard, trainers)
**Category:** basic
**Description:** Centered icon or illustration + heading + description + optional CTA button. Pattern is consistent; worth extracting.
**Props to extract:**
- `icon?: ReactNode` or `illustrationVariant?: "recipes" | "trainers" | ...`
- `title: string`
- `description: string`
- `ctaHref?: string`
- `ctaLabel?: string`
- `ctaIcon?: string`

Two common implementations:
1. Large `<i className="ri-*" />` 4xl icon in a rounded-2xl bg-slate-100 tile
2. `<AppFeatureIllustration variant="..." />` SVG

---

### TabPillGroup

**Source:** inlined in `frontend/app/(dashboard)/workouts/page.tsx` (History / Log / Library)
**Category:** basic
**Description:** Rounded chip strip with active state highlighted by white bg + shadow. Different from `@/components/ui/tabs.tsx` (which uses Radix) — most page tab switching uses this inline pattern.
**Props to extract:**
- `tabs: { value: string; label: string; icon: string }[]`
- `active: string`
- `onChange: (value: string) => void`

**Hardcoded elements:**
- Container: `flex gap-1 p-1 bg-slate-100 dark:bg-slate-800 rounded-xl w-fit`
- Active: `bg-white dark:bg-slate-900 text-slate-900 dark:text-slate-100 shadow-sm`

---

### CategoryBadge (workouts)

**Source:** inlined as `categoryBadge()` function in `frontend/app/(dashboard)/workouts/page.tsx`
**Category:** basic
**Description:** Color-coded category pill: Strength=brand, Cardio=red, Flexibility=amber, Balance=indigo.
**Props to extract:**
- `category: string`

---

### Pagination

**Source:** `frontend/components/Pagination.tsx`
**Category:** basic
**Description:** Page-number navigation used on recipes list, ingredients, meal-plan, body-measurements, admin tables.
**Props to extract:**
- `currentPage: number`
- `totalPages: number`
- `totalCount: number`
- `pageSize: number`
- `baseUrl: string` (with trailing `?` or `&`)

Read source for full API.

---

### Illustration (AppFeatureIllustration)

**Source:** `frontend/components/illustrations/AppFeatureIllustration.tsx`
**Category:** basic
**Description:** Inline SVG illustration with named variants used in empty states and hero panels.
**Props to extract:**
- `variant: "recipes" | "trainers" | ...` (read source for full list)
- `className: string`
