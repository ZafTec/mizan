"use client";

import { useEffect, useState } from "react";
import { Icon } from "@/components/ui/icon";
import { cn } from "@/lib/utils";

interface StreakChipProps {
	count: number;
	/** Local midnight, from the server. The only thing that knows the user's zone.  */
	resetsAt?: string | null;
	isActiveToday: boolean;
	atRisk: boolean;
}

function remaining(resetsAt: string): string | null {
	const ms = Date.parse(resetsAt) - Date.now();
	if (!Number.isFinite(ms) || ms <= 0) return null;

	const hours = Math.floor(ms / 3_600_000);
	const minutes = Math.floor((ms % 3_600_000) / 60_000);
	return hours > 0 ? `${hours}h ${minutes}m` : `${minutes}m`;
}

/**
 * A streak with its deadline attached.
 *
 * The flame on its own never said when the day ends, and the day it was
 * counting was UTC rather than the user's - so someone three hours east could
 * log every night and watch the number stay put. Both halves of that are
 * fixed; this is the half you can see.
 */
export default function StreakChip({ count, resetsAt, isActiveToday, atRisk }: StreakChipProps) {
	// Starts empty and fills in on the client. The server has no business
	// rendering a countdown - it would be a second stale on arrival and a
	// hydration mismatch every time the minute turned between the two.
	const [left, setLeft] = useState<string | null>(null);

	useEffect(() => {
		if (!resetsAt) return;
		const timer = setInterval(() => setLeft(remaining(resetsAt)), 1_000);
		return () => clearInterval(timer);
	}, [resetsAt]);

	if (count <= 0) return null;

	return (
		<div
			className={cn(
				"inline-flex items-center gap-2 text-sm font-semibold",
				atRisk ? "text-tuscan-sun-700 dark:text-tuscan-sun-400" : "streak-gradient",
			)}
			title={
				isActiveToday
					? "Logged today. The streak is safe."
					: "Log something before the day ends to keep it."
			}
		>
			<Icon name="flame" size={16} />
			<span>{count}-day streak</span>
			{atRisk && left && <span className="num text-xs font-medium opacity-80">{left} left</span>}
		</div>
	);
}
