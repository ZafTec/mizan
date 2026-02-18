---
name: visual-web-dev-v2
description: A framework-agnostic guide for high-end frontend development. Use this skill when the user wants to build creative, visually stunning websites or components (like landing pages or portfolios) and needs help with the design-to-code mental model, visual effects, or interaction logic.
license: MIT
---

# Visual Web Development (The Hyperplexed Method)

This skill provides a mental model for translating high-fidelity design references into code, regardless of the tech stack (React, Vue, Svelte, Vanilla, etc.).

## Core Philosophy

**Design First, Code Second.**
Do not open your code editor until you have a visual reference. Professional creative development is about "reverse engineering" beauty.

## The Mental Model

1.  **Scout (Find)**: Locate a design that solves your visual problem.
2.  **Dissect (Analyze)**: Break the reference down into bounding boxes.
3.  **Construct (Build)**: Rebuild the boxes with code.

## How to Use This Skill

### When to Reference specific guides:

-   **Starting a project?**
    See `references/workflow.md` for the step-by-step breakdown of how to approach a blank canvas using the "Red Box" method.

-   **Need specific "looks"?**
    See `references/design-patterns.md` for the theory behind Glassmorphism, Neon Glows, Bento Grids, and Grain/Noise textures.

-   **Adding "juice" (interaction)?**
    See `references/motion-principles.md` for non-technical guidelines on Staggering, Parallax, and Smooth Scrolling.

### The "Red Box" Debugging Rule

When layout issues occur in ANY framework:
1.  Apply a 1px red outline to every element.
2.  If you can't see the box, you can't fix the box.
3.  CSS: `* { outline: 1px solid red; }`