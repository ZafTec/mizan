import { AnimatedIcon } from "@/components/ui/animated-icon";

const STEPS = [
	{ num: "01", title: "Log without thinking", description: "Search a food, snap a photo, or paste a recipe. Macros land on screen before you finish typing." },
	{ num: "02", title: "Plan the week", description: "Drag meals onto a calendar. A grouped shopping list falls out of the other side." },
	{ num: "03", title: "Adjust with data", description: "Weekly trends, streaks, and AI suggestions nudge you toward the goal you set." },
];

export function HowItWorksSection() {
	return (
		<section aria-labelledby="how-heading" className="py-12 sm:py-16">
			<div className="mb-10 max-w-3xl sm:mb-12">
				<div className="eyebrow mb-4">
					<AnimatedIcon name="rocket" size={14} aria-hidden="true" />
					Three strokes
				</div>
				<h2 id="how-heading" className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
					From first log to visible progress.
				</h2>
			</div>
			<ol className="grid grid-cols-1 gap-4 md:grid-cols-3">
				{STEPS.map((step) => (
					<li key={step.num} className="card flex items-start gap-5 p-6">
						<span
							className="shrink-0 font-bold leading-none text-brand-600 opacity-80 dark:text-brand-300"
							aria-hidden="true"
							style={{ fontSize: "2.25rem", letterSpacing: "-0.02em", fontVariantNumeric: "tabular-nums" }}
						>
							{step.num}
						</span>
						<div>
							<h3 className="text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
								{step.title}
							</h3>
							<p className="mt-1.5 text-sm leading-relaxed text-charcoal-blue-600 dark:text-charcoal-blue-400">
								{step.description}
							</p>
						</div>
					</li>
				))}
			</ol>
		</section>
	);
}
