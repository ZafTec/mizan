import { AnimatedIcon } from "@/components/ui/animated-icon";

const testimonials = [
	{
		name: "Sarah M.",
		role: "Lost 12kg in 4 months",
		quote: "The meal planning feature changed how I approach nutrition. I actually look forward to cooking now.",
		initials: "SM",
		gradient: "bg-brand-600",
	},
	{
		name: "Daniel K.",
		role: "Competitive athlete",
		quote: "Finally, a tracker that handles macros properly. The recipe builder with automatic nutrition calculation is genius.",
		initials: "DK",
		gradient: "bg-accent-600",
	},
	{
		name: "Amira T.",
		role: "Certified trainer",
		quote: "I manage all my clients through Mizan. The real-time chat and goal tracking make remote coaching seamless.",
		initials: "AT",
		gradient: "bg-slate-900 dark:bg-slate-100 dark:text-slate-900",
	},
];

export function TestimonialSection() {
	return (
		<section data-testid="testimonial-section" className="py-8">
			<div className="mb-10 text-center">
				<div className="eyebrow mb-4">
					<AnimatedIcon name="heart" size={14} aria-hidden="true" />
					Real outcomes
				</div>
				<h2 className="text-3xl font-semibold tracking-tight text-slate-950 dark:text-slate-50 sm:text-4xl">
					Trusted by people who take nutrition seriously
				</h2>
			</div>
			<div className="grid grid-cols-1 md:grid-cols-3 gap-6">
				{testimonials.map((t, index) => (
					<div key={t.name} className="card stagger-item p-6" style={{ animationDelay: `${index * 90}ms` }}>
						<div className="mb-4 flex items-center justify-between">
							<div className="flex items-center gap-3">
								<div className={`flex h-10 w-10 items-center justify-center rounded-full ${t.gradient} text-sm font-semibold text-white`}>
									{t.initials}
								</div>
								<div>
									<p className="text-sm font-semibold text-slate-900 dark:text-slate-100">{t.name}</p>
									<p className="text-xs text-slate-500 dark:text-slate-400">{t.role}</p>
								</div>
							</div>
							<span className="icon-chip h-9 w-9 text-brand-600 dark:text-brand-300">
								<AnimatedIcon name="circleCheck" size={16} aria-hidden="true" />
							</span>
						</div>
						<p className="text-sm text-slate-600 dark:text-slate-400 leading-relaxed italic">&ldquo;{t.quote}&rdquo;</p>
					</div>
				))}
			</div>
		</section>
	);
}
