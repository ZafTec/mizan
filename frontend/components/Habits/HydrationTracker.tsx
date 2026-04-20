"use client";

import { useEffect, useState } from "react";
import { AnimatedIcon } from "@/components/ui/animated-icon";

const TARGET_GLASSES = 8;
const GLASS_ML = 250;
const STORAGE_KEY_PREFIX = "mizan-hydration-";

function todayKey() {
	return `${STORAGE_KEY_PREFIX}${new Date().toISOString().split("T")[0]}`;
}

export default function HydrationTracker() {
	const [glasses, setGlasses] = useState(0);
	const [mounted, setMounted] = useState(false);

	useEffect(() => {
		let initial = 0;
		try {
			const raw = window.localStorage.getItem(todayKey());
			if (raw) initial = Math.max(0, parseInt(raw, 10) || 0);
		} catch {
			// ignore persistence errors
		}
		// eslint-disable-next-line react-hooks/set-state-in-effect -- one-time hydration from localStorage at mount
		setGlasses(initial);
		setMounted(true);
	}, []);

	function persist(next: number) {
		setGlasses(next);
		try {
			window.localStorage.setItem(todayKey(), String(next));
		} catch {
			// ignore
		}
	}

	const percent = Math.min(glasses / TARGET_GLASSES, 1);
	const ml = glasses * GLASS_ML;

	return (
		<section className="glass-panel flex flex-col gap-4 p-6 sm:p-8">
			<header>
				<h2 className="text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
					Hydration
				</h2>
				<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
					{mounted ? `${ml} ml of ${TARGET_GLASSES * GLASS_ML} ml` : "Loading…"}
				</p>
			</header>

			<div className="relative mx-auto h-48 w-32 overflow-hidden rounded-b-[48px] rounded-t-3xl border border-charcoal-blue-200 bg-white/60 dark:border-white/10 dark:bg-charcoal-blue-950/60">
				<div
					className="absolute inset-x-0 bottom-0 transition-all duration-700"
					style={{
						height: `${percent * 100}%`,
						background:
							"linear-gradient(180deg, color-mix(in oklab, var(--color-verdigris-400) 50%, transparent), var(--color-verdigris-500))",
					}}
					aria-hidden="true"
				/>
				<div className="relative flex h-full flex-col items-center justify-center gap-1">
					<p className="text-3xl font-bold text-charcoal-blue-900 drop-shadow-sm dark:text-charcoal-blue-50">
						{mounted ? glasses : "–"}
					</p>
					<p className="text-[10px] font-semibold uppercase tracking-[0.2em] text-charcoal-blue-700 dark:text-charcoal-blue-100">
						of {TARGET_GLASSES}
					</p>
				</div>
			</div>

			<div className="flex items-center justify-center gap-3">
				<button
					type="button"
					onClick={() => persist(Math.max(0, glasses - 1))}
					disabled={!mounted || glasses === 0}
					className="btn-ghost rounded-2xl! py-2! text-sm"
					aria-label="Remove a glass"
				>
					-
				</button>
				<button
					type="button"
					onClick={() => persist(glasses + 1)}
					disabled={!mounted}
					className="btn-primary rounded-2xl! py-2! text-sm"
				>
					<AnimatedIcon name="sparkles" size={14} />
					+ Glass
				</button>
				<button
					type="button"
					onClick={() => persist(0)}
					disabled={!mounted || glasses === 0}
					className="btn-ghost rounded-2xl! py-2! text-sm"
					aria-label="Reset hydration"
				>
					Reset
				</button>
			</div>
			<p className="text-center text-[11px] text-charcoal-blue-500 dark:text-charcoal-blue-400">
				Stored locally. A server-side hydration log is planned.
			</p>
		</section>
	);
}
