# Design canvas sources

The artboards behind the Mizan design-language canvas (phase 18).

Each `.dc.html` is one artboard; `canvas.json` lays them out.

| File | What it is |
|---|---|
| `DirectionA.dc.html` | Direction A — Ledger. The admin jobs table as an editorial ruled sheet. |
| `DirectionB.dc.html` | Direction B — Signal. The same screen as a dark instrument panel. |
| `DirectionC.dc.html` | Direction C — Daylight. The current app with the glass removed. |
| `Main.dc.html` | The table specification: anatomy, the five rules, loading and empty states. Light/dark tweak. |
| `Language.dc.html` | Type, colour, controls, and what the rebuild removes and keeps. |
| `Landing.dc.html` | The landing page revamp. |

The published canvas is built by seeding these into the Claude Design payload;
the seeded `.html` is a build artifact and is gitignored. To change anything,
edit the source here and re-seed — never edit the seeded output.
