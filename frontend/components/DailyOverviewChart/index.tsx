'use client';

import { useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { clientApi } from '@/lib/api.client';
import { AnimatedIcon } from '@/components/ui/animated-icon';

interface Goal {
	targetCalories: number | null;
	targetProteinGrams: number | null;
	targetCarbsGrams: number | null;
	targetFatGrams: number | null;
}

interface Macros {
	calories: number;
	protein: number;
	carbs: number;
	fat: number;
	fiber: number;
}

type MacroItem = {
	key: keyof Macros;
	label: string;
	unit: string;
	value: number;
	dotClassName: string;
	panelClassName: string;
};

const EMPTY_MACROS: Macros = {
	calories: 0,
	protein: 0,
	carbs: 0,
	fat: 0,
	fiber: 0,
};

function roundValue(value: number): number {
	return Math.round(value || 0);
}

function getProgress(value: number, target: number | null): number | null {
	if (!target || target <= 0) return null;
	return Math.min(Math.round((value / target) * 100), 100);
}

function getDeltaCopy(value: number, target: number | null, unit: string): string {
	if (!target || target <= 0) return 'No target set';

	const difference = roundValue(target - value);
	if (difference > 0) return `${difference}${unit} remaining`;
	if (difference < 0) return `${Math.abs(difference)}${unit} over`;
	return 'Target reached';
}

export default function DailyOverviewChart() {
	const [goal, setGoal] = useState<Goal | null>(null);
	const [macros, setMacros] = useState<Macros>(EMPTY_MACROS);
	const [loading, setLoading] = useState(true);

	useEffect(() => {
		async function fetchData() {
			try {
				const today = new Date().toISOString().split('T')[0];

				const [goalResponse, mealsResponse] = await Promise.all([
					clientApi<Goal>('/api/Goals').catch(() => null),
					clientApi<{ totals: Macros }>(`/api/Meals?date=${today}`).catch(() => ({ totals: EMPTY_MACROS })),
				]);

				setGoal(goalResponse);
				setMacros(mealsResponse.totals ?? EMPTY_MACROS);
			} catch (error) {
				console.error('Failed to fetch chart data:', error);
			} finally {
				setLoading(false);
			}
		}

		fetchData();
	}, []);

	const macroItems = useMemo<MacroItem[]>(
		() => [
			{
				key: 'calories',
				label: 'Calories',
				unit: ' kcal',
				value: macros.calories,
				dotClassName: 'bg-brand-600',
				panelClassName: 'border-brand-100 bg-brand-50/80 text-brand-800 dark:border-brand-500/20 dark:bg-brand-500/10 dark:text-brand-300',
			},
			{
				key: 'protein',
				label: 'Protein',
				unit: 'g',
				value: macros.protein,
				dotClassName: 'bg-sky-500',
				panelClassName: 'border-sky-100 bg-sky-50/80 text-sky-800 dark:border-sky-500/20 dark:bg-sky-500/10 dark:text-sky-300',
			},
			{
				key: 'carbs',
				label: 'Carbs',
				unit: 'g',
				value: macros.carbs,
				dotClassName: 'bg-amber-500',
				panelClassName: 'border-amber-100 bg-amber-50/80 text-amber-800 dark:border-amber-500/20 dark:bg-amber-500/10 dark:text-amber-300',
			},
			{
				key: 'fat',
				label: 'Fat',
				unit: 'g',
				value: macros.fat,
				dotClassName: 'bg-rose-500',
				panelClassName: 'border-rose-100 bg-rose-50/80 text-rose-800 dark:border-rose-500/20 dark:bg-rose-500/10 dark:text-rose-300',
			},
		],
		[macros]
	);

	const macroDistribution = useMemo(() => {
		const total = macros.protein + macros.carbs + macros.fat;

		return macroItems
			.filter((item) => item.key !== 'calories')
			.map((item) => ({
				...item,
				share: total > 0 ? Math.round((item.value / total) * 100) : 0,
			}));
	}, [macroItems, macros.carbs, macros.fat, macros.protein]);

	const calorieProgress = getProgress(macros.calories, goal?.targetCalories ?? null);
	const hasGoals = Boolean(
		goal?.targetCalories ||
		goal?.targetProteinGrams ||
		goal?.targetCarbsGrams ||
		goal?.targetFatGrams
	);
	const trackedMetrics = macroItems.filter((item) => item.value > 0).length;

	if (loading) {
		return (
			<div className="grid gap-4 lg:grid-cols-[1.2fr_0.8fr]">
				<div className="card p-6">
					<div className="h-6 w-36 animate-pulse rounded-full bg-slate-200 dark:bg-slate-800" />
					<div className="mt-6 grid gap-3 sm:grid-cols-3">
						{Array.from({ length: 3 }).map((_, index) => (
							<div key={index} className="h-24 animate-pulse rounded-3xl bg-slate-100 dark:bg-slate-900" />
						))}
					</div>
					<div className="mt-6 space-y-4">
						{Array.from({ length: 4 }).map((_, index) => (
							<div key={index} className="space-y-2">
								<div className="h-4 w-24 animate-pulse rounded-full bg-slate-200 dark:bg-slate-800" />
								<div className="h-2.5 animate-pulse rounded-full bg-slate-100 dark:bg-slate-900" />
							</div>
						))}
					</div>
				</div>
				<div className="card h-full p-6">
					<div className="h-full animate-pulse rounded-3xl bg-slate-100 dark:bg-slate-900" />
				</div>
			</div>
		);
	}

	return (
		<div className="grid gap-4 lg:grid-cols-[1.2fr_0.8fr]">
			<div className="card p-6 sm:p-7">
				<div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
					<div>
						<p className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500 dark:text-slate-400">Today&apos;s intake</p>
						<h3 className="mt-2 text-2xl font-semibold text-slate-950 dark:text-slate-50">Nutrition pace</h3>
						<p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
							{trackedMetrics > 0
								? `You have tracked ${trackedMetrics} core nutrition metric${trackedMetrics === 1 ? '' : 's'} today.`
								: 'No meals logged yet. Start with a quick entry and the dashboard will fill in.'}
						</p>
					</div>
					<Link href="/meals" className="btn-secondary w-full justify-center sm:w-auto">
						Log meal
						<AnimatedIcon name="arrowRight" size={16} aria-hidden="true" />
					</Link>
				</div>

				<div className="mt-6 grid gap-3 sm:grid-cols-3">
					<SummaryPanel
						label="Calories logged"
						value={`${roundValue(macros.calories)}`}
						suffix="kcal"
						helper={goal?.targetCalories ? `${roundValue(goal.targetCalories)} kcal target` : 'Goal not set'}
						className="border-slate-200 bg-slate-50/90 text-slate-900 dark:border-white/10 dark:bg-slate-900/70 dark:text-slate-100"
					/>
					<SummaryPanel
						label="Goal progress"
						value={calorieProgress !== null ? `${calorieProgress}%` : '--'}
						helper={getDeltaCopy(macros.calories, goal?.targetCalories ?? null, ' kcal')}
						className="border-brand-100 bg-brand-50/85 text-brand-900 dark:border-brand-500/20 dark:bg-brand-500/10 dark:text-brand-200"
					/>
					<SummaryPanel
						label="Fiber"
						value={`${roundValue(macros.fiber)}`}
						suffix="g"
						helper={macros.fiber > 0 ? 'Tracked from meals today' : 'No fiber recorded yet'}
						className="border-amber-100 bg-amber-50/85 text-amber-900 dark:border-amber-500/20 dark:bg-amber-500/10 dark:text-amber-200"
					/>
				</div>

				<div className="mt-6 rounded-3xl border border-slate-200 bg-slate-50/90 p-4 dark:border-white/10 dark:bg-slate-900/70">
					<div className="flex items-center justify-between gap-3">
						<div>
							<p className="text-sm font-semibold text-slate-900 dark:text-slate-100">Daily read</p>
							<p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
								{hasGoals
									? getDeltaCopy(macros.calories, goal?.targetCalories ?? null, ' kcal')
									: 'Set calorie and macro goals to turn this into a real pacing view.'}
							</p>
						</div>
						<span className="icon-chip h-11 w-11 text-brand-600 dark:text-brand-300">
							<AnimatedIcon name="flame" size={18} aria-hidden="true" />
						</span>
					</div>
					<div className="mt-4 flex flex-wrap gap-2">
						{macroDistribution.map((item) => (
							<div key={item.key} className="inline-flex items-center gap-2 rounded-full border border-slate-200 bg-white px-3 py-2 text-sm text-slate-600 dark:border-white/10 dark:bg-slate-950 dark:text-slate-300">
								<span className={`h-2 w-2 rounded-sm ${item.dotClassName}`} />
								<span>{item.label}</span>
								<span className="font-medium text-slate-900 dark:text-slate-100">{item.share}%</span>
							</div>
						))}
					</div>
				</div>
			</div>

			<div className="card p-6 sm:p-7">
				<div className="flex items-center gap-3">
					<span className="icon-chip h-10 w-10 text-brand-600 dark:text-brand-300">
						<AnimatedIcon name="chartLine" size={18} aria-hidden="true" />
					</span>
					<div>
						<p className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500 dark:text-slate-400">Breakdown</p>
						<h3 className="text-lg font-semibold text-slate-950 dark:text-slate-50">Macro balance</h3>
					</div>
				</div>

				<div className="mt-6 space-y-3">
					{macroDistribution.map((item) => (
						<div key={item.key} className={`rounded-3xl border p-4 ${item.panelClassName}`}>
							<div className="flex items-center justify-between gap-3 mb-2.5">
								<div className="flex items-center gap-3">
									<span className={`h-2.5 w-2.5 rounded-xl ${item.dotClassName}`} />
									<div>
										<p className="text-sm font-semibold">{item.label}</p>
										<p className="text-xs opacity-75">{roundValue(item.value)}{item.unit}</p>
									</div>
								</div>
								<p className="text-sm font-semibold">{item.share}%</p>
							</div>
							<div className="h-1.5 w-full rounded-xl bg-slate-900/10 dark:bg-white/10 overflow-hidden">
								<div className={`h-full rounded-xl ${item.dotClassName} transition-all duration-500`} style={{ width: `${item.share}%` }} />
							</div>
						</div>
					))}
				</div>

				<div className="mt-6 rounded-3xl border border-slate-200 bg-slate-50/90 p-4 dark:border-white/10 dark:bg-slate-900/70">
					<p className="text-sm font-semibold text-slate-900 dark:text-slate-100">Next step</p>
					<p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
						{hasGoals
							? 'Use the goal dashboard to compare today against your trend and tighten any weak macro.'
							: 'Set daily targets to unlock progress tracking and make this section actually useful.'}
					</p>
					<Link href="/goal" className="btn-primary mt-4 w-full justify-center">
						{hasGoals ? 'Review goals' : 'Set goals'}
						<AnimatedIcon name="arrowRight" size={16} aria-hidden="true" />
					</Link>
				</div>
			</div>
		</div>
	);
}

function SummaryPanel({
	label,
	value,
	suffix,
	helper,
	className,
}: {
	label: string;
	value: string;
	suffix?: string;
	helper: string;
	className: string;
}) {
	return (
		<div className={`rounded-3xl border p-4 ${className}`}>
			<p className="text-xs font-semibold uppercase tracking-[0.14em] opacity-70">{label}</p>
			<div className="mt-3 flex items-end gap-1">
				<p className="text-2xl font-semibold">{value}</p>
				{suffix ? <span className="pb-1 text-sm opacity-75">{suffix}</span> : null}
			</div>
			<p className="mt-2 text-xs opacity-75">{helper}</p>
		</div>
	);
}
