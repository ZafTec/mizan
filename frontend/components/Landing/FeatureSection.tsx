import { AnimatedIcon, type AnimatedIconName } from "@/components/ui/animated-icon";

const features = [
	{
		icon: "chartLine" as AnimatedIconName,
		title: "Track Nutrition",
		description: "Log meals and monitor macros with a clean, intuitive interface. AI-powered food recognition makes tracking effortless.",
		gradient: "bg-brand-600",
		shadow: "shadow-brand-500/20",
	},
	{
		icon: "calendarCheck" as AnimatedIconName,
		title: "Plan Meals",
		description: "Create weekly meal plans that hit your macro targets. Generate shopping lists automatically from your plans.",
		gradient: "bg-accent-600",
		shadow: "shadow-accent-500/20",
	},
	{
		icon: "users" as AnimatedIconName,
		title: "Get Coached",
		description: "Connect with certified trainers for personalized guidance. Real-time chat and goal tracking keep you accountable.",
		gradient: "bg-slate-900 dark:bg-slate-100 dark:text-slate-900",
		shadow: "shadow-brand-500/20",
	},
];

export function FeatureSection() {
	return (
		<section data-testid="feature-section" className="py-8">
			<div className="mb-10 text-center">
				<div className="eyebrow mb-4">
					<AnimatedIcon name="sparkles" size={14} aria-hidden="true" />
					Daily clarity
				</div>
				<h2 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
					Everything you need to hit your goals
				</h2>
				<p className="mx-auto mt-3 max-w-2xl text-charcoal-blue-500 dark:text-charcoal-blue-400">
					From tracking to planning to coaching — one platform, zero friction.
				</p>
			</div>
			<div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
				{features.map((f, index) => (
					<div key={f.title} className="card-hover stagger-item group p-6 sm:p-7" style={{ animationDelay: `${index * 90}ms` }}>
						<div className={`mb-5 flex h-14 w-14 items-center justify-center rounded-2xl ${f.gradient} text-white shadow-lg ${f.shadow}`}>
							<AnimatedIcon name={f.icon} size={22} aria-hidden="true" />
						</div>
						<h3 className="mb-2 text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">{f.title}</h3>
						<p className="text-sm leading-relaxed text-slate-600 dark:text-slate-400">{f.description}</p>
					</div>
				))}
			</div>
		</section>
	);
}
