/**
 * A setting that is on or off: a label, a sentence saying what it does, and a
 * switch. The whole row is one `role="switch"` button, so a click anywhere or
 * Space and Enter flip it, and screen readers read the label and sentence.
 */
export function ToggleRow({
  label,
  description,
  checked,
  disabled,
  onChange,
}: {
  label: string;
  description: string;
  checked: boolean;
  disabled?: boolean;
  onChange: (checked: boolean) => void;
}) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      disabled={disabled}
      onClick={() => onChange(!checked)}
      className="flex w-full items-center justify-between gap-4 rounded-3xl border border-charcoal-blue-200 bg-white p-4 text-left transition-colors hover:border-charcoal-blue-300 disabled:cursor-not-allowed dark:border-white/10 dark:bg-charcoal-blue-950 dark:hover:border-white/20"
    >
      <div className="min-w-0">
        <p className="font-medium text-charcoal-blue-900 dark:text-charcoal-blue-100">
          {label}
        </p>
        <p className="mt-1 text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
          {description}
        </p>
      </div>
      <span
        className={`relative h-6 w-11 shrink-0 rounded-full transition-colors ${
          checked
            ? "bg-brand-600"
            : "bg-charcoal-blue-300 dark:bg-charcoal-blue-700"
        }`}
      >
        <span
          className={`absolute top-0.5 h-5 w-5 rounded-full bg-white transition-transform ${
            checked ? "translate-x-5" : "translate-x-0.5"
          }`}
        />
      </span>
    </button>
  );
}
