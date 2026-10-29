import { ViewTransition, type ReactNode } from "react";

/** Transition types for links that go deeper (a list to one item) or back up. */
export const NAV_FORWARD = ["nav-forward"];
export const NAV_BACK = ["nav-back"];

/**
 * Page content that slides a short way in the direction of travel when a link
 * carries NAV_FORWARD or NAV_BACK. Any other navigation, the browser's back
 * button and refreshes swap it with no motion. Wrap the page, not the layout:
 * layouts persist, so their content never enters or exits.
 *
 * In `loading.tsx`, pass `fallback`: when the page's data arrives the skeleton
 * fades out under it. That reveal is its own transition and carries no type.
 */
export default function DirectionalTransition({
  children,
  fallback = false,
}: {
  children: ReactNode;
  fallback?: boolean;
}) {
  return (
    <ViewTransition
      enter={{
        "nav-forward": "nav-forward",
        "nav-back": "nav-back",
        default: "none",
      }}
      exit={{
        "nav-forward": "nav-forward",
        "nav-back": "nav-back",
        default: fallback ? "skeleton-exit" : "none",
      }}
      default="none"
    >
      {children}
    </ViewTransition>
  );
}

/**
 * A title that travels from a list row to the page it opens, and back. Both ends
 * use the same `name`; the link must reach a page that is already prefetched, or
 * the page's skeleton stands in and the title simply appears.
 */
export function SharedTitle({
  name,
  children,
}: {
  name: string;
  children: ReactNode;
}) {
  return (
    <ViewTransition name={name} share="text-morph" default="none">
      {children}
    </ViewTransition>
  );
}
