"use client";

import { useCallback, useEffect, useMemo, useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import { appToast } from "@/lib/toast";
import { cn } from "@/lib/utils";
import type {
	AiEvalMatrix,
	AiPromptDetail,
	AiPromptVersion,
} from "@/data/admin/ai";
import {
	createDraft,
	getEvalMatrix,
	publishVersion,
	runEvals,
	updateDraft,
} from "@/lib/api/admin-ai";
import HardConstraints from "./HardConstraints";
import EvalMatrix from "./EvalMatrix";
import BodyDiff from "./BodyDiff";

const DRAFT = 0;
const PUBLISHED = 1;

function statusLabel(status: number) {
	if (status === PUBLISHED) return "live";
	return status === DRAFT ? "draft" : "archived";
}

/** Pending and Running are the two states worth polling through. */
function isRunning(runStatus: string | null | undefined) {
	return runStatus === "Pending" || runStatus === "Running";
}

interface Props {
	prompt: AiPromptDetail;
	initialMatrix: AiEvalMatrix | null;
}

export default function PromptConsole({ prompt, initialMatrix }: Props) {
	const router = useRouter();
	const [pending, startTransition] = useTransition();
	const [selectedId, setSelectedId] = useState(prompt.versions[0]?.id ?? null);
	const [matrix, setMatrix] = useState(initialMatrix);

	// The suite runs on the queue now, so the console watches instead of
	// waiting. Polling stops the moment the job leaves a running state - a
	// timer that outlives the page is how you get a request every two seconds
	// on an idle admin tab.
	const [running, setRunning] = useState(
		initialMatrix?.runStatus === "Pending" || initialMatrix?.runStatus === "Running",
	);
	const [draft, setDraft] = useState<{ body: string; softPolicy: string } | null>(
		null,
	);

	const selected = useMemo(
		() => prompt.versions.find((v) => v.id === selectedId) ?? null,
		[prompt.versions, selectedId],
	);
	const live = useMemo(
		() => prompt.versions.find((v) => v.status === PUBLISHED) ?? null,
		[prompt.versions],
	);

	const editing = selected?.status === DRAFT;
	const body = draft?.body ?? selected?.body ?? prompt.defaultBody;
	const softPolicy = draft?.softPolicy ?? selected?.softPolicy ?? "{}";
	const dirty = draft !== null;

	function select(version: AiPromptVersion) {
		setSelectedId(version.id);
		setDraft(null);
		setMatrix(null);
		setRunning(false);
		startTransition(async () => {
			try {
				const next = await getEvalMatrix(version.id);
				setMatrix(next);
				setRunning(isRunning(next.runStatus));
			} catch (error) {
				appToast.error(error, "Could not load evals");
			}
		});
	}

	function run<T>(action: () => Promise<T>, onDone: (result: T) => void, failure: string) {
		startTransition(async () => {
			try {
				onDone(await action());
			} catch (error) {
				appToast.error(error, failure);
			}
		});
	}

	const announce = useCallback((next: AiEvalMatrix) => {
		if (next.runStatus === "DeadLettered" || next.runStatus === "Failed") {
			appToast.error(next.runError ?? "The suite did not finish.");
			return;
		}
		appToast.success(
			next.publishable
				? "Suite passed. Ready to publish."
				: (next.blockedReason ?? "Suite finished with failures."),
		);
	}, []);

	useEffect(() => {
		if (!running || !selectedId) return;

		let cancelled = false;
		const timer = setInterval(async () => {
			try {
				const next = await getEvalMatrix(selectedId);
				if (cancelled) return;

				setMatrix(next);
				if (isRunning(next.runStatus)) return;

				setRunning(false);
				announce(next);
			} catch {
				// A single failed poll is not worth a toast; the next one either
				// works or the interval is torn down with the page.
			}
		}, 3000);

		return () => {
			cancelled = true;
			clearInterval(timer);
		};
	}, [running, selectedId, announce]);

	function onBranch() {
		run(
			() => createDraft(prompt.key, { body, softPolicy }),
			(version) => {
				setSelectedId(version.id);
				setDraft(null);
				setMatrix(null);
				appToast.success(`Draft v${version.version} created`);
				router.refresh();
			},
			"Could not create the draft",
		);
	}

	function onSave() {
		if (!selected) return;
		run(
			() => updateDraft(selected.id, { body, softPolicy }),
			() => {
				setDraft(null);
				// Saving discards what the old text proved, so the matrix goes with it.
				setMatrix(null);
				setRunning(false);
				appToast.success("Draft saved. Evals cleared.");
				router.refresh();
			},
			"Could not save the draft",
		);
	}

	function onRunEvals() {
		if (!selected) return;
		run(
			async () => {
				await runEvals(selected.id);
				return getEvalMatrix(selected.id);
			},
			(next) => {
				setMatrix(next);
				setRunning(true);
				appToast.success("Suite queued. Results land here as it runs.");
			},
			"Could not queue the suite",
		);
	}

	function onPublish() {
		if (!selected) return;
		run(
			() => publishVersion(selected.id),
			() => {
				appToast.success(
					selected.status === DRAFT
						? `v${selected.version} published`
						: `Rolled back to v${selected.version}`,
				);
				router.refresh();
			},
			"Could not publish",
		);
	}

	const canPublish =
		selected !== null &&
		selected.status !== PUBLISHED &&
		!dirty &&
		!running &&
		(selected.status !== DRAFT || matrix?.publishable === true);

	return (
		<div className="grid gap-6 lg:grid-cols-[240px_minmax(0,1fr)]">
			<aside className="space-y-3">
				<div className="flex items-center justify-between">
					<h2 className="text-sm font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
						Versions
					</h2>
					<button
						type="button"
						onClick={onBranch}
						disabled={pending}
						className="btn-ghost btn-sm"
					>
						New draft
					</button>
				</div>

				{prompt.versions.length === 0 ? (
					<p className="rounded-2xl border border-dashed border-charcoal-blue-200 p-4 text-xs text-charcoal-blue-500 dark:border-white/10 dark:text-charcoal-blue-400">
						Nothing published. The built-in default is answering, and a new
						draft starts from it.
					</p>
				) : (
					<ul className="space-y-1.5">
						{prompt.versions.map((version) => (
							<li key={version.id}>
								<button
									type="button"
									onClick={() => select(version)}
									className={cn(
										"w-full rounded-xl border px-3 py-2 text-left text-sm transition-colors",
										version.id === selectedId
											? "border-verdigris-400 bg-verdigris-50 dark:border-verdigris-500/40 dark:bg-verdigris-500/10"
											: "border-charcoal-blue-200 hover:border-verdigris-300 dark:border-white/10",
									)}
								>
									<span className="flex items-center justify-between gap-2">
										<span className="font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">
											v{version.version}
										</span>
										<span
											className={cn(
												"rounded-full px-2 py-0.5 text-[11px]",
												version.status === PUBLISHED
													? "bg-verdigris-100 text-verdigris-800 dark:bg-verdigris-500/15 dark:text-verdigris-300"
													: version.status === DRAFT
														? "bg-amber-100 text-amber-800 dark:bg-amber-500/15 dark:text-amber-300"
														: "bg-charcoal-blue-100 text-charcoal-blue-600 dark:bg-white/10 dark:text-charcoal-blue-300",
											)}
										>
											{statusLabel(version.status)}
										</span>
									</span>
									{version.authorName && (
										<span className="mt-0.5 block text-[11px] text-charcoal-blue-400 dark:text-charcoal-blue-500">
											{version.authorName}
										</span>
									)}
								</button>
							</li>
						))}
					</ul>
				)}
			</aside>

			<div className="space-y-6">
				<HardConstraints
					preamble={prompt.preamble}
					constraints={prompt.hardConstraints}
				/>

				<section className="glass-panel space-y-4 p-5">
					<div className="flex flex-wrap items-center justify-between gap-3">
						<div>
							<h2 className="text-base font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
								{selected ? `Version ${selected.version}` : "Built-in default"}
							</h2>
							<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
								{editing
									? "Editable. Saving clears whatever the old text proved."
									: "Read-only. Branch a new draft to change it."}
							</p>
						</div>

						<div className="flex gap-2">
							{editing && (
								<>
									<button
										type="button"
										onClick={onSave}
										disabled={pending || !dirty}
										className="btn-secondary btn-sm"
									>
										Save
									</button>
									<button
										type="button"
										onClick={onRunEvals}
										disabled={pending || dirty || running}
										className="btn-secondary btn-sm"
									>
										{running ? "Running…" : "Run evals"}
									</button>
								</>
							)}
							{selected && selected.status !== PUBLISHED && (
								<button
									type="button"
									onClick={onPublish}
									disabled={pending || !canPublish}
									title={
										canPublish
											? undefined
											: (matrix?.blockedReason ??
												"Save and run the suite first.")
									}
									className="btn-primary btn-sm"
								>
									{selected.status === DRAFT ? "Publish" : "Roll back to this"}
								</button>
							)}
						</div>
					</div>

					<label className="block space-y-1.5">
						<span className="text-xs font-medium uppercase tracking-wide text-charcoal-blue-500 dark:text-charcoal-blue-400">
							Body
						</span>
						<textarea
							value={body}
							readOnly={!editing}
							onChange={(e) => setDraft({ body: e.target.value, softPolicy })}
							rows={14}
							className="input w-full font-mono text-xs leading-relaxed"
						/>
					</label>

					<label className="block space-y-1.5">
						<span className="text-xs font-medium uppercase tracking-wide text-charcoal-blue-500 dark:text-charcoal-blue-400">
							Soft policy
						</span>
						<textarea
							value={softPolicy}
							readOnly={!editing}
							onChange={(e) => setDraft({ body, softPolicy: e.target.value })}
							rows={5}
							className="input w-full font-mono text-xs leading-relaxed"
							placeholder='{"tone":"dry","verbosity":"short","refusalTopics":["supplement dosing"]}'
						/>
					</label>
				</section>

				{live && selected && live.id !== selected.id && (
					<BodyDiff
						liveLabel={`v${live.version} (live)`}
						draftLabel={`v${selected.version}`}
						live={live.body}
						draft={body}
					/>
				)}

				{running && (
					<div
						role="status"
						className="flex items-center gap-3 rounded-2xl border border-amber-300 bg-amber-50 px-4 py-3 text-sm text-amber-900 dark:border-amber-500/30 dark:bg-amber-500/10 dark:text-amber-200"
					>
						<span className="h-2 w-2 shrink-0 animate-pulse rounded-full bg-amber-500" />
						<span>
							The suite is running on the queue. Anything below is from the
							previous run until it finishes; publishing is blocked meanwhile.
						</span>
					</div>
				)}

				{!running && matrix?.runError && (
					<div className="rounded-2xl border border-red-300 bg-red-50 px-4 py-3 text-sm text-red-800 dark:border-red-500/30 dark:bg-red-500/10 dark:text-red-300">
						The last run did not finish: {matrix.runError}
					</div>
				)}

				{selected && (
					<EvalMatrix
						matrix={matrix}
						loading={pending && matrix === null}
						editable={editing}
					/>
				)}
			</div>
		</div>
	);
}
