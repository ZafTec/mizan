const features = [
	{
		icon: "ri-heart-pulse-line",
		title: "Track Nutrition",
		description: "Log meals and monitor macros with a clean, intuitive interface. AI-powered food recognition makes tracking effortless.",
		gradient: "from-brand-400 to-brand-600",
		shadow: "shadow-brand-500/25",
	},
	{
		icon: "ri-calendar-schedule-line",
		title: "Plan Meals",
		description: "Create weekly meal plans that hit your macro targets. Generate shopping lists automatically from your plans.",
		gradient: "from-accent-400 to-accent-600",
		shadow: "shadow-accent-500/25",
	},
	{
		icon: "ri-user-heart-line",
		title: "Get Coached",
		description: "Connect with certified trainers for personalized guidance. Real-time chat and goal tracking keep you accountable.",
		gradient: "from-violet-400 to-violet-600",
		shadow: "shadow-violet-500/25",
	},
];

export function FeatureSection() {
	return (
		<section data-testid="feature-section" className="py-8">
			<div className="text-center mb-10">
				<h2 className="text-3xl font-bold text-slate-900 dark:text-slate-100 tracking-tight">
					Everything you need to hit your goals
				</h2>
				<p className="mt-3 text-slate-500 dark:text-slate-400 max-w-xl mx-auto">
					From tracking to planning to coaching — one platform, zero friction.
				</p>
			</div>
			<div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
				{features.map((f) => (
					<div key={f.title} className="card-hover p-6 group">
						<div className={`w-12 h-12 rounded-2xl bg-linear-to-br ${f.gradient} flex items-center justify-center shadow-lg ${f.shadow} group-hover:scale-110 transition-transform mb-4`}>
							<i className={`${f.icon} text-2xl text-white`} />
						</div>
						<h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100 mb-2">{f.title}</h3>
						<p className="text-sm text-slate-500 dark:text-slate-400 leading-relaxed">{f.description}</p>
					</div>
				))}
			</div>
		</section>
	);
}
