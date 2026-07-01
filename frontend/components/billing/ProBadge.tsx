import { AnimatedIcon } from "@/components/ui/animated-icon";

export function ProBadge({ className = "" }: { className?: string }) {
  return (
    <span
      className={`inline-flex shrink-0 items-center gap-1 rounded-full bg-gradient-to-r from-brand-500 to-brand-600 px-2 py-0.5 text-[10px] font-semibold uppercase tracking-[0.1em] text-white shadow-sm shadow-brand-500/30 ${className}`}
    >
      <AnimatedIcon name="sparkles" size={10} aria-hidden="true" />
      Pro
    </span>
  );
}
