import { addTransitionType, startTransition } from "react";
import { LIST_CHANGE } from "./list";

/** Applies a change to a list (state, a router refresh, or both) as a LIST_CHANGE transition. */
export function changeList(update: () => void) {
  startTransition(() => {
    addTransitionType(LIST_CHANGE);
    update();
  });
}
