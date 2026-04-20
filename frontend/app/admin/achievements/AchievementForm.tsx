"use client";

import { useActionState } from "react";
import { FieldError } from "@/components/FieldError";
import Loading from "@/components/Loading";
import { EMPTY_FORM_STATE, type FormState } from "@/helper/FormErrorHandler";
import type { AdminAchievement } from "@/data/admin/achievement";

const CATEGORIES = ["nutrition", "consistency", "workout", "milestone"] as const;

const CRITERIA_OPTIONS: { value: string; label: string; hint: string }[] = [
    { value: "", label: "(manual only) - no auto-unlock", hint: "Admins grant this achievement manually" },
    { value: "meals_logged", label: "Meals logged", hint: "Total number of diary entries the user has logged" },
    { value: "recipes_created", label: "Recipes created", hint: "Total number of user-owned recipes" },
    { value: "workouts_logged", label: "Workouts logged", hint: "Total workouts recorded" },
    { value: "body_measurements_logged", label: "Body measurements logged", hint: "Total body measurement entries" },
    { value: "goal_progress_logged", label: "Goal progress entries", hint: "Total goal progress records" },
    { value: "streak_nutrition", label: "Nutrition streak (days)", hint: "Current consecutive-day meal logging streak" },
    { value: "streak_workout", label: "Workout streak (days)", hint: "Current consecutive-day workout streak" },
    { value: "points_total", label: "Total points earned", hint: "Sum of points from all earned achievements" },
];

interface Props {
    mode: "create" | "edit";
    initial?: AdminAchievement | null;
    action: (state: FormState, formData: FormData) => Promise<FormState>;
    submitLabel: string;
}

export default function AchievementForm({ mode, initial, action, submitLabel }: Props) {
    const [state, formAction, isPending] = useActionState(action, EMPTY_FORM_STATE);

    return (
        <form action={formAction} className="space-y-6">
            {state.status === "error" && state.message && !state.fieldErrors?.length && (
                <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-800 dark:border-red-900 dark:bg-red-950/40 dark:text-red-200">
                    {state.message}
                </div>
            )}
            {state.status === "success" && state.message && (
                <div className="rounded-xl border border-brand-200 bg-brand-50 p-4 text-sm text-brand-800 dark:border-brand-800 dark:bg-brand-950/40 dark:text-brand-200">
                    {state.message}
                </div>
            )}

            <div className="card p-6 space-y-5">
                <h2 className="font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100">
                    Identity
                </h2>

                <div>
                    <label htmlFor="name" className="label">Name</label>
                    <input
                        id="name"
                        name="name"
                        type="text"
                        className="input"
                        required
                        maxLength={100}
                        defaultValue={initial?.name ?? ""}
                        placeholder="e.g. One-Week Warrior"
                    />
                    <FieldError formState={state} name="name" />
                </div>

                <div>
                    <label htmlFor="description" className="label">Description</label>
                    <textarea
                        id="description"
                        name="description"
                        rows={3}
                        maxLength={500}
                        className="input"
                        defaultValue={initial?.description ?? ""}
                        placeholder="What did the user do to earn this?"
                    />
                    <FieldError formState={state} name="description" />
                </div>

                <div>
                    <label htmlFor="iconUrl" className="label">Icon URL (optional)</label>
                    <input
                        id="iconUrl"
                        name="iconUrl"
                        type="url"
                        className="input"
                        maxLength={500}
                        defaultValue={initial?.iconUrl ?? ""}
                        placeholder="https://..."
                    />
                    <FieldError formState={state} name="iconUrl" />
                </div>
            </div>

            <div className="card p-6 space-y-5">
                <h2 className="font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100">
                    Scoring
                </h2>

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                    <div>
                        <label htmlFor="points" className="label">Points awarded</label>
                        <input
                            id="points"
                            name="points"
                            type="number"
                            min={0}
                            max={10000}
                            className="input"
                            defaultValue={initial?.points ?? 10}
                            required
                        />
                        <FieldError formState={state} name="points" />
                    </div>

                    <div>
                        <label htmlFor="category" className="label">Category</label>
                        <select id="category" name="category" className="input" defaultValue={initial?.category ?? ""}>
                            <option value="">(none)</option>
                            {CATEGORIES.map((c) => (
                                <option key={c} value={c}>
                                    {c}
                                </option>
                            ))}
                        </select>
                        <FieldError formState={state} name="category" />
                    </div>
                </div>
            </div>

            <div className="card p-6 space-y-5">
                <h2 className="font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100">
                    Unlock criteria
                </h2>
                <p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
                    Pick a signal and a threshold. The evaluator will auto-unlock the achievement for any user whose value meets or exceeds the threshold.
                </p>

                <div>
                    <label htmlFor="criteriaType" className="label">Signal</label>
                    <select
                        id="criteriaType"
                        name="criteriaType"
                        className="input"
                        defaultValue={initial?.criteriaType ?? ""}
                    >
                        {CRITERIA_OPTIONS.map((opt) => (
                            <option key={opt.value || "manual"} value={opt.value}>
                                {opt.label}
                            </option>
                        ))}
                    </select>
                    <FieldError formState={state} name="criteriaType" />
                </div>

                <div>
                    <label htmlFor="threshold" className="label">Threshold</label>
                    <input
                        id="threshold"
                        name="threshold"
                        type="number"
                        min={0}
                        className="input"
                        defaultValue={initial?.threshold ?? 1}
                    />
                    <FieldError formState={state} name="threshold" />
                    <p className="mt-1 text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
                        Ignored when the signal is "(manual only)".
                    </p>
                </div>
            </div>

            <div className="flex items-center justify-end gap-3">
                <a href="/admin/achievements" className="btn-ghost">Cancel</a>
                <button type="submit" className="btn-primary" disabled={isPending}>
                    {isPending ? <Loading /> : submitLabel}
                </button>
            </div>
        </form>
    );
}
