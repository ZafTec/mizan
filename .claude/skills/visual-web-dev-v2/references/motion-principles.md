# Motion & Interaction Principles

Animation should feel "physical," not linear.

## 1. Staggering
Never animate a list of items all at once.

-   **Concept**: If loading a list of 5 cards, card #2 should start 100ms after card #1.
-   **Why**: It guides the eye and feels more organic.
-   **Tooling**: `transition-delay` in CSS, or `stagger` properties in animation libraries.

## 2. Inertia (Smooth Scroll)
Standard browser scrolling is "jerky" (pixels per step). Creative sites often use Inertia.

-   **Concept**: The scroll position "lerps" (linearly interpolates) to the target. It feels like the page has weight.
-   **Warning**: Do not hijack scroll for the sake of it. Use libraries that maintain accessibility (like Lenis).

## 3. Parallax & Mouse Tracking
Depth is created when layers move at different speeds.

-   **Mouse Parallax**: Elements move slightly in the opposite direction of the mouse.
-   **Scroll Parallax**: Background elements move slower than foreground elements.

## 4. Easing
Real objects don't start and stop instantly.

-   **Avoid**: `linear` ease.
-   **Prefer**: `ease-out` for entrances (fast start, slow stop). `ease-in-out` for continuous motion.
-   **Elastic**: For playful elements, allow values to "overshoot" slightly before settling.