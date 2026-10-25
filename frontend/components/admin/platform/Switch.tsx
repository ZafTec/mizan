"use client";

import { cn } from "@/lib/utils";

/** An on/off control with its state in words beside it, so it reads without colour. */
export default function Switch({
  checked,
  label,
  disabled,
  onChange,
}: {
  checked: boolean;
  label: string;
  disabled?: boolean;
  onChange: (checked: boolean) => void;
}) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      aria-label={label}
      disabled={disabled}
      onClick={() => onChange(!checked)}
      className="inline-flex min-h-11 shrink-0 items-center gap-2 rounded-md px-1 text-sm font-medium disabled:cursor-not-allowed disabled:opacity-60"
    >
      <span
        aria-hidden="true"
        className={cn(
          "relative block h-6 w-11 shrink-0 rounded-full transition-colors",
          checked
            ? "bg-brand-600"
            : "bg-charcoal-blue-300 dark:bg-charcoal-blue-700",
        )}
      >
        <span
          className={cn(
            "absolute top-0.5 left-0 h-5 w-5 rounded-full bg-white transition-transform motion-reduce:transition-none",
            checked ? "translate-x-5" : "translate-x-0.5",
          )}
        />
      </span>
      <span className="w-7 text-left">{checked ? "On" : "Off"}</span>
    </button>
  );
}
