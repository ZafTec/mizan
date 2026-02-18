# Visual Design Patterns

Common "Hyperplexed-style" visual techniques and how to achieve them conceptually.

## 1. Glassmorphism (The Frosted Look)
Used to create depth and separate layers without solid backgrounds.

-   **Recipe**:
    -   **Transparency**: Low opacity background (e.g., `rgba(255, 255, 255, 0.1)`).
    -   **Blur**: Background blur (e.g., `backdrop-filter: blur(10px)`).
    -   **Border**: A subtle, semi-transparent white border to define edges.

## 2. Magic Borders (The Glow Effect)
Used to highlight specific cards or interactive elements.

-   **Recipe**:
    -   Use a radial gradient that tracks the mouse cursor position.
    -   Apply this gradient to the *border-image* or a pseudo-element behind the content.
    -   As the mouse moves, the "glow" follows the border.

## 3. Bento Grids
A layout style popularized by Apple and modern SaaS.

-   **Concept**: A strictly defined grid where content lives in rounded, distinct "cells".
-   **Recipe**:
    -   Use CSS Grid.
    -   Define a master grid (e.g., 12 columns).
    -   Make items span multiple rows/columns (`grid-column: span 2`).
    -   Gap is critical: Keep it consistent.

## 4. Grain & Noise
Used to remove the "digital sterility" of flat colors.

-   **Recipe**:
    -   Create an SVG filter or use a noise image.
    -   Overlay it on the entire site using `pointer-events: none` and `position: fixed`.
    -   Set opacity very low (3-5%) and blend mode to `overlay`.