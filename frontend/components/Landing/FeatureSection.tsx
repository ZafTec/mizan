import { AnimatedIcon } from "@/components/ui/animated-icon";

// Asymmetric feature grid wired to the project palette. Color accents use
// brand (verdigris), burnt-peach, tuscan-sun, and sandy-brown tokens so the
// section responds to both light and dark modes.

type Feature = {
	title: string;
	description: string;
	tone: "brand" | "peach" | "sun" | "sand";
};

const COL_LEFT: Feature[] = [
	{ title: "Nutrition tracking", description: "Log a meal in seconds. Macros, calories, and fibre calculated as you type.", tone: "brand" },
	{ title: "Recipe workshop", description: "Build reusable dishes with per-serving macros that scale when you cook.", tone: "peach" },
];

const COL_MIDDLE: Feature[] = [
	{ title: "Weekly planner", description: "Plan a full week, export a grouped shopping list in one tap.", tone: "sun" },
	{ title: "Workouts + body data", description: "Log lifts, track volume, and watch your body composition line move.", tone: "brand" },
];

const COL_RIGHT: Feature[] = [
	{ title: "Trainer collaboration", description: "Real-time chat, shared goals, and progress signed off by someone who knows you.", tone: "sand" },
	{ title: "Household sharing", description: "Invite up to 6 members. Share recipes, meal plans, shopping lists.", tone: "peach" },
];

const TONE_BAR = {
	brand: "bg-brand-500",
	peach: "bg-burnt-peach-500",
	sun: "bg-tuscan-sun-500",
	sand: "bg-sandy-brown-500",
};

function FeatureCard({ feature, large = false }: { feature: Feature; large?: boolean }) {
	return (
		<article className={`card-hover group ${large ? "p-7 sm:p-8" : "p-5 sm:p-6"}`}>
			<span className={`mb-4 inline-block h-1 w-10 rounded-full ${TONE_BAR[feature.tone]} opacity-80`} aria-hidden="true" />
			<h3 className={`mb-1.5 font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 ${large ? "text-xl sm:text-2xl" : "text-lg"}`}>
				{feature.title}
			</h3>
			<p className="text-sm leading-relaxed text-charcoal-blue-600 dark:text-charcoal-blue-400">
				{feature.description}
			</p>
		</article>
	);
}

function AiCoachSpotlight() {
	return (
		<article className="card relative overflow-hidden p-7 sm:p-8 md:col-span-2">
			<div
				aria-hidden="true"
				className="pointer-events-none absolute -right-20 -top-20 h-64 w-64 rounded-full"
				style={{
					background: "radial-gradient(closest-side, color-mix(in oklab, var(--color-brand-500) 22%, transparent), transparent 70%)",
					filter: "blur(50px)",
				}}
			/>
			<div className="relative grid grid-cols-1 gap-6 md:grid-cols-[1fr_1.1fr] md:items-center">
				<div>
					<span className="mb-4 inline-block h-1 w-10 rounded-full bg-brand-500 opacity-80" aria-hidden="true" />
					<h3 className="mb-2 text-xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-2xl">
						AI coach that reads your week.
					</h3>
					<p className="text-sm leading-relaxed text-charcoal-blue-600 dark:text-charcoal-blue-400">
						Ask anything in plain English. It knows your last week of meals, your goal, and your training, and answers like a trainer, not a search engine.
					</p>
				</div>
				<div className="space-y-3 rounded-[22px] bg-charcoal-blue-50 p-4 dark:bg-charcoal-blue-900/60">
					<div className="flex items-start gap-2">
						<div className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-charcoal-blue-200 text-xs font-semibold text-charcoal-blue-700 dark:bg-charcoal-blue-800 dark:text-charcoal-blue-200">
							E
						</div>
						<div className="rounded-2xl bg-charcoal-blue-100 px-3.5 py-2.5 text-sm text-charcoal-blue-800 dark:bg-charcoal-blue-800 dark:text-charcoal-blue-100">
							Why did my weight stall this week?
						</div>
					</div>
					<div className="flex items-start gap-2">
						<div className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-brand-600 text-xs font-semibold text-white dark:bg-brand-500">
							M
						</div>
						<div className="rounded-2xl bg-brand-500/10 px-3.5 py-2.5 text-sm text-charcoal-blue-800 dark:text-charcoal-blue-100">
							Weekend carbs averaged 320g vs. 210g on weekdays: fluid shift, not fat. Your 7-day avg is still <span className="font-semibold text-brand-700 dark:text-brand-300">-0.3 kg</span>. Stay the course.
						</div>
					</div>
					<div className="flex items-center gap-2 pl-9">
						<span className="inline-flex gap-1">
							<span className="h-1.5 w-1.5 animate-pulse rounded-full bg-brand-500" />
							<span className="h-1.5 w-1.5 animate-pulse rounded-full bg-brand-500" style={{ animationDelay: "120ms" }} />
							<span className="h-1.5 w-1.5 animate-pulse rounded-full bg-brand-500" style={{ animationDelay: "240ms" }} />
						</span>
						<span className="text-xs font-medium uppercase tracking-[0.14em] text-charcoal-blue-500 dark:text-charcoal-blue-400">
							typing
						</span>
					</div>
				</div>
			</div>
		</article>
	);
}

export function FeatureSection() {
	return (
		<section aria-labelledby="feature-heading" className="py-12 sm:py-16">
			<div className="mb-10 max-w-3xl sm:mb-12">
				<div className="eyebrow mb-4">
					<AnimatedIcon name="sparkles" size={14} aria-hidden="true" />
					Six instruments
				</div>
				<h2 id="feature-heading" className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
					One workspace. Built like an instrument, not a spreadsheet.
				</h2>
				<p className="mt-3 max-w-2xl text-sm leading-relaxed text-charcoal-blue-500 dark:text-charcoal-blue-400 sm:text-base">
					Stop stitching together three tools. Meals, training, and coaching share one canvas so progress compounds.
				</p>
			</div>

			<div className="grid grid-cols-1 gap-4 md:grid-cols-2">
				<AiCoachSpotlight />
			</div>

			<div className="mt-4 grid grid-cols-1 gap-4 md:grid-cols-3">
				<div className="space-y-4">{COL_LEFT.map((f) => <FeatureCard key={f.title} feature={f} />)}</div>
				<div className="space-y-4 md:translate-y-8">{COL_MIDDLE.map((f) => <FeatureCard key={f.title} feature={f} />)}</div>
				<div className="space-y-4">{COL_RIGHT.map((f) => <FeatureCard key={f.title} feature={f} />)}</div>
			</div>
		</section>
	);
}
