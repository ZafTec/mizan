import { cn } from "@/lib/utils";

export type PillTone = "neutral" | "good" | "warn" | "bad" | "info";

const TONES: Record<PillTone, string> = {
	neutral: "bg-charcoal-blue-100 text-charcoal-blue-700 dark:bg-white/10 dark:text-charcoal-blue-200",
	good: "bg-verdigris-100 text-verdigris-800 dark:bg-verdigris-500/15 dark:text-verdigris-300",
	warn: "bg-amber-100 text-amber-800 dark:bg-amber-500/15 dark:text-amber-300",
	bad: "bg-red-100 text-red-800 dark:bg-red-500/15 dark:text-red-300",
	info: "bg-blue-100 text-blue-800 dark:bg-blue-500/15 dark:text-blue-300",
};

/** Status in a table cell. One shape, five tones, no per-page colour decisions. */
export default function Pill({
	tone = "neutral",
	children,
	className,
}: {
	tone?: PillTone;
	children: React.ReactNode;
	className?: string;
}) {
	return (
		<span
			className={cn(
				"inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium",
				TONES[tone],
				className,
			)}
		>
			{children}
		</span>
	);
}
