/**
 * Rows that the person adds, removes or clears. A list change runs in a transition tagged
 * LIST_CHANGE (see `changeList`): the rows that left fade out, the rows that arrived fade in,
 * and the rows around them move to their new places. Any other update to the same list
 * (a new day, a refresh, a navigation) changes it in place.
 *
 * Spread `rowMotion` on a `<ViewTransition key={id}>` that wraps each row, with no element
 * between the list and the boundary. This module has no React imports, so server pages use it.
 */
export const LIST_CHANGE = "list-change";

export const rowMotion = {
  enter: { [LIST_CHANGE]: "row-enter", default: "none" },
  exit: { [LIST_CHANGE]: "row-exit", default: "none" },
  update: { [LIST_CHANGE]: "auto", default: "none" },
  default: "none",
} as const;
