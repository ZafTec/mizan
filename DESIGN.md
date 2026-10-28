---
name: Mizan
description: A quiet, ruled interface for daily records.
colors:
  primary: "oklch(21.02% 0.007 78)"
  primary-hover: "oklch(33.75% 0.013 88)"
  teal: "oklch(49.28% 0.076 187)"
  teal-dark: "oklch(77.18% 0.097 186)"
  background: "oklch(98.5% 0.004 91)"
  background-dark: "oklch(17.2% 0.005 80)"
  foreground-dark: "oklch(95.53% 0.007 89)"
  card: "#ffffff"
  muted: "oklch(93.47% 0.013 87)"
  muted-foreground: "oklch(43.65% 0.012 82)"
  muted-foreground-dark: "oklch(76.46% 0.021 88)"
  border: "oklch(90.43% 0.013 87)"
  input: "oklch(86.16% 0.016 86)"
  border-dark: "oklch(29.97% 0.012 84)"
  destructive: "oklch(49.51% 0.142 31)"
  destructive-dark: "oklch(71.27% 0.13 30)"
  chart-carbs: "oklch(51.69% 0.1 74)"
  chart-carbs-dark: "oklch(74.56% 0.114 76)"
  chart-fat: "oklch(54% 0.126 39)"
  chart-fat-dark: "oklch(71% 0.118 46)"
typography:
  headline: { fontFamily: "Archivo, system-ui, sans-serif", fontSize: "34px", fontWeight: 600, lineHeight: 1.15, letterSpacing: "-0.022em" }
  title: { fontFamily: "Archivo, system-ui, sans-serif", fontSize: "20px", fontWeight: 600, lineHeight: 1.3 }
  body: { fontFamily: "Archivo, system-ui, sans-serif", fontSize: "14px", fontWeight: 400, lineHeight: "20px" }
  label: { fontFamily: "Archivo, system-ui, sans-serif", fontSize: "14px", fontWeight: 500, lineHeight: "20px" }
rounded:
  md: "3px"
spacing:
  "2": "8px"
  "3": "12px"
  "4": "16px"
  "5": "20px"
  "6": "24px"
  "8": "32px"
components:
  button-primary: { backgroundColor: "{colors.primary}", textColor: "{colors.background}", rounded: "{rounded.md}", padding: "12px 20px" }
  button-primary-hover: { backgroundColor: "{colors.primary-hover}" }
  button-secondary: { backgroundColor: "transparent", textColor: "{colors.primary-hover}", rounded: "{rounded.md}", padding: "12px 20px" }
  button-ghost: { backgroundColor: "transparent", textColor: "{colors.muted-foreground}", rounded: "{rounded.md}", padding: "12px 20px" }
  input: { backgroundColor: "{colors.card}", textColor: "{colors.primary}", rounded: "{rounded.md}", padding: "12px 16px" }
  card: { backgroundColor: "{colors.card}", rounded: "{rounded.md}" }
  navigation-link: { textColor: "{colors.muted-foreground}", rounded: "{rounded.md}", padding: "0 14px", height: "46px" }
---
# Design System: Mizan

## Overview

**Creative North Star: "The daily ledger"**

A warm, restrained interface built from readable type, aligned data, and ruled surfaces. Hierarchy comes from weight, spacing, and contrast.

Product context lives in [README.md](README.md). [The stylesheet](frontend/app/globals.css) is the implementation authority; keep these extracted tokens aligned with it.

**Key Characteristics:**

- One type family, tabular figures, and static line icons.
- Flat cream surfaces, near-black ink, and restrained teal interaction states.

## Colors

Primary actions use ink; teal identifies links, focus, and active states. Cream, white, and muted neutral surfaces establish grouping. Amber and clay distinguish data series; destructive red identifies errors and destructive actions.

Dark mode retains the same hierarchy with dark paper, lighter ink, teal, and data colors. Use the existing semantic CSS variables to inherit the theme.

## Typography

**The Single Family Rule.** Use Archivo for headings, body text, controls, and numbers. Weight and tracking create hierarchy.

The headline steps down to (30px) below the small breakpoint. Supporting copy uses (12–14px), with medium and semibold weights for labels and emphasis. Use tabular figures for comparable counts, quantities, and measurements.

## Layout

Spacing follows the recorded steps, with thin rules separating repeated records. Content containers remain fluid within the shell's maximum width (1240px); log views use a narrower maximum (1060px).

At (1024px), navigation changes from a bottom bar to a sidebar. Below (640px), content stacks and uses side gutters (20px). Account for the bottom bar and device safe area when reserving scroll space.

## Elevation & Depth

**The Flat Sheet Rule.** Separate surfaces with borders, spacing, and tonal changes. Surfaces have no shadows or hover lift.

Dialogs use a dimmed backdrop and a defined border. Interaction feedback changes color or opacity; button presses briefly scale to (0.985). Respect reduced motion.

## Shapes

Controls and panels use the shared corner token; structural rules are (1px). Circles remain appropriate for avatars and status dots, and switches retain their sliding pill shape. Static Lucide line icons use the shared [Icon component](frontend/components/ui/icon.tsx); avoid decorative icon containers.

## Components

- **Buttons:** ink primary, outlined secondary, and quiet ghost controls share spacing and corners. Hover changes tone or border; disabled controls reduce opacity. Icon buttons provide targets (44px square).
- **Fields:** bordered white surfaces become ink surfaces in dark mode. Labels remain visible; focus strengthens the border, and keyboard focus uses a teal outline (2px, offset 3px). Errors use destructive color with text.
- **Dropdowns:** every choice from a list uses [Select](frontend/components/ui/select.tsx), never a native `<select>`. A bordered trigger (44px) opens a ruled list (40px rows) with a teal check on the chosen option; the keyboard-active row takes the hairline fill and a 2px ink bar on its left. Lists longer than 15 options, time zones and price regions carry a search box and an "n of total" count.
- **Cards and rows:** cards have a thin border and no elevation; repeated records use separators and aligned values.
- **Navigation:** [AppShell](frontend/components/Layout/AppShell.tsx) uses a muted fill and stronger type for the active sidebar item; active mobile links use teal. Keep text labels alongside static icons.
- **Logging sheet:** [LogSheet](frontend/components/Layout/LogSheet.tsx) becomes a centered dialog above the small breakpoint. Keep its title and close control visible, trap focus, and restore focus when it closes. The [meal form](frontend/components/logging/MealLogForm.tsx) leads with a box to describe the meal in words or a photo; the proposal lands in the meal as editable rows. Search and recipes are one step away, and the recipe list opens in its own view inside the sheet (favorites, then often logged, then the rest) and returns to the same meal. The form scrolls its contents while its total and save action stay visible.
- **Assistant:** a panel, not a page. **Ask Mizan** in the top bar opens [AiChat](frontend/components/ai/AiChat.tsx) docked at 420px on the right of a wide screen, or as a full-height sheet on a phone. The header holds the title, earlier conversations (a view inside the panel), a new conversation, and close. Until the person chooses what it may use, the permission step stands where the suggestions would; a refused tool call shows one **Allow** button under the reply. Photos open full size from the transcript and before they are sent. An administrator viewing the site as a user sees a banner on every screen with the time left and one way back.
- **Secondary pages:** the pages under More share one anatomy, drawn in [design/Secondary.dc.html](design/Secondary.dc.html): a title with one line of lead, up to three numbers in a `facts-strip` (the same ruled strip as the nutrition totals), lists as `ledger-row` rows (name left, numbers right, a chevron when the row opens something) or the [DataTable](frontend/components/ui/data-table/DataTable.tsx) when the page sorts and pages, an empty state that is one sentence, and one filled action. No icon tiles, stat cards, badge pills or mascot illustrations.
- **Settings:** [/profile/settings](frontend/app/%28dashboard%29/profile/settings/page.tsx) is one ruled page of sections (account, your day and prices, assistant, connected apps, appearance, sign-in and security, your data) with an anchor list that marks the section in view. Each setting is a `setting-row` (label column, control column) or a [ToggleRow](frontend/components/ui/toggle-row.tsx) switch; changes save as they are made and say **Saved** beside the control (`useSaveStatus`). `#assistant` holds the assistant's switches in the same words as the panel's permission step.
- **Profile:** [/profile](frontend/app/%28dashboard%29/profile/page.tsx) is the avatar, the name, "Member since" and the plan, **Edit profile**, a three-part strip (streak, goal, achievements), the Manage rows and **Sign out**.
- **Set entry:** [ActiveWorkout](frontend/app/%28dashboard%29/workouts/ActiveWorkout.tsx) aligns set, weight, reps, and completion controls in ruled rows. Completion uses a teal wash and an explicit control state.

## Do's and Don'ts

- **Do** reuse the semantic theme variables and existing component styles.
- **Do** pair color states with readable labels or explicit control states.
- **Do** keep numeric columns aligned and actions reachable on small screens.
- **Don't** add display typefaces, gradients, surface shadows, or looping icon motion.
- **Don't** turn every record into a card or every label into a badge.
- **Don't** add an icon that only decorates. An icon stays when it is the control and its meaning is universal: close, delete on a row, paging and date chevrons, the magnifier in a search field, a dropdown chevron, the checkbox tick, a provider logo, the primary Log entry plus, and a chevron on a row that opens something. Glyphs beside headings or button labels, empty-state and alert glyphs, icon tiles, category glyphs in lists and arrows inside buttons are removed.
