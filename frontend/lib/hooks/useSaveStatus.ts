"use client";

import { useCallback, useEffect, useRef, useState } from "react";

export type SaveStatus = "idle" | "saving" | "saved" | "failed";

const WORDS: Record<SaveStatus, string> = {
  idle: "",
  saving: "Saving…",
  saved: "Saved",
  failed: "Not saved",
};

export const saveStatusText = (status: SaveStatus) => WORDS[status];

/**
 * Feedback for a setting that saves as it changes: "Saving…" while the write
 * runs, "Saved" for a moment after it, "Not saved" when it failed. The work's
 * own error still reaches the caller, which says why.
 */
export function useSaveStatus(holdMs = 2500) {
  const [status, setStatus] = useState<SaveStatus>("idle");
  const timer = useRef<number | undefined>(undefined);
  useEffect(() => () => window.clearTimeout(timer.current), []);

  const track = useCallback(
    async <T>(work: () => Promise<T>): Promise<T> => {
      window.clearTimeout(timer.current);
      setStatus("saving");
      try {
        const result = await work();
        setStatus("saved");
        timer.current = window.setTimeout(() => setStatus("idle"), holdMs);
        return result;
      } catch (error) {
        setStatus("failed");
        throw error;
      }
    },
    [holdMs],
  );

  return { status, text: saveStatusText(status), track };
}
