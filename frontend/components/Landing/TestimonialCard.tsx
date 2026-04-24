const TESTIMONIALS = [
	{
		name: "Sarah M.",
		role: "Lost 12 kg in 4 months",
		quote: "The first tracker I've used that doesn't feel like homework. The ring + chips combo is exactly the level of detail I want.",
		initials: "SM",
		accent: "primary" as const,
		size: "lg" as const,
	},
	{
		name: "Daniel K.",
		role: "National-level cyclist",
		quote: "Finally, a macro tracker that handles periodised carbs properly.",
		initials: "DK",
		accent: "secondary" as const,
		size: "md" as const,
	},
	{
		name: "Amira T.",
		role: "Certified trainer, 120+ clients",
		quote: "I run my entire coaching practice through Mizan. The shared goal tracking is the whole reason I switched.",
		initials: "AT",
		accent: "tertiary" as const,
		size: "md" as const,
	},
	{
		name: "Mesfin B.",
		role: "Home cook, weekend athlete",
		quote: "The recipe workshop with scaling is art.",
		initials: "MB",
		accent: "brown" as const,
		size: "sm" as const,
	},
];

const ACCENT_COLOR = {
	primary: "var(--eth-primary)",
	secondary: "var(--eth-secondary)",
	tertiary: "var(--eth-tertiary)",
	brown: "#D8B889",
};

export function TestimonialSection() {
	return (
		<section aria-labelledby="testimonial-heading" className="py-16 sm:py-24">
			<div className="mb-12 max-w-3xl sm:mb-16">
				<span className="eth-label eth-text-primary">Real outcomes</span>
				<h2 id="testimonial-heading" className="eth-display-md mt-3">
					People who take this seriously.
				</h2>
			</div>

			{/* Asymmetric masonry — sizes and rows intentionally uneven. */}
			<div className="grid grid-cols-1 gap-5 md:grid-cols-6">
				{TESTIMONIALS.map((t, i) => {
					const accent = ACCENT_COLOR[t.accent];
					const span = t.size === "lg" ? "md:col-span-4" : t.size === "md" ? "md:col-span-3" : "md:col-span-2";
					const offset = i === 1 ? "md:translate-y-8" : i === 3 ? "md:-translate-y-6" : "";
					return (
						<article
							key={t.name}
							className={`eth-card relative ${span} ${offset} p-7 sm:p-8`}
							style={{ background: "var(--eth-surface-container)" }}
						>
							<i
								className="ri-double-quotes-l text-3xl opacity-20 mb-3 inline-block"
								style={{ color: accent }}
								aria-hidden="true"
							/>
							<p
								className={`${t.size === "lg" ? "eth-headline-md" : "eth-body-lg"} italic mb-5`}
								style={{ color: "var(--eth-on-surface)" }}
							>
								{t.quote}
							</p>
							<div className="flex items-center gap-3">
								<div
									className="flex h-10 w-10 items-center justify-center rounded-full text-sm font-semibold"
									style={{
										background: `color-mix(in oklab, ${accent} 25%, var(--eth-surface-high))`,
										color: accent,
									}}
								>
									{t.initials}
								</div>
								<div>
									<p className="eth-body-md font-semibold">{t.name}</p>
									<p className="eth-label eth-text-dim">{t.role}</p>
								</div>
							</div>
						</article>
					);
				})}
			</div>
		</section>
	);
}
