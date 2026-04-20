import { cn } from "@/lib/utils";

interface MacroRingProps {
	label: string;
	current: number;
	target: number | null | undefined;
	unit: string;
	tone: "calories" | "protein" | "carbs" | "fat";
	size?: number;
}

const toneStroke: Record<MacroRingProps["tone"], string> = {
	calories: "var(--color-burnt-peach-500)",
	protein: "var(--color-verdigris-500)",
	carbs: "var(--color-tuscan-sun-500)",
	fat: "var(--color-sandy-brown-500)",
};

export default function MacroRing({
	label,
	current,
	target,
	unit,
	tone,
	size = 132,
}: MacroRingProps) {
	const validTarget = target && target > 0 ? target : null;
	const pct = validTarget ? Math.min(current / validTarget, 1) : 0;
	const stroke = size >= 200 ? 14 : 10;
	const radius = (size - stroke) / 2;
	const circumference = 2 * Math.PI * radius;
	const offset = circumference * (1 - pct);
	const remaining = validTarget ? Math.max(0, validTarget - current) : 0;
	const over = validTarget ? Math.max(0, current - validTarget) : 0;
	const isOver = over > 0;

	return (
		<div className={cn("flex flex-col items-center gap-2 text-center")}>
			<div className="relative" style={{ width: size, height: size }}>
				<svg width={size} height={size} className="-rotate-90">
					<circle
						className="ring-track"
						cx={size / 2}
						cy={size / 2}
						r={radius}
						fill="transparent"
						strokeWidth={stroke}
					/>
					<circle
						cx={size / 2}
						cy={size / 2}
						r={radius}
						fill="transparent"
						stroke={toneStroke[tone]}
						strokeWidth={stroke}
						strokeLinecap="round"
						strokeDasharray={circumference}
						strokeDashoffset={offset}
						style={{ transition: "stroke-dashoffset 900ms cubic-bezier(0.16, 1, 0.3, 1)" }}
					/>
				</svg>
				<div className="absolute inset-0 flex flex-col items-center justify-center leading-tight">
					<p className="text-2xl font-bold text-charcoal-blue-900 dark:text-charcoal-blue-50">
						{Math.round(current).toLocaleString()}
					</p>
					<p className="text-[10px] uppercase tracking-[0.18em] text-charcoal-blue-500 dark:text-charcoal-blue-400">
						{unit}
					</p>
					{validTarget && (
						<p className="mt-1 text-[10px] font-medium text-charcoal-blue-400 dark:text-charcoal-blue-400">
							of {Math.round(validTarget).toLocaleString()}
						</p>
					)}
				</div>
			</div>
			<div>
				<p className="text-xs font-semibold uppercase tracking-[0.16em] text-charcoal-blue-600 dark:text-charcoal-blue-300">
					{label}
				</p>
				<p
					className={cn(
						"text-[11px]",
						isOver
							? "text-burnt-peach-600 dark:text-burnt-peach-400"
							: "text-charcoal-blue-400 dark:text-charcoal-blue-400"
					)}
				>
					{validTarget
						? isOver
							? `+${Math.round(over)} ${unit} over`
							: `${Math.round(remaining)} ${unit} left`
						: "No goal set"}
				</p>
			</div>
		</div>
	);
}
