import Link from "next/link";
import { getUserServer } from "@/helper/session";
import { getCurrentGoal } from "@/data/goal";
import { getTodayMeal, getDailyTotals, type MealEntry } from "@/data/meal";
import { getStreak } from "@/data/achievement";
import { AnimatedIcon } from "@/components/ui/animated-icon";
import MacroRing from "@/components/Dashboard/MacroRing";
import QuickActions from "@/components/Dashboard/QuickActions";
import { cn } from "@/lib/utils";

export const dynamic = "force-dynamic";

function greetingForHour(hour: number) {
	if (hour < 5) return "Still up?";
	if (hour < 12) return "Good morning";
	if (hour < 17) return "Good afternoon";
	if (hour < 21) return "Good evening";
	return "Good night";
}

function formatTime(loggedAt: string) {
	try {
		const d = new Date(loggedAt);
		return d.toLocaleTimeString([], { hour: "numeric", minute: "2-digit" });
	} catch {
		return "";
	}
}

function mealTypeColor(type: string | undefined): string {
	switch ((type || "").toLowerCase()) {
		case "breakfast":
			return "macro-chip-carbs";
		case "lunch":
			return "macro-chip-protein";
		case "dinner":
			return "macro-chip-calories";
		case "snack":
			return "macro-chip-fat";
		default:
			return "macro-chip-protein";
	}
}

export default async function DashboardPage() {
	const user = await getUserServer();

	const [goal, meals, totals, streak] = await Promise.all([
		getCurrentGoal(),
		getTodayMeal(),
		getDailyTotals(),
		getStreak(),
	]);

	const now = new Date();
	const greeting = greetingForHour(now.getHours());
	const firstName = user.name?.split(" ")[0] || user.email?.split("@")[0] || "there";
	const dateLabel = now.toLocaleDateString(undefined, {
		weekday: "long",
		month: "long",
		day: "numeric",
	});

	const caloriesCurrent = totals?.calories ?? 0;
	const proteinCurrent = totals?.protein ?? 0;
	const carbsCurrent = totals?.carbs ?? 0;
	const fatCurrent = totals?.fat ?? 0;

	const targetCalories = goal?.targetCalories ?? null;
	const targetProtein = goal?.targetProteinGrams ?? null;
	const targetCarbs = goal?.targetCarbsGrams ?? null;
	const targetFat = goal?.targetFatGrams ?? null;

	const sortedMeals: MealEntry[] = [...meals].sort((a, b) =>
		new Date(a.loggedAt).getTime() - new Date(b.loggedAt).getTime()
	);
	const shownMeals = sortedMeals.slice(0, 5);

	const currentStreak = streak?.currentStreak ?? 0;
	const longestStreak = streak?.longestStreak ?? 0;
	const isActiveToday = streak?.isActiveToday ?? false;

	return (
		<div className="space-y-6 lg:space-y-8">
			{/* Greeting */}
			<section className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
				<div className="space-y-2">
					<p className="eyebrow">Today • {dateLabel}</p>
					<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
						{greeting}, {firstName}
					</h1>
					<p className="max-w-xl text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
						{goal
							? "Here's where your macros stand right now. Stay on target."
							: "Set a goal to unlock personalized macro targets and progress tracking."}
					</p>
				</div>
				<div className="flex items-center gap-2">
					{currentStreak > 0 && (
						<div className="streak-gradient inline-flex items-center gap-2 rounded-2xl px-4 py-2 text-sm font-semibold text-white">
							<AnimatedIcon name="flame" size={16} />
							<span>{currentStreak}-day streak</span>
						</div>
					)}
					<Link
						href="/goal"
						className="btn-secondary !rounded-2xl !py-2 text-sm"
					>
						{goal ? "Edit goal" : "Set goal"}
						<AnimatedIcon name="arrowRight" size={14} />
					</Link>
				</div>
			</section>

			{/* Main grid */}
			<div className="grid gap-6 lg:grid-cols-[1.65fr_1fr]">
				{/* Macro rings panel */}
				<section className="glass-panel p-6 sm:p-8">
					<header className="mb-6 flex items-center justify-between">
						<div>
							<h2 className="text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
								Macros today
							</h2>
							<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
								{goal ? "Tracking against your active goal" : "Targets appear once you set a goal"}
							</p>
						</div>
						<Link
							href="/meals"
							className="btn-ghost !rounded-2xl !py-2 text-sm"
						>
							View diary
							<AnimatedIcon name="arrowRight" size={14} />
						</Link>
					</header>

					<div className="flex flex-col items-center gap-6 sm:flex-row sm:items-start sm:justify-between">
						<div className="shrink-0">
							<MacroRing
								label="Calories"
								current={caloriesCurrent}
								target={targetCalories}
								unit="kcal"
								tone="calories"
								size={220}
							/>
						</div>
						<div className="grid flex-1 grid-cols-3 gap-3 sm:gap-4">
							<MacroRing
								label="Protein"
								current={proteinCurrent}
								target={targetProtein}
								unit="g"
								tone="protein"
							/>
							<MacroRing
								label="Carbs"
								current={carbsCurrent}
								target={targetCarbs}
								unit="g"
								tone="carbs"
							/>
							<MacroRing
								label="Fat"
								current={fatCurrent}
								target={targetFat}
								unit="g"
								tone="fat"
							/>
						</div>
					</div>
				</section>

				{/* Streak + habits card */}
				<section className="glass-panel flex flex-col gap-4 p-6 sm:p-8">
					<header>
						<h2 className="text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
							Consistency
						</h2>
						<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
							Your streak and today's check-ins
						</p>
					</header>

					<div className="grid grid-cols-2 gap-3">
						<div className="rounded-3xl border border-charcoal-blue-200 bg-white/70 p-4 dark:border-white/10 dark:bg-charcoal-blue-950/60">
							<p className="text-[10px] font-semibold uppercase tracking-[0.18em] text-charcoal-blue-500 dark:text-charcoal-blue-400">
								Current
							</p>
							<p className="mt-1 text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50">
								{currentStreak}
								<span className="ml-1 text-xs font-medium text-charcoal-blue-500">days</span>
							</p>
							<p
								className={cn(
									"mt-1 inline-flex items-center gap-1 text-[11px] font-medium",
									isActiveToday
										? "text-verdigris-700 dark:text-verdigris-300"
										: "text-charcoal-blue-500 dark:text-charcoal-blue-400"
								)}
							>
								<AnimatedIcon name={isActiveToday ? "circleCheck" : "calendarCheck"} size={12} />
								{isActiveToday ? "Logged today" : "Not logged yet"}
							</p>
						</div>
						<div className="rounded-3xl border border-charcoal-blue-200 bg-white/70 p-4 dark:border-white/10 dark:bg-charcoal-blue-950/60">
							<p className="text-[10px] font-semibold uppercase tracking-[0.18em] text-charcoal-blue-500 dark:text-charcoal-blue-400">
								Longest
							</p>
							<p className="mt-1 text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50">
								{longestStreak}
								<span className="ml-1 text-xs font-medium text-charcoal-blue-500">days</span>
							</p>
							<p className="mt-1 text-[11px] text-charcoal-blue-500 dark:text-charcoal-blue-400">
								Personal best
							</p>
						</div>
					</div>

					<div className="space-y-3 rounded-3xl border border-charcoal-blue-200 bg-white/70 p-4 dark:border-white/10 dark:bg-charcoal-blue-950/60">
						<p className="text-[10px] font-semibold uppercase tracking-[0.18em] text-charcoal-blue-500 dark:text-charcoal-blue-400">
							Quick jump
						</p>
						<div className="grid grid-cols-2 gap-2">
							<Link href="/achievements" className="btn-ghost !rounded-2xl !py-2 text-xs">
								<AnimatedIcon name="sparkles" size={14} />
								Achievements
							</Link>
							<Link href="/goal/dashboard" className="btn-ghost !rounded-2xl !py-2 text-xs">
								<AnimatedIcon name="trendingUp" size={14} />
								Progress
							</Link>
							<Link href="/meal-plan" className="btn-ghost !rounded-2xl !py-2 text-xs">
								<AnimatedIcon name="calendarCheck" size={14} />
								Meal plan
							</Link>
							<Link href="/habits" className="btn-ghost !rounded-2xl !py-2 text-xs">
								<AnimatedIcon name="circleCheck" size={14} />
								Habits
							</Link>
						</div>
					</div>
				</section>
			</div>

			{/* Quick Actions */}
			<section>
				<div className="mb-3 flex items-end justify-between">
					<div>
						<h2 className="text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
							Quick actions
						</h2>
						<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
							One tap to log or train
						</p>
					</div>
				</div>
				<QuickActions />
			</section>

			{/* Today's meals timeline */}
			<section className="glass-panel p-6 sm:p-8">
				<header className="mb-4 flex items-center justify-between">
					<div>
						<h2 className="text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
							Today's timeline
						</h2>
						<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
							{meals.length === 0
								? "No meals logged yet"
								: `${meals.length} meal${meals.length === 1 ? "" : "s"} logged`}
						</p>
					</div>
					<Link href="/meals/add" className="btn-primary !rounded-2xl !py-2 text-sm">
						<AnimatedIcon name="flame" size={14} />
						Log meal
					</Link>
				</header>

				{shownMeals.length === 0 ? (
					<div className="flex flex-col items-center justify-center gap-3 rounded-3xl border border-dashed border-charcoal-blue-300 bg-white/40 py-10 text-center dark:border-white/10 dark:bg-charcoal-blue-950/30">
						<span className="icon-chip h-12 w-12 text-charcoal-blue-400">
							<AnimatedIcon name="cookingPot" size={20} />
						</span>
						<p className="text-sm font-medium text-charcoal-blue-700 dark:text-charcoal-blue-200">
							Nothing logged yet today
						</p>
						<p className="max-w-xs text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
							Log what you eat to see macros fill your rings in real time.
						</p>
						<Link href="/meals/add" className="btn-primary !rounded-2xl !py-2 text-sm">
							Log your first meal
							<AnimatedIcon name="arrowRight" size={14} />
						</Link>
					</div>
				) : (
					<ul className="space-y-3">
						{shownMeals.map((meal) => (
							<li
								key={meal.id}
								className="flex items-center gap-4 rounded-3xl border border-charcoal-blue-200 bg-white/80 p-4 transition-colors hover:border-charcoal-blue-300 dark:border-white/10 dark:bg-charcoal-blue-950/60 dark:hover:border-white/15"
							>
								<span
									className={cn(
										"inline-flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl text-sm font-semibold",
										mealTypeColor(meal.mealType)
									)}
								>
									{meal.mealType?.[0]?.toUpperCase() ?? "•"}
								</span>
								<div className="min-w-0 flex-1">
									<div className="flex items-center gap-2">
										<p className="truncate text-sm font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
											{meal.name}
										</p>
										<span className="rounded-full border border-charcoal-blue-200 px-2 py-0.5 text-[10px] uppercase tracking-[0.14em] text-charcoal-blue-500 dark:border-white/10 dark:text-charcoal-blue-400">
											{meal.mealType || "meal"}
										</span>
									</div>
									<p className="mt-1 text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
										{formatTime(meal.loggedAt)} · {meal.servings ?? 1}× serving
									</p>
								</div>
								<div className="hidden items-center gap-3 text-xs font-medium text-charcoal-blue-600 sm:flex">
									<span className="macro-chip-calories rounded-full px-2 py-0.5">
										{Math.round(meal.calories ?? 0)} kcal
									</span>
									<span className="macro-chip-protein rounded-full px-2 py-0.5">
										{Math.round(meal.proteinGrams ?? 0)}p
									</span>
									<span className="macro-chip-carbs rounded-full px-2 py-0.5">
										{Math.round(meal.carbsGrams ?? 0)}c
									</span>
									<span className="macro-chip-fat rounded-full px-2 py-0.5">
										{Math.round(meal.fatGrams ?? 0)}f
									</span>
								</div>
							</li>
						))}
					</ul>
				)}

				{meals.length > shownMeals.length && (
					<div className="mt-4 text-center">
						<Link href="/meals" className="btn-ghost !rounded-2xl !py-2 text-sm">
							View full diary
							<AnimatedIcon name="arrowRight" size={14} />
						</Link>
					</div>
				)}
			</section>
		</div>
	);
}
