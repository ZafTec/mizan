import { cn } from "@/lib/utils";

export type PillTone = "neutral" | "good" | "warn" | "bad" | "info";

/*
 * "Status is a dot plus a word. Never colour alone - it has to survive
 * grayscale." (Main.dc.html, rule 02). A badge is a second coloured object
 * per row; a dot carries the hue and the word carries the meaning, so the
 * same markup reads correctly in grayscale or print.
 */
const TONES: Record<PillTone, string> = {
	neutral: "text-charcoal-blue-600 dark:text-charcoal-blue-400",
	good: "text-verdigris-700 dark:text-verdigris-400",
	warn: "text-tuscan-sun-700 dark:text-tuscan-sun-400",
	bad: "text-burnt-peach-700 dark:text-burnt-peach-400",
	info: "text-charcoal-blue-700 dark:text-charcoal-blue-300",
};

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
		<span className={cn("inline-flex items-center gap-1.5 text-[13px] font-medium", TONES[tone], className)}>
			<span className="h-[5px] w-[5px] shrink-0 rounded-full bg-current" aria-hidden="true" />
			{children}
		</span>
	);
}
