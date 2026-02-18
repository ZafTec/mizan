const testimonials = [
	{
		name: "Sarah M.",
		role: "Lost 12kg in 4 months",
		quote: "The meal planning feature changed how I approach nutrition. I actually look forward to cooking now.",
		initials: "SM",
		gradient: "from-brand-400 to-brand-600",
	},
	{
		name: "Daniel K.",
		role: "Competitive athlete",
		quote: "Finally, a tracker that handles macros properly. The recipe builder with automatic nutrition calculation is genius.",
		initials: "DK",
		gradient: "from-accent-400 to-accent-600",
	},
	{
		name: "Amira T.",
		role: "Certified trainer",
		quote: "I manage all my clients through Mizan. The real-time chat and goal tracking make remote coaching seamless.",
		initials: "AT",
		gradient: "from-violet-400 to-violet-600",
	},
];

export function TestimonialSection() {
	return (
		<section data-testid="testimonial-section" className="py-8">
			<div className="text-center mb-10">
				<h2 className="text-3xl font-bold text-slate-900 dark:text-slate-100 tracking-tight">
					Trusted by people who take nutrition seriously
				</h2>
			</div>
			<div className="grid grid-cols-1 md:grid-cols-3 gap-6">
				{testimonials.map((t) => (
					<div key={t.name} className="card p-6">
						<div className="flex items-center gap-3 mb-4">
							<div className={`w-10 h-10 rounded-full bg-linear-to-br ${t.gradient} flex items-center justify-center text-white text-sm font-semibold`}>
								{t.initials}
							</div>
							<div>
								<p className="text-sm font-semibold text-slate-900 dark:text-slate-100">{t.name}</p>
								<p className="text-xs text-slate-500 dark:text-slate-400">{t.role}</p>
							</div>
						</div>
						<p className="text-sm text-slate-600 dark:text-slate-400 leading-relaxed italic">&ldquo;{t.quote}&rdquo;</p>
					</div>
				))}
			</div>
		</section>
	);
}
