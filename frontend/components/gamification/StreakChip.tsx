"use client";

import { useEffect, useState } from "react";
import { AnimatedIcon } from "@/components/ui/animated-icon";
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
				"inline-flex items-center gap-2 rounded-2xl px-4 py-2 text-sm font-semibold text-white",
				atRisk ? "bg-amber-500" : "streak-gradient",
			)}
			title={
				isActiveToday
					? "Logged today. The streak is safe."
					: "Log something before the day ends to keep it."
			}
		>
			<AnimatedIcon name="flame" size={16} />
			<span>{count}-day streak</span>
			{atRisk && left && (
				<span className="rounded-full bg-white/20 px-2 py-0.5 text-xs font-medium tabular-nums">
					{left} left
				</span>
			)}
		</div>
	);
}
