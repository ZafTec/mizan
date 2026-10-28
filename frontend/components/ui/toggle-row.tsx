import { cn } from "@/lib/utils";

/**
 * A setting that is on or off: a label, a sentence saying what it does, and a
 * switch, on a hairline row. The whole row is one `role="switch"` button, so a
 * click anywhere or Space and Enter flip it, and screen readers read the label
 * and sentence. `status` sits beside the switch ("Saved") and is announced.
 */
export function ToggleRow({
  label,
  description,
  checked,
  disabled,
  status,
  onChange,
}: {
  label: string;
  description: string;
  checked: boolean;
  disabled?: boolean;
  status?: string | null;
  onChange: (checked: boolean) => void;
}) {
  return (
    <div className="flex items-center gap-3 border-t border-border">
      <button
        type="button"
        role="switch"
        aria-checked={checked}
        disabled={disabled}
        onClick={() => onChange(!checked)}
        className="flex min-h-16 min-w-0 flex-1 items-center gap-4 py-3.5 text-left disabled:cursor-not-allowed disabled:opacity-60"
      >
        <span className="min-w-0 flex-1">
          <span className="block text-[15px] font-medium">{label}</span>
          <span className="mt-0.5 block text-[13px] leading-snug text-muted-foreground">
            {description}
          </span>
        </span>
        <span
          aria-hidden="true"
          className={cn(
            "relative h-[26px] w-11 shrink-0 rounded-full transition-colors motion-reduce:transition-none",
            checked ? "bg-brand-700 dark:bg-brand-300" : "bg-input",
          )}
        >
          <span
            className={cn(
              "absolute top-[3px] h-5 w-5 rounded-full transition-transform motion-reduce:transition-none",
              checked
                ? "translate-x-[21px] bg-white dark:bg-charcoal-blue-950"
                : "translate-x-[3px] bg-foreground",
            )}
          />
        </span>
      </button>
      {status !== undefined && (
        <span
          role="status"
          className="w-12 shrink-0 text-right text-xs text-brand-700 dark:text-brand-300"
        >
          {status}
        </span>
      )}
    </div>
  );
}
