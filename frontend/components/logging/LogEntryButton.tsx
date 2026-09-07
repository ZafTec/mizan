"use client";

import type { ButtonHTMLAttributes } from "react";
import type { RecipeResult } from "./logging";

export type LogKind = "meal" | "workout" | "measurement";
export type LogEntryOptions = {
  kind?: LogKind;
  date?: string;
  mealType?: string;
  recipe?: RecipeResult;
};
export const LOG_ENTRY_EVENT = "mizan:log-entry";

export function openLogEntry(options: LogEntryOptions = {}) {
  window.dispatchEvent(
    new CustomEvent<LogEntryOptions>(LOG_ENTRY_EVENT, { detail: options }),
  );
}

export function LogEntryButton({
  kind,
  date,
  mealType,
  recipe,
  children,
  onClick,
  ...props
}: LogEntryOptions & ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button
      type="button"
      {...props}
      onClick={(event) => {
        onClick?.(event);
        if (!event.defaultPrevented)
          openLogEntry({ kind, date, mealType, recipe });
      }}
    >
      {children ?? "Log an entry"}
    </button>
  );
}

export default LogEntryButton;
