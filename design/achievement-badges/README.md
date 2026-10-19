# Mizan achievement badges

The revised set uses realistic folded paper: visible stock thickness, warm pulp edges, overlapping seams, origami gussets, creased faces and procedural fiber grain. Dyed paper uses the product palette; bronze, silver and gold describe pigment rather than a metallic shader. A directional upper-left light and weak fill reveal the folds without adding a ground or backdrop.

The set contains 29 achievement glyphs and three level marks. The packaged PNGs live once, in `frontend/public/badges/512` and `frontend/public/badges/128`, which the app serves as the default badges; `deliverables/` holds the review sheets, the manifest, the render report and the GLBs. `deliverables/contact-sheet.png` shows every glyph at **28 px**, beside a **14 px Archivo** label, on cream and dark paper. Each pair shows earned and the CSS-equivalent grayscale/50%-opacity state. `detail-sheet.png` shows the 128 px copies for inspecting geometry and edges. `silhouette-sheet.png` renders every alpha silhouette in the same ink at 28 px to check whether shape alone distinguishes the marks.

Review the real-size sheet before shipping. The app ships the `128/` files as defaults (see the `SeedAchievementBadges` migration); the 512 px files are for the administrator's **Upload badge** control at `/admin/achievements/[id]/edit` when a record needs a different mark. The manifest maps filenames to the seeded achievement IDs; confirm each record against the current catalog since administrators may rename or remove records.

## Rebuild

Requirements: Blender 4.2 LTS or later, an NVIDIA OptiX GPU, Python with Pillow, oxipng, and an Archivo TTF for the sheet labels. No geometry textures or external models are needed.

From this folder:

```sh
blender -b badges.blend --python render_badges.py -- --out ./deliverables
python render_badges.py --postprocess --out ./deliverables --oxipng /path/to/oxipng --font /path/to/Archivo.ttf
```

Blender writes the raw 512 px renders into `deliverables/`; they are ignored by git. `--postprocess` packages them into `--assets`, which defaults to `../../frontend/public/badges`, and writes the sheets and `manifest.json` into `--out`. Nothing else copies badge files.

`badges.blend` contains one scene, a representative badge, the shared camera/lights, and embedded copies of the script and catalog. The script rebuilds the scene on each run. The adjacent script and catalog are the editable sources. To save changes back into the blend:

```sh
blender -b --python render_badges.py -- --prepare-only --save-blend ./badges.blend --out ./deliverables
```

Add one row to `catalog.json` with `key`, `name`, `category`, `motif`, `tier` and `pigment`. Nutrition motifs include a shallow bowl (`empty-bowl`), bowl on a wide plate (`plate`), two stacked bowls (`filled-bowl`), three stacked bowls (`harvest-bowl`), a recipe bowl and a pot. The consistency `steps` motif produces one through five accordion folds; the fourth tier is bound, and the fifth is bound and upright. Other motifs cover dumbbells, barbells, deep loaded bars, large-plate bars, racks, trap bars, plate stacks, tapes, scales, paired people and level tallies. `seed` is only for the original 20 position-seeded records; later records use `md5('mizan-achievement:<key>')`. Level marks have no achievement ID.

Generate a geometry-only silhouette review before rendering. These commands never run Cycles or overwrite the existing delivery PNGs:

```sh
blender -b --python render_badges.py -- --silhouette-geometry --out ./proof-paper/scope
python render_badges.py --silhouette-preview --out ./proof-paper/scope --font /path/to/Archivo.ttf --keys first-meal,getting-started,century-club,half-grand,three-day-runner,one-week-warrior,two-week-titan,monthly-master,quarter-year-habit,gym-regular,first-pr
```

`proof-paper/scope/silhouette-sheet.png` checks all 32 glyphs at 28 px on both papers. `silhouette-detail.png` enlarges the changed outlines for inspecting folds. After review, pass the same `--keys` list to both the Blender render command and the Python `--postprocess` command. Subset packaging preserves all other PNGs and manifest entries byte-for-byte while rebuilding the three delivery sheets.

Export all 32 compact GLBs, without a render:

```sh
blender -b --python render_badges.py -- --glb ./deliverables/glb
```

Each GLB contains the same joined mesh and orthographic camera, uses Draco mesh compression, and must remain below 80,000 bytes. A viewer needs a Draco decoder. No images or textures are baked or included. glTF cannot encode Blender's procedural fiber/bump nodes, ambient occlusion shader or area lights; the GLBs use the matching base pigment, roughness and paper sheen, and store the Blender procedural settings and area-light setup as scene metadata. The source Blender materials and lighting remain intact.

Rendering uses Cycles, OptiX, 256 samples with adaptive sampling and OptiX denoising, transparent film, Standard color management, 512 px RGBA 8-bit and a 30° elevated/35° yaw orthographic camera. Every glyph fits its projected mesh silhouette to 80% of the frame on the longer axis. Paper roughness varies procedurally from 0.72 to 0.92; metallic is zero. Fine fibers and broader wrinkles use bump shading; every shell has physical thickness and a contrasting uncoated reverse. Geometry is joined into one mesh per badge. No floor, enclosing decoration or baked shadow is present.

Packaging downsizes with Lanczos using premultiplied channels, then exports straight alpha. PNG pixels are encoded in sRGB; metadata is stripped as requested. oxipng runs at `-o4 --strip all` while retaining 8-bit RGBA. If lossless optimization exceeds 60,000 bytes, the packager rounds RGB channels in steps of two to ten code values while preserving alpha and surface texture; palette reduction is a last fallback. It validates all files and records their dimensions, byte sizes, compression choices, alpha bounds and hashes in `manifest.json`.

Rookie has no mark. Bronze, Silver and Gold use one, two and three open folded paper tallies. Platinum reuses the three-tally gold geometry with the `platinum` pigment (`#B9C4CC`); it is not a fourth required file. Changing the pigment requires a rerender because these are raster assets.

After approval, upload through the administrator editor, then check `/achievements` on a phone in both themes. API upload requires an administrator session: `POST /api/Uploads/image?folder=Achievements` with the file as `file`, then `PUT /api/Achievements/{id}` using the returned URL as `iconUrl` **and preserving the other current record fields**. The PUT replaces those fields; do not send only `iconUrl`.
