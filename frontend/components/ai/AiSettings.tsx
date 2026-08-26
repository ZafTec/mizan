"use client";

import { useCallback, useEffect, useState } from "react";
import { AnimatedIcon } from "@/components/ui/animated-icon";
import Loading from "@/components/Loading";
import { getAiConsent, getMyAiUsage, updateAiConsent, type AiConsent, type MyAiUsage } from "@/lib/api/ai";
import { appToast } from "@/lib/toast";

const AXES = [
	{
		key: "shareNutrition",
		label: "Meals",
		description: "What you ate, and your macro targets.",
	},
	{
		key: "shareTraining",
		label: "Workouts",
		description: "Sessions, exercises and volume.",
	},
	{
		key: "shareBody",
		label: "Body measurements",
		description: "Weight and the rest of your measurements.",
	},
] as const;

const NOTHING: AiConsent = {
	enabled: false,
	shareNutrition: false,
	shareTraining: false,
	shareBody: false,
};

/**
 * What the assistant may see, and what it has cost you. Both defaults matter:
 * every switch starts off, and an axis that is off is never sent - see
 * docs/REFOCUS.md §11.
 */
export function AiSettings() {
	const [consent, setConsent] = useState<AiConsent>(NOTHING);
	const [usage, setUsage] = useState<MyAiUsage | null>(null);
	const [loading, setLoading] = useState(true);
	const [saving, setSaving] = useState(false);

	const load = useCallback(async () => {
		try {
			const [loadedConsent, loadedUsage] = await Promise.all([getAiConsent(), getMyAiUsage()]);
			setConsent(loadedConsent);
			setUsage(loadedUsage);
		} catch (error) {
			appToast.error(error, "Could not load your assistant settings");
		} finally {
			setLoading(false);
		}
	}, []);

	useEffect(() => {
		void load();
	}, [load]);

	async function save(next: AiConsent) {
		const previous = consent;
		setConsent(next);
		setSaving(true);
		try {
			setConsent(await updateAiConsent({
				enabled: next.enabled,
				shareNutrition: next.shareNutrition,
				shareTraining: next.shareTraining,
				shareBody: next.shareBody,
			}));
		} catch (error) {
			setConsent(previous);
			appToast.error(error, "Could not save that change");
		} finally {
			setSaving(false);
		}
	}

	if (loading) {
		return (
			<div className="flex justify-center py-8">
				<Loading />
			</div>
		);
	}

	const sharedCount = AXES.filter((axis) => consent[axis.key]).length;

	return (
		<div className="mt-6 space-y-6">
			<Toggle
				label="Let the assistant use my log"
				description={
					consent.enabled
						? `Sharing ${sharedCount} of ${AXES.length}. Turn this off and it sees nothing, whatever the switches below say.`
						: "Off. The assistant answers from what you type and nothing else."
				}
				checked={consent.enabled}
				disabled={saving}
				onChange={(enabled) => save({ ...consent, enabled })}
			/>

			<div className={consent.enabled ? "space-y-3" : "space-y-3 opacity-50"}>
				{AXES.map((axis) => (
					<Toggle
						key={axis.key}
						label={axis.label}
						description={axis.description}
						checked={consent[axis.key]}
						disabled={saving || !consent.enabled}
						onChange={(checked) => save({ ...consent, [axis.key]: checked })}
					/>
				))}
			</div>

			<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
				An axis you have not shared is never sent to the model - it is left out of
				the request, not included with an instruction to ignore it. Trainers see
				what you granted them separately, and only where both allow it.
			</p>

			{usage && <UsagePanel usage={usage} />}
		</div>
	);
}

function UsagePanel({ usage }: { usage: MyAiUsage }) {
	const { today } = usage;
	const resets = new Date(today.resetsAt);

	return (
		<div className="rounded-3xl border border-charcoal-blue-200 bg-white/70 p-4 dark:border-white/10 dark:bg-charcoal-blue-950/60">
			<div className="flex items-center justify-between">
				<p className="text-sm font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
					Today&apos;s usage
				</p>
				<span className="rounded-full border border-charcoal-blue-200 px-2 py-0.5 text-[10px] uppercase tracking-[0.14em] text-charcoal-blue-500 dark:border-white/10 dark:text-charcoal-blue-400">
					{today.plan}
				</span>
			</div>

			<div className="mt-3 grid gap-3 sm:grid-cols-2">
				<Meter label="Requests" used={today.requestsUsed} limit={today.requestLimit} />
				<Meter label="Tokens" used={today.tokensUsed} limit={today.tokenLimit} />
			</div>

			<p className="mt-3 text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
				Resets at {resets.toLocaleTimeString([], { hour: "numeric", minute: "2-digit" })}.
			</p>

			{usage.byFeature.length > 0 && (
				<ul className="mt-3 space-y-1 border-t border-charcoal-blue-200/70 pt-3 dark:border-white/10">
					{usage.byFeature.map((feature) => (
						<li
							key={feature.feature}
							className="flex items-center justify-between text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400"
						>
							<span>{feature.feature}</span>
							<span>
								{feature.requests} request{feature.requests === 1 ? "" : "s"} ·{" "}
								{feature.tokens.toLocaleString()} tokens
							</span>
						</li>
					))}
				</ul>
			)}
		</div>
	);
}

function Meter({ label, used, limit }: { label: string; used: number; limit: number }) {
	const percent = limit > 0 ? Math.min(100, Math.round((used / limit) * 100)) : 0;

	return (
		<div>
			<div className="flex items-baseline justify-between text-xs">
				<span className="text-charcoal-blue-500 dark:text-charcoal-blue-400">{label}</span>
				<span className="font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">
					{used.toLocaleString()} / {limit.toLocaleString()}
				</span>
			</div>
			<div className="mt-1.5 h-1.5 overflow-hidden rounded-full bg-charcoal-blue-200 dark:bg-charcoal-blue-800">
				<div
					className={percent >= 90 ? "h-full bg-red-500" : "h-full bg-brand-600 dark:bg-brand-500"}
					style={{ width: `${percent}%` }}
				/>
			</div>
		</div>
	);
}

function Toggle({
	label,
	description,
	checked,
	disabled,
	onChange,
}: {
	label: string;
	description: string;
	checked: boolean;
	disabled?: boolean;
	onChange: (checked: boolean) => void;
}) {
	return (
		<button
			type="button"
			role="switch"
			aria-checked={checked}
			disabled={disabled}
			onClick={() => onChange(!checked)}
			className="flex w-full items-center justify-between gap-4 rounded-3xl border border-charcoal-blue-200 bg-white p-4 text-left transition-colors hover:border-charcoal-blue-300 disabled:cursor-not-allowed dark:border-white/10 dark:bg-charcoal-blue-950 dark:hover:border-white/20"
		>
			<div className="min-w-0">
				<p className="font-medium text-charcoal-blue-900 dark:text-charcoal-blue-100">{label}</p>
				<p className="mt-1 text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">{description}</p>
			</div>
			<span
				className={`relative h-6 w-11 shrink-0 rounded-full transition-colors ${
					checked ? "bg-brand-600" : "bg-charcoal-blue-300 dark:bg-charcoal-blue-700"
				}`}
			>
				<span
					className={`absolute top-0.5 h-5 w-5 rounded-full bg-white transition-transform ${
						checked ? "translate-x-5" : "translate-x-0.5"
					}`}
				/>
			</span>
		</button>
	);
}

export function AiSettingsIcon() {
	return <AnimatedIcon name="brain" size={18} aria-hidden="true" />;
}
