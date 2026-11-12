"use client";

import { useEffect, useState } from "react";

/**
 * The section whose heading was last scrolled past the top third of the
 * screen, for an anchor list that marks where the reader is. The URL's hash
 * wins on the first render. Pass the same ids each render (a module constant
 * or a memoised list); the observer follows them when they change.
 */
export function useSectionInView<T extends string>(ids: readonly T[]): T {
  const [active, setActive] = useState<T>(ids[0]);
  const key = ids.join("|");
  useEffect(() => {
    const list = key.split("|") as T[];
    const fromHash = list.find((id) => `#${id}` === window.location.hash);
    // eslint-disable-next-line react-hooks/set-state-in-effect -- one read of the URL on mount
    if (fromHash) setActive(fromHash);
    if (typeof IntersectionObserver === "undefined") return;
    const observer = new IntersectionObserver(
      (entries) => {
        const visible = entries
          .filter((entry) => entry.isIntersecting)
          .sort((a, b) => a.boundingClientRect.top - b.boundingClientRect.top);
        const first = visible[0]?.target.id as T | undefined;
        if (first) setActive(first);
      },
      { rootMargin: "0px 0px -66% 0px" },
    );
    for (const id of list) {
      const element = document.getElementById(id);
      if (element) observer.observe(element);
    }
    return () => observer.disconnect();
  }, [key]);
  return active;
}
