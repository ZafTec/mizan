"use client";

import { useMemo } from "react";
import { cn } from "@/lib/utils";

type Row = { kind: "same" | "added" | "removed"; text: string };

/**
 * A line-level diff, which is the granularity a prompt is actually reviewed
 * at. Anything cleverer would need a diff library for a screen that shows two
 * paragraphs of text.
 */
function diff(before: string, after: string): Row[] {
	const a = before.split("\n");
	const b = after.split("\n");
	const kept = new Set(b);
	const introduced = new Set(a);

	const removed: Row[] = a
		.filter((line) => !kept.has(line))
		.map((text) => ({ kind: "removed" as const, text }));

	const rows: Row[] = b.map((text) => ({
		kind: introduced.has(text) ? ("same" as const) : ("added" as const),
		text,
	}));

	return [...removed, ...rows];
}

export default function BodyDiff({
	live,
	draft,
	liveLabel,
	draftLabel,
}: {
	live: string;
	draft: string;
	liveLabel: string;
	draftLabel: string;
}) {
	const rows = useMemo(() => diff(live, draft), [live, draft]);
	const changed = rows.some((r) => r.kind !== "same");

	return (
		<section className="glass-panel space-y-3 p-5">
			<h2 className="text-sm font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
				{liveLabel} → {draftLabel}
			</h2>

			{!changed ? (
				<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
					The body is identical. Any difference is in the soft policy.
				</p>
			) : (
				<div className="overflow-x-auto">
					<pre className="min-w-full font-mono text-[11px] leading-relaxed">
						{rows.map((row, i) => (
							<div
								key={`${row.kind}-${i}`}
								className={cn(
									"whitespace-pre-wrap px-2",
									row.kind === "added" &&
										"bg-verdigris-50 text-verdigris-900 dark:bg-verdigris-500/10 dark:text-verdigris-200",
									row.kind === "removed" &&
										"bg-red-50 text-red-800 line-through dark:bg-red-500/10 dark:text-red-300",
									row.kind === "same" &&
										"text-charcoal-blue-500 dark:text-charcoal-blue-400",
								)}
							>
								{row.kind === "added" ? "+ " : row.kind === "removed" ? "- " : "  "}
								{row.text || " "}
							</div>
						))}
					</pre>
				</div>
			)}
		</section>
	);
}
