import { Icon } from "@/components/ui/icon";

export function ProBadge({ className = "" }: { className?: string }) {
	return (
		<span
			className={`inline-flex shrink-0 items-center gap-1 text-[10px] font-semibold uppercase tracking-[0.14em] text-verdigris-700 dark:text-verdigris-400 ${className}`}
		>
			<Icon name="sparkles" size={10} aria-hidden="true" />
			Pro
		</span>
	);
}
