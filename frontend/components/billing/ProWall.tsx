"use client";

import { useCallback, useState, type ReactNode } from "react";
import Link from "next/link";
import { ModalShell } from "@/components/ModalShell";
import { Icon } from "@/components/ui/icon";
import { useSubscription } from "@/lib/hooks/useSubscription";

export type CheckoutPlan = "pro" | "pro-yearly" | "lifetime";

export interface ProWallConfig {
	/** What the user just tried to do, as a heading: "Unlimited meal plans". */
	title: string;
	/** One sentence on what Pro changes about it. */
	message: string;
	plan?: CheckoutPlan;
}

/**
 * The in-context Pro wall - docs/REFOCUS.md §3 tier 2 and §5.
 *
 * Gating happens at the moment of the attempt, never as a standing banner. Wrap
 * the handler, render the dialog:
 *
 * const { guard, wall } = useProWall({ title: "...", message: "..." });
 * <button onClick={guard(createPlan)}>New plan</button>
 * {wall}
 *
 * While the subscription is still loading the action runs. The server is the
 * authority on entitlement; a wall that guesses "not Pro" would block paying
 * users for the first few hundred milliseconds of every page.
 */
export function useProWall(config: ProWallConfig) {
	const { isPro, loading } = useSubscription();
	const [open, setOpen] = useState(false);

	const openWall = useCallback(() => setOpen(true), []);
	const closeWall = useCallback(() => setOpen(false), []);

	const guard = useCallback(
		<A extends unknown[]>(action: (...args: A) => void) =>
			(...args: A) => {
				if (isPro || loading) {
					action(...args);
					return;
				}
				setOpen(true);
			},
		[isPro, loading],
	);

	const wall = <ProWallDialog open={open} onClose={closeWall} {...config} />;

	return { isPro, loading, guard, openWall, wall };
}

export function ProWallDialog({
	open,
	onClose,
	title,
	message,
	plan = "pro",
}: ProWallConfig & { open: boolean; onClose: () => void }) {
	return (
		<ModalShell open={open} onClose={onClose}>
			<div className="rounded-2xl border border-charcoal-blue-200 bg-white p-6 dark:border-white/10 dark:bg-charcoal-blue-950 sm:p-8">
				<span className="icon-chip h-12 w-12 text-brand-700 dark:text-brand-300">
					<Icon name="lock" size={20} />
				</span>
				<h2 className="mt-4 text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
					{title}
				</h2>
				<p className="mt-1.5 text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
					{message}
				</p>
				<div className="mt-6 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
					<button type="button" onClick={onClose} className="btn-ghost !rounded-2xl">
						Not now
					</button>
					<Link href={`/billing?checkout=${plan}`} className="btn-primary !rounded-2xl">
						<Icon name="sparkles" size={16} />
						See Pro
					</Link>
				</div>
			</div>
		</ModalShell>
	);
}

/**
 * Declarative form, for gates whose trigger is a link or a block rather than a
 * handler: Pro users get the real thing, everyone else gets a button that
 * explains the gate.
 */
export function ProWall({
	children,
	lockedLabel,
	...config
}: ProWallConfig & { children: ReactNode; lockedLabel: string }) {
	const { isPro, loading, openWall, wall } = useProWall(config);

	if (isPro || loading) return <>{children}</>;

	return (
		<>
			<button type="button" onClick={openWall} className="btn-secondary !rounded-2xl text-sm">
				<Icon name="lock" size={14} />
				{lockedLabel}
			</button>
			{wall}
		</>
	);
}
