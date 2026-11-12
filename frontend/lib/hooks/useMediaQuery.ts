"use client";

import { useSyncExternalStore } from "react";

/** Below the small breakpoint: sheets come up from the bottom instead of floating. */
export const NARROW_SCREEN = "(max-width: 639px)";

/** Whether a media query matches; false on the server and in the first client render. */
export function useMediaQuery(media: string) {
  return useSyncExternalStore(
    (changed) => {
      const query = window.matchMedia?.(media);
      query?.addEventListener("change", changed);
      return () => query?.removeEventListener("change", changed);
    },
    () => window.matchMedia?.(media).matches ?? false,
    () => false,
  );
}

export function useNarrowScreen() {
  return useMediaQuery(NARROW_SCREEN);
}
