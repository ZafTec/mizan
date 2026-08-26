"use client";

import { useState } from "react";
import type { HardConstraint } from "@/data/admin/ai";

/**
 * The half of the guardrails nobody can edit here, shown next to the half they
 * can. A constraint nobody can see gets worked around by people who do not
 * know it exists (docs/REFOCUS.md §12).
 */
export default function HardConstraints({
	preamble,
	constraints,
}: {
	preamble: string;
	constraints: HardConstraint[];
}) {
	const [showPreamble, setShowPreamble] = useState(false);

	return (
		<section className="rounded-3xl border border-charcoal-blue-200 bg-charcoal-blue-50/60 p-5 dark:border-white/10 dark:bg-white/[0.03]">
			<div className="flex flex-wrap items-baseline justify-between gap-2">
				<h2 className="text-sm font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
					Enforced in code
				</h2>
				<button
					type="button"
					onClick={() => setShowPreamble((v) => !v)}
					className="text-xs text-charcoal-blue-500 underline-offset-2 hover:underline dark:text-charcoal-blue-400"
				>
					{showPreamble ? "Hide" : "Show"} preamble
				</button>
			</div>

			<p className="mt-1 text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
				Not editable here, and not enforced by the prompt below. Changing any
				of these is a code change and a deploy.
			</p>

			<ul className="mt-3 space-y-2">
				{constraints.map((c) => (
					<li key={c.title} className="text-xs">
						<span className="font-medium text-charcoal-blue-900 dark:text-charcoal-blue-100">
							{c.title}
						</span>
						<span className="text-charcoal-blue-600 dark:text-charcoal-blue-300">
							{" — "}
							{c.detail}
						</span>
						<span className="block text-charcoal-blue-400 dark:text-charcoal-blue-500">
							{c.enforcedBy}
						</span>
					</li>
				))}
			</ul>

			{showPreamble && (
				<pre className="mt-3 max-h-64 overflow-auto whitespace-pre-wrap rounded-2xl bg-white p-3 font-mono text-[11px] leading-relaxed text-charcoal-blue-700 dark:bg-charcoal-blue-950 dark:text-charcoal-blue-300">
					{preamble}
				</pre>
			)}
		</section>
	);
}
