import { AnimatedIcon, type AnimatedIconName } from "@/components/ui/animated-icon";

const features: Array<{
	icon: AnimatedIconName;
	title: string;
	description: string;
	gradient: string;
}> = [
	{
		icon: "chartLine",
		title: "Nutrition tracking",
		description: "Log meals in seconds. Macros, calories, and fibre calculated automatically.",
		gradient: "bg-brand-600",
	},
	{
		icon: "cookingPot",
		title: "Recipe workshop",
		description: "Build reusable dishes with per-serving macros that scale when you cook.",
		gradient: "bg-accent-600",
	},
	{
		icon: "calendarCheck",
		title: "Weekly planner",
		description: "Plan a full week of meals, export a grouped shopping list in one tap.",
		gradient: "bg-brand-700",
	},
	{
		icon: "activity",
		title: "Workouts + body data",
		description: "Log lifts, track volume, and watch your body composition line move.",
		gradient: "bg-charcoal-blue-900 dark:bg-charcoal-blue-100 dark:text-charcoal-blue-900",
	},
	{
		icon: "brain",
		title: "AI coach",
		description: "Ask a question, snap a photo of food, get a macro-aware suggestion.",
		gradient: "bg-accent-500",
	},
	{
		icon: "messageCircle",
		title: "Trainer collaboration",
		description: "Real-time chat, shared goals, and progress signed off by someone who knows you.",
		gradient: "bg-brand-500",
	},
];

export function FeatureSection() {
	return (
		<section data-testid="feature-section" aria-labelledby="feature-heading" className="py-12 sm:py-16">
			<div className="mb-10 text-center sm:mb-12">
				<div className="eyebrow mb-4">
					<AnimatedIcon name="sparkles" size={14} aria-hidden="true" />
					Every piece of your routine
				</div>
				<h2 id="feature-heading" className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
					One app for food, training, and the feedback loop.
				</h2>
				<p className="mx-auto mt-3 max-w-2xl text-charcoal-blue-500 dark:text-charcoal-blue-400">
					Stop stitching together three tools. Mizan keeps your meals, workouts, and coaching in one place so progress compounds.
				</p>
			</div>
			<div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
				{features.map((f, index) => (
					<article
						key={f.title}
						className="card-hover stagger-item group relative overflow-hidden p-6 sm:p-7"
						style={{ animationDelay: `${index * 60}ms` }}
					>
						<div className="pointer-events-none absolute -right-8 -top-8 h-32 w-32 rounded-full bg-brand-500/5 opacity-0 transition-opacity duration-300 ease-out group-hover:opacity-100" aria-hidden="true" />
						<div className={`relative mb-5 flex h-12 w-12 items-center justify-center rounded-2xl ${f.gradient} text-white shadow-lg shadow-charcoal-blue-950/10`}>
							<AnimatedIcon name={f.icon} size={20} aria-hidden="true" />
						</div>
						<h3 className="relative mb-2 text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">{f.title}</h3>
						<p className="relative text-sm leading-relaxed text-charcoal-blue-600 dark:text-charcoal-blue-400">{f.description}</p>
					</article>
				))}
			</div>
		</section>
	);
}
