const STEPS = [
	{
		num: "01",
		title: "Log without thinking",
		description: "Search a food, snap a photo, or paste a recipe. Macros land on screen before you finish typing.",
		icon: "ri-flashlight-line",
	},
	{
		num: "02",
		title: "Plan the week",
		description: "Drag meals onto a calendar. A grouped shopping list falls out of the other side.",
		icon: "ri-calendar-line",
	},
	{
		num: "03",
		title: "Adjust with data",
		description: "Weekly trends, streaks, and AI suggestions nudge you toward the goal you set.",
		icon: "ri-line-chart-line",
	},
];

// Three strokes, one line. Each step is a tall glass column. Step numbers
// bleed out of the card top-left. No 1px connectors — translucent teal
// glows flow between them instead.
export function HowItWorksSection() {
	return (
		<section aria-labelledby="how-heading" className="relative py-16 sm:py-24">
			<div className="mb-12 max-w-3xl sm:mb-16">
				<span className="eth-label eth-text-primary">Three strokes</span>
				<h2 id="how-heading" className="eth-display-md mt-3">
					From first log to visible progress.
				</h2>
			</div>

			{/* Ambient flow between columns — replaces the 1px connector. */}
			<div
				aria-hidden="true"
				className="pointer-events-none absolute inset-x-0 top-1/2 -z-10 h-60 -translate-y-1/2"
				style={{
					background: "radial-gradient(ellipse at center, rgba(110, 235, 224, 0.06), transparent 70%)",
					filter: "blur(50px)",
				}}
			/>

			<ol className="relative grid grid-cols-1 gap-6 md:grid-cols-3 md:gap-8">
				{STEPS.map((step, i) => (
					<li
						key={step.num}
						className="relative"
						style={{ transform: i === 1 ? "translateY(32px)" : "translateY(0)" }}
					>
						{/* Number bleeds out of the card top-left corner. */}
						<span
							className="absolute -top-6 -left-2 eth-display-md eth-text-primary opacity-80 pointer-events-none select-none"
							aria-hidden="true"
							style={{ fontVariantNumeric: "tabular-nums" }}
						>
							{step.num}
						</span>
						<div
							className="eth-card relative h-full pt-14"
							style={{ background: "var(--eth-surface-low)" }}
						>
							<div
								className="mb-5 inline-flex h-10 w-10 items-center justify-center rounded-2xl"
								style={{
									background: "rgba(110, 235, 224, 0.14)",
									color: "var(--eth-primary)",
								}}
							>
								<i className={`${step.icon} text-lg`} aria-hidden="true" />
							</div>
							<h3 className="eth-headline-md mb-2">{step.title}</h3>
							<p className="eth-body-md eth-text-muted">{step.description}</p>
						</div>
					</li>
				))}
			</ol>
		</section>
	);
}
