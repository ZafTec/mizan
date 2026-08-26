"use client";

import { useState } from "react";
import { cn } from "@/lib/utils";
import type { AiEvalMatrix, AiEvalRun } from "@/data/admin/ai";

const PASSED = 0;
const FAILED = 1;

function currency(micros: number) {
	return `$${(micros / 1_000_000).toFixed(4)}`;
}

function Outcome({ run }: { run: AiEvalRun | undefined }) {
	if (!run) {
		return (
			<span className="rounded-full bg-charcoal-blue-100 px-2 py-0.5 text-[11px] text-charcoal-blue-500 dark:bg-white/10 dark:text-charcoal-blue-400">
				not run
			</span>
		);
	}

	const label =
		run.outcome === PASSED ? "passed" : run.outcome === FAILED ? "failed" : "errored";

	return (
		<span
			className={cn(
				"rounded-full px-2 py-0.5 text-[11px]",
				run.outcome === PASSED
					? "bg-verdigris-100 text-verdigris-800 dark:bg-verdigris-500/15 dark:text-verdigris-300"
					: run.outcome === FAILED
						? "bg-red-100 text-red-800 dark:bg-red-500/15 dark:text-red-300"
						: "bg-amber-100 text-amber-800 dark:bg-amber-500/15 dark:text-amber-300",
			)}
		>
			{label}
		</span>
	);
}

export default function EvalMatrix({
	matrix,
	loading,
	editable,
}: {
	matrix: AiEvalMatrix | null;
	loading: boolean;
	editable: boolean;
}) {
	const [open, setOpen] = useState<string | null>(null);

	if (loading) {
		return (
			<section className="glass-panel p-5 text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
				Loading evals…
			</section>
		);
	}

	if (!matrix) {
		return (
			<section className="glass-panel p-5 text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
				{editable
					? "No results for this text yet. Run the suite."
					: "No results recorded for this version."}
			</section>
		);
	}

	const runs = new Map(matrix.runs.map((r) => [r.caseId, r]));
	const delta =
		matrix.publishedCostMicros === null
			? null
			: matrix.costMicros - matrix.publishedCostMicros;

	return (
		<section className="glass-panel space-y-4 p-5">
			<div className="flex flex-wrap items-baseline justify-between gap-2">
				<h2 className="text-sm font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
					Evals
				</h2>
				<span className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
					{currency(matrix.costMicros)} to run
					{delta !== null && matrix.costMicros > 0 && (
						<>
							{" · "}
							<span
								className={
									delta > 0
										? "text-amber-700 dark:text-amber-300"
										: "text-verdigris-700 dark:text-verdigris-300"
								}
							>
								{delta > 0 ? "+" : ""}
								{currency(delta)} vs live
							</span>
						</>
					)}
				</span>
			</div>

			{matrix.blockedReason && (
				<p className="rounded-2xl border border-amber-300 bg-amber-50 px-3 py-2 text-xs text-amber-800 dark:border-amber-500/30 dark:bg-amber-500/10 dark:text-amber-300">
					{matrix.blockedReason}
				</p>
			)}

			<ul className="space-y-1.5">
				{matrix.cases.map((evalCase) => {
					const run = runs.get(evalCase.id);
					const expanded = open === evalCase.id;

					return (
						<li
							key={evalCase.id}
							className="rounded-xl border border-charcoal-blue-200 dark:border-white/10"
						>
							<button
								type="button"
								onClick={() => setOpen(expanded ? null : evalCase.id)}
								className="flex w-full items-center gap-3 px-3 py-2 text-left"
							>
								{evalCase.isAdversarial && (
									<span
										title="Gates publish"
										className="rounded bg-charcoal-blue-900 px-1.5 py-0.5 text-[10px] font-medium uppercase tracking-wide text-white dark:bg-white dark:text-charcoal-blue-900"
									>
										gate
									</span>
								)}
								<span className="flex-1 text-sm text-charcoal-blue-900 dark:text-charcoal-blue-50">
									{evalCase.name}
								</span>
								{run && (
									<span className="hidden text-[11px] tabular-nums text-charcoal-blue-400 sm:inline dark:text-charcoal-blue-500">
										{run.tokens} tok · {run.latencyMs} ms
									</span>
								)}
								<Outcome run={run} />
							</button>

							{expanded && (
								<div className="space-y-2 border-t border-charcoal-blue-200 px-3 py-2 text-xs dark:border-white/10">
									<Field label="Input">{evalCase.input}</Field>
									{evalCase.context && (
										<Field label="Context">{evalCase.context}</Field>
									)}
									<Field label="Assertions">{evalCase.assertions}</Field>
									{run?.failureReason && (
										<Field label="Why it failed">{run.failureReason}</Field>
									)}
									{run?.output && <Field label="Output">{run.output}</Field>}
								</div>
							)}
						</li>
					);
				})}
			</ul>
		</section>
	);
}

function Field({ label, children }: { label: string; children: string }) {
	return (
		<div>
			<span className="text-[10px] font-medium uppercase tracking-wide text-charcoal-blue-400 dark:text-charcoal-blue-500">
				{label}
			</span>
			<pre className="mt-0.5 max-h-40 overflow-auto whitespace-pre-wrap font-mono text-[11px] leading-relaxed text-charcoal-blue-700 dark:text-charcoal-blue-300">
				{children}
			</pre>
		</div>
	);
}
