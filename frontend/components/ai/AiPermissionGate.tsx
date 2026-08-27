"use client";

import { useState } from "react";
import { Icon } from "@/components/ui/icon";
import { updateAiConsent, type AiConsent } from "@/lib/api/ai";
import { appToast } from "@/lib/toast";

/**
 * Setup records things on the user's behalf, so it has to ask first.
 *
 * Two grants, not one, and they are genuinely independent: recording a meal
 * someone dictates needs no sight of their history, and answering questions
 * about last week needs no ability to write. The default for both is off, and
 * this is the screen that changes that - see docs/REFOCUS.md §11.
 */
export default function AiPermissionGate({
	consent,
	onGranted,
}: {
	consent: AiConsent;
	onGranted: (next: AiConsent) => void;
}) {
	const [allowWrites, setAllowWrites] = useState(true);
	const [allowReads, setAllowReads] = useState(true);
	const [saving, setSaving] = useState(false);

	async function grant() {
		setSaving(true);
		try {
			const next = await updateAiConsent({
				enabled: allowReads,
				shareNutrition: allowReads,
				shareTraining: allowReads,
				shareBody: allowReads,
				allowWrites,
				writeNutrition: allowWrites,
				writeTraining: allowWrites,
				writeBody: allowWrites,
			});
			onGranted(next);
		} catch (error) {
			appToast.error(error, "Could not save your choice");
		} finally {
			setSaving(false);
		}
	}

	return (
		<div className="glass-panel space-y-5 p-6">
			<div className="space-y-1">
				<h2 className="text-base font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
					Before we start
				</h2>
				<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
					Setup works by recording things as you talk. Decide what it may do. You can
					change both of these later in Settings.
				</p>
			</div>

			<div className="space-y-3">
				<Choice
					checked={allowWrites}
					onChange={setAllowWrites}
					title="Let it record things for me"
					body="Save targets, log meals and workouts, record weigh-ins. It says what it did every time, and nothing it does can delete anything."
				/>
				<Choice
					checked={allowReads}
					onChange={setAllowReads}
					title="Let it read my log"
					body="Your meals, training and measurements, so answers use your actual numbers. Anything you leave off is never sent."
				/>
			</div>

			<div className="flex flex-wrap items-center gap-3">
				<button type="button" onClick={grant} disabled={saving} className="btn-primary !rounded-2xl">
					{saving ? "Saving…" : "Continue"}
					{!saving && <Icon name="arrowRight" size={16} />}
				</button>
				<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
					{!allowWrites && !allowReads
						? "With both off it can only talk - you would do the setup by hand."
						: allowWrites
							? "It will tell you about every change it makes."
							: "It will suggest, and you will do the recording."}
				</p>
			</div>
		</div>
	);
}

function Choice({
	checked,
	onChange,
	title,
	body,
}: {
	checked: boolean;
	onChange: (checked: boolean) => void;
	title: string;
	body: string;
}) {
	return (
		<button
			type="button"
			role="switch"
			aria-checked={checked}
			onClick={() => onChange(!checked)}
			className="flex w-full items-start gap-3 rounded-2xl border border-charcoal-blue-200 p-4 text-left transition-colors hover:border-brand-500/40 dark:border-white/10 dark:hover:border-white/20"
		>
			<span
				className={`mt-0.5 flex h-5 w-5 shrink-0 items-center justify-center rounded-md border transition-colors ${
					checked
						? "border-brand-600 bg-brand-600 text-white"
						: "border-charcoal-blue-300 dark:border-charcoal-blue-600"
				}`}
			>
				{checked && <Icon name="circleCheck" size={12} />}
			</span>
			<span className="min-w-0">
				<span className="block font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">
					{title}
				</span>
				<span className="mt-0.5 block text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
					{body}
				</span>
			</span>
		</button>
	);
}
