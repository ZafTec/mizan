import Link from "next/link";
import { AnimatedIcon } from "@/components/ui/animated-icon";
import { getUserServer } from "@/helper/session";
import { getStreak } from "@/data/achievement";
import { getDailyTotals, getTodayMeal } from "@/data/meal";
import { getCurrentGoal } from "@/data/goal";
import HydrationTracker from "@/components/Habits/HydrationTracker";

export const dynamic = "force-dynamic";

interface HabitRow {
	id: string;
	label: string;
	description: string;
	done: boolean;
	icon: Parameters<typeof AnimatedIcon>[0]["name"];
	href?: string;
}

export default async function HabitsPage() {
	await getUserServer();
	const [goal, meals, totals, streak] = await Promise.all([
		getCurrentGoal(),
		getTodayMeal(),
		getDailyTotals(),
		getStreak(),
	]);

	const mealsByType = new Set(meals.map((m) => m.mealType?.toLowerCase()).filter(Boolean));
	const hitCalories =
		goal?.targetCalories && totals?.calories
			? totals.calories >= goal.targetCalories * 0.8
			: false;
	const hitProtein =
		goal?.targetProteinGrams && totals?.protein
			? totals.protein >= goal.targetProteinGrams * 0.8
			: false;

	const habits: HabitRow[] = [
		{
			id: "breakfast",
			label: "Log breakfast",
			description: "Start the day within 90 min of waking.",
			done: mealsByType.has("breakfast"),
			icon: "flame",
			href: "/meals/add",
		},
		{
			id: "lunch",
			label: "Log lunch",
			description: "Mid-day refuel. Aim for protein + fibre.",
			done: mealsByType.has("lunch"),
			icon: "cookingPot",
			href: "/meals/add",
		},
		{
			id: "dinner",
			label: "Log dinner",
			description: "Close the calorie ring comfortably.",
			done: mealsByType.has("dinner"),
			icon: "flame",
			href: "/meals/add",
		},
		{
			id: "calories",
			label: "Hit calorie target",
			description: "Within ±20% of your daily goal.",
			done: hitCalories,
			icon: "trendingUp",
			href: "/dashboard",
		},
		{
			id: "protein",
			label: "Hit protein target",
			description: "Protein is the anchor macro. Nail it first.",
			done: hitProtein,
			icon: "sparkles",
			href: "/goal",
		},
	];

	const doneCount = habits.filter((h) => h.done).length;

	return (
		<div className="space-y-6 lg:space-y-8">
			<header className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
				<div className="space-y-2">
					<p className="eyebrow">Daily cadence</p>
					<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
						Habits & hydration
					</h1>
					<p className="max-w-2xl text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
						Daily check-ins to keep your streak alive.
					</p>
				</div>
				<div className="flex items-center gap-2">
					{streak && streak.currentStreak > 0 && (
						<div className="streak-gradient inline-flex items-center gap-2 rounded-2xl px-4 py-2 text-sm font-semibold text-white">
							<AnimatedIcon name="flame" size={16} />
							<span>{streak.currentStreak}-day streak</span>
						</div>
					)}
				</div>
			</header>

			<div className="grid gap-6 lg:grid-cols-[1.5fr_1fr]">
				<section className="glass-panel p-6 sm:p-8">
					<header className="mb-4 flex items-center justify-between">
						<div>
							<h2 className="text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
								Today's check-in
							</h2>
							<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
								{doneCount} of {habits.length} complete
							</p>
						</div>
						<div
							className="relative h-12 w-12"
							style={{ transform: "rotate(-90deg)" }}
							aria-hidden="true"
						>
							<svg width="48" height="48" viewBox="0 0 48 48">
								<circle cx="24" cy="24" r="20" fill="transparent" className="ring-track" strokeWidth="5" />
								<circle
									cx="24"
									cy="24"
									r="20"
									fill="transparent"
									stroke="var(--color-verdigris-500)"
									strokeWidth="5"
									strokeLinecap="round"
									strokeDasharray={2 * Math.PI * 20}
									strokeDashoffset={2 * Math.PI * 20 * (1 - doneCount / habits.length)}
									style={{ transition: "stroke-dashoffset 900ms cubic-bezier(0.16, 1, 0.3, 1)" }}
								/>
							</svg>
						</div>
					</header>
					<ul className="space-y-2">
						{habits.map((habit) => {
							const body = (
								<div
									className={`flex items-center gap-3 rounded-3xl border p-4 transition-all ${
										habit.done
											? "border-verdigris-300 bg-verdigris-500/10 dark:border-verdigris-500/30 dark:bg-verdigris-500/5"
											: "border-charcoal-blue-200 bg-white/70 dark:border-white/10 dark:bg-charcoal-blue-950/60"
									}`}
								>
									<span
										className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-2xl ${
											habit.done
												? "bg-verdigris-600 text-white"
												: "border border-charcoal-blue-200 text-charcoal-blue-500 dark:border-white/10 dark:text-charcoal-blue-300"
										}`}
									>
										<AnimatedIcon name={habit.done ? "circleCheck" : habit.icon} size={16} />
									</span>
									<div className="min-w-0 flex-1">
										<p className="text-sm font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
											{habit.label}
										</p>
										<p className="text-[11px] text-charcoal-blue-500 dark:text-charcoal-blue-400">
											{habit.description}
										</p>
									</div>
									{habit.done ? (
										<span className="rounded-full bg-verdigris-600 px-2 py-0.5 text-[10px] font-semibold uppercase tracking-[0.14em] text-white">
											Done
										</span>
									) : habit.href ? (
										<AnimatedIcon name="arrowRight" size={14} />
									) : null}
								</div>
							);
							return (
								<li key={habit.id}>
									{habit.href && !habit.done ? (
										<Link href={habit.href} className="block">
											{body}
										</Link>
									) : (
										body
									)}
								</li>
							);
						})}
					</ul>
				</section>

				<HydrationTracker />
			</div>
		</div>
	);
}
