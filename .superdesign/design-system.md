# Mizan Design System

## Product

Mizan is a responsive nutrition, fitness, workout, coaching, and social application. Authenticated routes use a viewport-height application shell with a collapsible desktop sidebar, a compact top bar, an internal scrolling content region, and a fixed mobile bottom navigation.

## Visual language

- Font: Inter, weights 400 to 800, with system-ui fallback.
- Primary: verdigris (`brand-*`) for primary actions, selected states, focus rings, and workout progress.
- Secondary accents: tuscan sun for streaks and highlights, sandy brown for warm secondary actions, burnt peach for destructive attention and calories.
- Neutral surfaces: charcoal blue scales, light and dark themes.
- Cards use 28px corners, subtle borders, translucent surfaces, backdrop blur, and `--shadow-panel`.
- Large panels use 32px corners. Buttons and inputs use 16px corners.
- Avoid gradients except existing shell ambience, Pro surfaces, and supplied marketing assets.

## Layout

- Desktop shell: 288px expanded sidebar or 80px collapsed, 64px top bar, content max width 1280px.
- Mobile: top bar, content padding 16px, fixed five-item bottom navigation with safe-area padding.
- Page sections use 24px mobile and 32px desktop vertical rhythm.
- Dense workout logging remains one-thumb friendly, with sticky active-workout controls and large tap targets.

## Typography

- Page title: 30px mobile, 36px desktop, semibold, tight tracking.
- Section title: 24px semibold.
- Card title: 16px to 18px semibold.
- Body: 14px to 16px.
- Eyebrows: 12px semibold uppercase with 0.18em tracking.
- Supporting text uses charcoal-blue-500/400.

## Components

- Primary button: filled brand color, white text in light mode, dark charcoal text in dark mode.
- Secondary button: neutral bordered glass surface.
- Destructive button: red filled or red-tinted ghost.
- Every pressable surface has subtle `scale(0.97)` active feedback.
- Inputs use neutral glass surfaces, brand focus border, and a four-pixel translucent focus halo.
- Tabs use a neutral pill track and a raised active pill.
- Empty states use the supplied theme-aware workout/social SVG assets, real explanatory text, and one clear CTA.
- Generic UI icons use Lucide/AnimatedIcon. Workout and muscle domain icons use the supplied sprites.

## Motion

- UI motion stays under 300ms.
- Enter and exit use strong ease-out curves.
- On-screen movement uses ease-in-out.
- Animate transform and opacity only except functional progress arcs.
- Repeated workout interactions should not animate beyond press feedback and state color.
- Rare celebrations may use the supplied animated SVGs.
- Respect `prefers-reduced-motion` and the app's `reduce-motion` setting.

## Workout experience

- Start choices: template, repeat last, or empty workout.
- Active logging centers completed/uncompleted sets, tap-to-cycle reps, per-set editing, rest timing, and draft persistence.
- Set rows must remain legible on mobile without horizontal scrolling.
- Completion leads to a post-workout summary with totals, PRs, streaks, achievements, and optional social sharing.

## Social experience

- Social is free and opt-in.
- Profiles are link-discovered, request/approve, and one-way following.
- Feed cards read live workout data and are retained forever.
- Reaction set is fixed to: 👍, ❤️, 💪, 🔥, 👏, 🎉, 🏆.
- Do not add handle search or public discoverability yet.

## Accessibility

- Interactive targets are at least 40px high, preferably 44px on workout screens.
- All icon-only buttons have accessible labels.
- Decorative illustrations are `aria-hidden`; their messages are real text.
- Color is never the only state indicator.
- Maintain visible keyboard focus and semantic headings.
