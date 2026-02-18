# The Workflow: Scout, Dissect, Construct

## Phase 1: Scout (Inspiration)

Don't rely on imagination. Rely on curation.

-   **The Goal**: Find *one* reference for layout and *one* reference for "vibe" (colors/font).
-   **Sources**:
    -   *Pinterest*: For broad mood boards and "vibes".
    -   *Dribbble/Behance*: For specific component layouts (cards, navbars).
    -   *Awwwards/Godly*: For full-site composition and interaction ideas.

## Phase 2: Dissect (The Box Model)

Before writing component code, analyze the reference image visually.

1.  **Draw Mental Boxes**:
    -   Everything is a box (`div`, `section`, `article`).
    -   Draw a box around the biggest element.
    -   Draw boxes around the smaller elements inside it.
2.  **Identify Relationships**:
    -   Are the boxes stacked vertically? (Flex Column / Block)
    -   Are they side-by-side? (Flex Row / Grid)
    -   Are they floating on top of each other? (Absolute / Fixed)
3.  **Hierarchy Check**:
    -   If a "card" has an image and text, the *card* is the parent. The image and text are siblings.

## Phase 3: Construct (The Order of Operations)

Build in this strict order to avoid CSS spaghetti:

1.  **Structure (HTML/JSX)**:
    -   Write the semantic tags first. No classes yet. Just hierarchy.
2.  **Layout (CSS)**:
    -   Position the boxes. Use `display: grid` or `flex`.
    -   Ignore colors and fonts. Focus on spacing (`gap`, `margin`, `padding`).
3.  **Aesthetics (CSS)**:
    -   Apply typography, colors, backgrounds, and borders.
4.  **Polish (CSS/JS)**:
    -   Add hover states, transitions, and cursor effects last.