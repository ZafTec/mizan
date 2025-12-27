'use client';

import { useEffect, useState } from 'react';
import { apiClient } from '@/lib/auth-client';
import Link from 'next/link';

interface DailyTotals {
	calories: number;
	protein: number;
	carbs: number;
	fat: number;
}

interface Goal {
	targetCalories: number | null;
	targetProteinGrams: number | null;
	targetCarbsGrams: number | null;
	targetFatGrams: number | null;
}

export default function DashboardStats() {
	const [dailyTotals, setDailyTotals] = useState<DailyTotals | null>(null);
	const [goal, setGoal] = useState<Goal | null>(null);
	const [loading, setLoading] = useState(true);
	const [error, setError] = useState<string | null>(null);

	useEffect(() => {
		async function fetchData() {
			try {
				setLoading(true);
				setError(null);

				// Fetch daily meals and goal in parallel (via BFF proxy)
				const today = new Date().toISOString().split('T')[0];
				const [mealsResponse, goalResponse] = await Promise.all([
					apiClient<{ date: string; entries: any[]; totals: DailyTotals }>('/api/bff/Meals?date=' + today)
						.catch((err) => {
							console.error('[Dashboard] Meals API error:', err);
							return { date: today, entries: [], totals: { calories: 0, protein: 0, carbs: 0, fat: 0 } };
						}),
					apiClient<Goal>('/api/bff/Goals')
						.catch((err) => {
							console.error('[Dashboard] Goals API error:', err);
							return null;
						}),
				]);

				// Backend already calculates totals for us
				const totalsResponse = mealsResponse.totals;

				setDailyTotals(totalsResponse);
				setGoal(goalResponse);
			} catch (err) {
				setError('Failed to load dashboard data');
				console.error('Dashboard data error:', err);
			} finally {
				setLoading(false);
			}
		}

		fetchData();
	}, []);

	if (loading) {
		return (
			<div className="grid grid-cols-2 lg:grid-cols-4 gap-4 sm:gap-6">
				{[1, 2, 3, 4].map((i) => (
					<div key={i} className="card p-4 sm:p-6 animate-pulse">
						<div className="h-4 bg-slate-200 rounded w-20 mb-4" />
						<div className="h-8 bg-slate-200 rounded w-16 mb-2" />
						<div className="h-3 bg-slate-200 rounded w-24" />
					</div>
				))}
			</div>
		);
	}

	if (error) {
		return (
			<div className="card p-6 text-center">
				<i className="ri-error-warning-line text-4xl text-red-500 mb-2" />
				<p className="text-slate-600">{error}</p>
				<button
					onClick={() => window.location.reload()}
					className="mt-4 px-4 py-2 text-sm text-brand-600 hover:text-brand-700"
				>
					Try Again
				</button>
			</div>
		);
	}

	const calories = dailyTotals ? Math.round(dailyTotals.calories) : 0;
	const protein = dailyTotals ? Math.round(dailyTotals.protein) : 0;

	return (
		<div className="grid grid-cols-2 lg:grid-cols-4 gap-4 sm:gap-6">
			{/* Calories */}
			<Link href="/meals" className="card p-4 sm:p-6 hover:shadow-lg transition-shadow cursor-pointer group">
				<div className="flex items-center gap-3 mb-3">
					<div className="p-2 bg-brand-50 rounded-lg group-hover:bg-brand-100 transition-colors">
						<i className="ri-fire-line text-xl text-brand-600" />
					</div>
					<span className="text-sm text-slate-500 font-medium">Calories</span>
				</div>
				<div className="flex items-baseline gap-2">
					<span className="text-2xl font-bold text-slate-900">
						{calories.toLocaleString()}
					</span>
					{goal?.targetCalories && (
						<span className="text-sm text-slate-400">/ {goal.targetCalories.toLocaleString()}</span>
					)}
				</div>
				<div className="mt-2 text-xs text-slate-400">
					{goal?.targetCalories ? (
						<>
							{Math.round((calories / goal.targetCalories) * 100)}% of goal
						</>
					) : (
						'No goal set'
					)}
				</div>
			</Link>

			{/* Protein */}
			<Link href="/meals" className="card p-4 sm:p-6 hover:shadow-lg transition-shadow cursor-pointer group">
				<div className="flex items-center gap-3 mb-3">
					<div className="p-2 bg-blue-50 rounded-lg group-hover:bg-blue-100 transition-colors">
						<i className="ri-heart-pulse-line text-xl text-blue-600" />
					</div>
					<span className="text-sm text-slate-500 font-medium">Protein</span>
				</div>
				<div className="flex items-baseline gap-2">
					<span className="text-2xl font-bold text-slate-900">
						{protein}g
					</span>
					{goal?.targetProteinGrams && (
						<span className="text-sm text-slate-400">/ {Math.round(goal.targetProteinGrams)}g</span>
					)}
				</div>
				<div className="mt-2 text-xs text-slate-400">
					{goal?.targetProteinGrams ? (
						<>
							{Math.round((protein / goal.targetProteinGrams) * 100)}% of goal
						</>
					) : (
						'No goal set'
					)}
				</div>
			</Link>

			{/* Water (Placeholder) */}
			<Link href="/profile" className="card p-4 sm:p-6 hover:shadow-lg transition-shadow cursor-pointer group">
				<div className="flex items-center gap-3 mb-3">
					<div className="p-2 bg-cyan-50 rounded-lg group-hover:bg-cyan-100 transition-colors">
						<i className="ri-drop-line text-xl text-cyan-600" />
					</div>
					<span className="text-sm text-slate-500 font-medium">Water</span>
				</div>
				<div className="flex items-baseline gap-2">
					<span className="text-2xl font-bold text-slate-900">0</span>
					<span className="text-sm text-slate-400">/ 8 cups</span>
				</div>
				<div className="mt-2 text-xs text-slate-400">Coming soon</div>
			</Link>

			{/* Streak (Placeholder) */}
			<Link href="/profile" className="card p-4 sm:p-6 hover:shadow-lg transition-shadow cursor-pointer group">
				<div className="flex items-center gap-3 mb-3">
					<div className="p-2 bg-amber-50 rounded-lg group-hover:bg-amber-100 transition-colors">
						<i className="ri-fire-fill text-xl text-amber-600" />
					</div>
					<span className="text-sm text-slate-500 font-medium">Streak</span>
				</div>
				<div className="flex items-baseline gap-2">
					<span className="text-2xl font-bold text-slate-900">0</span>
					<span className="text-sm text-slate-400">days</span>
				</div>
				<div className="mt-2 text-xs text-slate-400">Coming soon</div>
			</Link>
		</div>
	);
}
