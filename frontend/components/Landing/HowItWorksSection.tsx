import { AnimatedIcon, type AnimatedIconName } from "@/components/ui/animated-icon";

const steps: Array<{
	title: string;
	description: string;
	icon: AnimatedIconName;
}> = [
	{
		title: "Log without thinking",
		description: "Search a food, snap a photo, or paste a recipe. Macros are on screen before you finish reading this step.",
		icon: "flame",
	},
	{
		title: "Plan the week",
		description: "Drag meals onto a calendar. A shopping list, grouped by aisle, falls out the other side.",
		icon: "calendarCheck",
	},
	{
		title: "Adjust with data",
		description: "Weekly trends, streaks, and AI suggestions nudge you toward the goal you actually set.",
		icon: "trendingUp",
	},
];

export function HowItWorksSection() {
	return (
		<section aria-labelledby="how-it-works-heading" className="py-12 sm:py-16">
			<div className="mb-10 text-center sm:mb-12">
				<div className="eyebrow mb-4">
					<AnimatedIcon name="rocket" size={14} aria-hidden="true" />
					Three short steps
				</div>
				<h2
					id="how-it-works-heading"
					className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl"
				>
					From first log to visible progress.
				</h2>
			</div>
			<ol className="relative grid grid-cols-1 gap-6 sm:grid-cols-3">
				{/* Connector line between steps on desktop. Decorative only. */}
				<div
					className="pointer-events-none absolute left-0 right-0 top-6 hidden h-px sm:block"
					style={{
						background:
							"linear-gradient(to right, transparent, var(--color-brand-400, #f5a3a3) 20%, var(--color-brand-400, #f5a3a3) 80%, transparent)",
					}}
					aria-hidden="true"
				/>
				{steps.map((step, index) => (
					<li
						key={step.title}
						className="relative stagger-item"
						style={{ animationDelay: `${index * 90}ms` }}
					>
						<div className="relative z-10 mx-auto mb-4 flex h-12 w-12 items-center justify-center rounded-full border border-brand-500/20 bg-white text-sm font-semibold text-brand-700 shadow-lg shadow-brand-500/10 dark:bg-charcoal-blue-900 dark:text-brand-300">
							{String(index + 1).padStart(2, "0")}
						</div>
						<div className="card p-6 text-center">
							<div className="mb-4 inline-flex h-11 w-11 items-center justify-center rounded-2xl bg-brand-500/10 text-brand-700 dark:bg-brand-500/15 dark:text-brand-300">
								<AnimatedIcon name={step.icon} size={20} aria-hidden="true" />
							</div>
							<h3 className="mb-2 text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
								{step.title}
							</h3>
							<p className="text-sm leading-relaxed text-charcoal-blue-600 dark:text-charcoal-blue-400">
								{step.description}
							</p>
						</div>
					</li>
				))}
			</ol>
		</section>
	);
}
