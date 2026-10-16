# Achievement badges: brief for the Blender agent

Mizan shows one badge per achievement. Today every achievement has no picture (`icon_url` is empty), so the list shows text only. This brief gives a local agent everything it needs to render the set in Blender and hand it to an administrator for upload.

## Where the badges appear

- The Achievements page (`/achievements`): a 28px square at the left of each row, on cream paper in light mode and dark paper in dark mode. Earned badges show in full colour. Badges still to earn show at 50% opacity and in grayscale (CSS, not a second file).
- The admin editor (`/admin/achievements/[id]/edit`): a preview beside the upload control.
- Later: the Android app and the unlock toast, at up to 96px.

One file per achievement serves all of these. Render large and let the browser scale down.

## Deliverables

For every achievement in the catalog below, one PNG with transparency:

- `achievement-<key>.png`, 512 × 512 px, sRGB, straight alpha, under 60 KB after `oxipng -o4` or equivalent.
- A second copy at 128 × 128 px with the same name under `128/`, for the toast and the admin preview.
- A contact sheet `contact-sheet.png` with every badge at 28 px on cream `#FBFAF7` and on dark `#14161A`, so a person can judge legibility at the real size in both themes before anything is uploaded.

Also three level marks, same format: `level-bronze.png`, `level-silver.png`, `level-gold.png`. The level ladder is Rookie (0), Bronze (100), Silver (500), Gold (1500), Platinum (5000 points); Rookie has no mark, and Platinum reuses the gold mark with a cooler metal.

## The catalog

Keys are the stable part of each achievement's id (`md5('mizan-achievement:<key>')` for the second batch; the first batch is seeded by position and keyed here by name). Points and thresholds are context for how "big" a badge should feel, not text to render.

| Key | Category | Name | What the person did | Points |
| --- | --- | --- | --- | --- |
| first-meal | nutrition | First Meal | Logged the first meal | 10 |
| getting-started | nutrition | Getting Started | Logged 10 meals | 25 |
| century-club | nutrition | Century Club | Logged 100 meals | 100 |
| half-grand | nutrition | Half Grand | Logged 500 meals | 250 |
| kitchen-opener | nutrition | Kitchen Opener | Created the first recipe | 15 |
| chefs-shelf | nutrition | Chef's Shelf | Created 10 recipes | 75 |
| three-day-runner | consistency | Three-Day Runner | Logged meals 3 days in a row | 15 |
| one-week-warrior | consistency | One-Week Warrior | 7 days in a row | 50 |
| two-week-titan | consistency | Two-Week Titan | 14 days in a row | 100 |
| monthly-master | consistency | Monthly Master | 30 days in a row | 250 |
| quarter-year-habit | consistency | Quarter-Year Habit | 90 days in a row | 500 |
| first-rep | workout | First Rep | Logged the first workout | 10 |
| gym-regular | workout | Gym Regular | Logged 10 workouts | 50 |
| iron-habit | workout | Iron Habit | Logged 50 workouts | 200 |
| workout-week | workout | Workout Week | Workouts 7 days in a row | 75 |
| first-pr | workout | Personal Best | Set the first personal record | 25 |
| volume | workout | Heavy Lifter | Lifted 100 000 kg in total | 200 |
| template-ten | workout | Program Regular | Completed 10 template workouts | 75 |
| baseline-set | milestone | Baseline Set | Recorded the first body measurement | 10 |
| body-tracker | milestone | Body Tracker | Recorded 10 body measurements | 50 |
| bronze-badge | milestone | Bronze Badge | Reached 100 points | 0 |
| silver-badge | milestone | Silver Badge | Reached 500 points | 0 |
| gold-badge | milestone | Gold Badge | Reached 1500 points | 0 |
| first-share | social | First Workout Shared | Shared the first workout | 20 |
| ten-shares | social | Training Out Loud | Shared 10 workouts | 75 |
| first-follower | social | First Follower | Gained the first follower | 20 |
| ten-followers | social | Training Circle | Gained 10 followers | 100 |
| reactions | social | Hype Crew | Reacted to 10 feed items | 40 |
| comments | social | Training Partner | Commented on 5 feed items | 40 |

Administrators can add, change and delete achievements later, so the agent should keep its Blender file parametric: one scene, one script, a table of keys, and a motif and tier per key. A new achievement is one new row.

## Design language

Mizan is a ledger, not a game. Read [DESIGN.md](../DESIGN.md) and [Language.dc.html](Language.dc.html) first. The rules that matter for a 3D badge:

- **One object per badge, on nothing.** No plinth, no frame, no sparkle, no text. The row already has the name; the badge is a glyph.
- **Matte, not shiny.** Clay or anodised metal with a soft roughness around 0.5. No chrome, no glow, no emission, no lens flare. A single soft key light from the upper left, a weak fill, and ambient occlusion. The look should sit next to a 14px Archivo label without shouting.
- **The palette is the product's.** Brand verdigris `oklch(49% 0.076 187)` (about `#1F6F68`) for the "live" accent; ink `#1A1815`; warm cream `#FBFAF7`; the two data hues, amber `oklch(52% 0.10 74)` and clay `oklch(54% 0.126 39)`. Tiers use metals: bronze `#9A6B3F`, silver `#A8ABB0`, gold `#C9A227`, platinum `#B9C4CC`. Nothing saturated beyond these. No pure black or pure white anywhere, because the badge must read on both papers.
- **Silhouette first.** Every badge must be identifiable at 28 px. Test every render at that size on both backgrounds before moving on. If two badges in a family are told apart only by colour, change the shape.
- **Families share a form.** Nutrition badges are a bowl family, consistency badges a stacked-calendar or stepped-bar family, workout badges a plate-and-bar family, milestone badges a measuring-tape or scale family, social badges a two-shape (people) family. Within a family the tier grows the object: more plates on the bar, more steps, a fuller bowl. Thresholds 1, 10, 100, 500 should read as a clear progression.
- **Camera.** Orthographic, three-quarter view from above (about 30° elevation, 35° yaw), the same for every badge, so the set lines up in a list. Fill about 80% of the frame.
- **Corners.** The product's radius is 3 px. Bevels should be small and crisp, not pillowy.

## Rendering on the RTX 3060

- Blender 4.2 LTS or newer, run headless: `blender -b badges.blend --python render_badges.py -- --out ./out`.
- Cycles with the OptiX device (`bpy.context.preferences.addons['cycles'].preferences.compute_device_type = 'OPTIX'`, then enable the GPU and set `scene.cycles.device = 'GPU'`). 12 GB of VRAM is plenty for these scenes; keep textures procedural so nothing is loaded from disk.
- 256 samples with the OptiX denoiser is enough for a matte object on transparency. Film transparent on, colour management Standard view transform (not Filmic or AgX) so the brand colours come out as specified.
- Render 512 × 512, then downscale to 128 with Lanczos in the compositor or with Pillow. Do not render at 28 px directly.
- Export PNG RGBA 8-bit. Strip metadata. Run `oxipng -o4 --strip all` on the results.
- Budget: a full set of 32 renders should finish in a few minutes on this card. If it does not, lower samples before touching resolution.

## Delivery and upload

1. Put the files in a folder with the contact sheet at the top. A person approves the sheet first.
2. Upload each badge through the admin editor: More → Achievements is the user view; `/admin/achievements` lists the catalog, each row opens the editor, and the editor has an **Upload badge** control beside the icon address. The file goes to the `achievements` storage folder, which only administrators may write to.
3. Or by API, signed in as an administrator: `POST /api/Uploads/image?folder=Achievements` with the file as `file`, then `PUT /api/Achievements/{id}` with the returned URL as `iconUrl`.
4. Check `/achievements` in light and dark mode on a phone. The badge must be readable at 28 px and must not pull the eye away from the name.

## Out of scope

No animation, no 3D in the browser, no per-user variants, no separate "locked" renders. The page handles locked badges itself.
