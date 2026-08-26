"use client";

import { useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import { clientApi } from "@/lib/api.client";
import { appToast } from "@/lib/toast";

/**
 * The criteria vocabulary, mirrored from Domain/Achievements/CriteriaTypes.
 *
 * A dropdown rather than a text box on purpose: a typo here produces an
 * achievement that silently never unlocks, and nobody notices for months.
 */
const CRITERIA = [
	{ value: "meals_logged", label: "Meals logged" },
	{ value: "recipes_created", label: "Recipes created" },
	{ value: "workouts_logged", label: "Workouts logged" },
	{ value: "body_measurements_logged", label: "Measurements logged" },
	{ value: "goal_progress_logged", label: "Goal progress logged" },
	{ value: "streak_nutrition", label: "Nutrition streak (days)" },
	{ value: "streak_workout", label: "Workout streak (days)" },
	{ value: "points_total", label: "Total points" },
	{ value: "total_volume_kg", label: "Lifetime volume (kg)" },
	{ value: "template_completed_count", label: "Template workouts" },
	{ value: "followers_count", label: "Followers" },
	{ value: "workouts_shared", label: "Workouts shared" },
	{ value: "reactions_given", label: "Reactions given" },
	{ value: "comments_made", label: "Comments made" },
	{ value: "pr_count", label: "Personal records" },
] as const;

const CATEGORIES = ["nutrition", "training", "body", "social", "milestone"] as const;

export interface AchievementDraft {
	id?: string;
	name: string;
	description: string;
	iconUrl: string;
	points: number;
	category: string;
	criteriaType: string;
	threshold: number;
}

export const EMPTY_DRAFT: AchievementDraft = {
	name: "",
	description: "",
	iconUrl: "",
	points: 25,
	category: "nutrition",
	criteriaType: "meals_logged",
	threshold: 10,
};

export default function AchievementForm({ initial }: { initial: AchievementDraft }) {
	const router = useRouter();
	const [draft, setDraft] = useState(initial);
	const [pending, startTransition] = useTransition();

	const editing = Boolean(initial.id);

	function set<K extends keyof AchievementDraft>(key: K, value: AchievementDraft[K]) {
		setDraft((d) => ({ ...d, [key]: value }));
	}

	function save() {
		if (!draft.name.trim()) {
			appToast.error(new Error("A name is required"), "A name is required");
			return;
		}

		startTransition(async () => {
			try {
				const body = {
					...(editing ? { id: initial.id } : {}),
					name: draft.name.trim(),
					description: draft.description.trim() || null,
					iconUrl: draft.iconUrl.trim() || null,
					points: draft.points,
					category: draft.category,
					criteriaType: draft.criteriaType,
					threshold: draft.threshold,
				};

				if (editing) {
					await clientApi(`/api/Achievements/${initial.id}`, { method: "PUT", body });
				} else {
					await clientApi("/api/Achievements", { method: "POST", body });
				}

				appToast.success(editing ? "Achievement updated" : "Achievement created");
				router.push("/admin/achievements");
				router.refresh();
			} catch (error) {
				appToast.error(error, "Could not save the achievement");
			}
		});
	}

	const criteriaLabel = CRITERIA.find((c) => c.value === draft.criteriaType)?.label ?? "";

	return (
		<div className="space-y-6">
			<section className="card space-y-4 p-6">
				<Field label="Name">
					<input
						value={draft.name}
						onChange={(e) => set("name", e.target.value)}
						maxLength={120}
						className="input w-full"
						placeholder="Consistency"
					/>
				</Field>

				<Field label="Description" helper="Shown under the badge.">
					<textarea
						value={draft.description}
						onChange={(e) => set("description", e.target.value)}
						rows={2}
						maxLength={500}
						className="input w-full"
						placeholder="Log a meal every day for a week."
					/>
				</Field>

				<div className="grid gap-4 sm:grid-cols-2">
					<Field label="Category">
						<select
							value={draft.category}
							onChange={(e) => set("category", e.target.value)}
							className="input w-full"
						>
							{CATEGORIES.map((c) => (
								<option key={c} value={c}>
									{c}
								</option>
							))}
						</select>
					</Field>

					<Field label="Points" helper="Counts toward the user's level.">
						<input
							type="number"
							min={0}
							max={10000}
							value={draft.points}
							onChange={(e) => set("points", Number(e.target.value) || 0)}
							className="input w-full tabular-nums"
						/>
					</Field>
				</div>
			</section>

			<section className="card space-y-4 p-6">
				<div>
					<h2 className="text-base font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
						Unlock condition
					</h2>
					<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
						Evaluated after every relevant log. Streaks use the live count, so a
						lapsed streak cannot unlock one.
					</p>
				</div>

				<div className="grid gap-4 sm:grid-cols-2">
					<Field label="Measure">
						<select
							value={draft.criteriaType}
							onChange={(e) => set("criteriaType", e.target.value)}
							className="input w-full"
						>
							{CRITERIA.map((c) => (
								<option key={c.value} value={c.value}>
									{c.label}
								</option>
							))}
						</select>
					</Field>

					<Field label="Threshold">
						<input
							type="number"
							min={1}
							value={draft.threshold}
							onChange={(e) => set("threshold", Number(e.target.value) || 1)}
							className="input w-full tabular-nums"
						/>
					</Field>
				</div>

				<p className="rounded-2xl bg-charcoal-blue-50 px-3 py-2 text-sm text-charcoal-blue-600 dark:bg-white/[0.03] dark:text-charcoal-blue-300">
					Unlocks at <strong>{criteriaLabel.toLowerCase()} ≥ {draft.threshold}</strong>.
				</p>
			</section>

			<div className="flex gap-2">
				<button
					type="button"
					onClick={() => router.push("/admin/achievements")}
					disabled={pending}
					className="btn-ghost"
				>
					Cancel
				</button>
				<button type="button" onClick={save} disabled={pending} className="btn-primary">
					{pending ? "Saving…" : editing ? "Save changes" : "Create achievement"}
				</button>
			</div>
		</div>
	);
}

function Field({
	label,
	helper,
	children,
}: {
	label: string;
	helper?: string;
	children: React.ReactNode;
}) {
	return (
		<label className="block space-y-1.5">
			<span className="text-sm font-medium text-charcoal-blue-900 dark:text-charcoal-blue-100">
				{label}
			</span>
			{children}
			{helper && (
				<span className="block text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
					{helper}
				</span>
			)}
		</label>
	);
}
