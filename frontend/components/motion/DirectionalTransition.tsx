import { ViewTransition, type ReactNode } from "react";

/** Transition types for links that go deeper (a list to one item) or back up. */
export const NAV_FORWARD = ["nav-forward"];
export const NAV_BACK = ["nav-back"];

/**
 * Page content that slides a short way in the direction of travel when a link
 * carries NAV_FORWARD or NAV_BACK. Any other navigation, the browser's back
 * button and refreshes swap it with no motion. Wrap the page, not the layout:
 * layouts persist, so their content never enters or exits.
 */
export default function DirectionalTransition({
  children,
}: {
  children: ReactNode;
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
        default: "none",
      }}
      default="none"
    >
      {children}
    </ViewTransition>
  );
}
