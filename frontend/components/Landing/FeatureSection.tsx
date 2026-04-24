// Six instruments. The middle column is vertically offset to break the grid.
// AI coach spans wider (simulated chat transcript). No dropshadows; tonal
// separation + ambient glow only.

type Feature = {
	icon: string;
	title: string;
	description: string;
	tone: "primary" | "secondary" | "tertiary" | "brown";
};

const COL_LEFT: Feature[] = [
	{
		icon: "ri-line-chart-line",
		title: "Nutrition tracking",
		description: "Log a meal in seconds. Macros, calories, and fibre calculated as you type.",
		tone: "primary",
	},
	{
		icon: "ri-restaurant-line",
		title: "Recipe workshop",
		description: "Build reusable dishes with per-serving macros that scale when you cook.",
		tone: "secondary",
	},
];

const COL_MIDDLE: Feature[] = [
	{
		icon: "ri-calendar-check-line",
		title: "Weekly planner",
		description: "Plan a full week, export a grouped shopping list in one tap.",
		tone: "tertiary",
	},
	{
		icon: "ri-pulse-line",
		title: "Workouts + body data",
		description: "Log lifts, track volume, and watch your body composition line move.",
		tone: "primary",
	},
];

const COL_RIGHT: Feature[] = [
	{
		icon: "ri-team-line",
		title: "Trainer collaboration",
		description: "Real-time chat, shared goals, and progress signed off by someone who knows you.",
		tone: "brown",
	},
	{
		icon: "ri-home-smile-line",
		title: "Household sharing",
		description: "Invite up to 6 members. Share recipes, meal plans, shopping lists.",
		tone: "secondary",
	},
];

function FeatureCard({ feature, large = false }: { feature: Feature; large?: boolean }) {
	const toneColor = {
		primary: "var(--eth-primary)",
		secondary: "var(--eth-secondary)",
		tertiary: "var(--eth-tertiary)",
		brown: "#D8B889",
	}[feature.tone];

	return (
		<article
			className={`eth-card relative group ${large ? "p-8 sm:p-10" : "p-6 sm:p-7"}`}
			style={{ background: "var(--eth-surface-container)" }}
		>
			{/* Ambient color-tinted glow on hover. */}
			<div
				aria-hidden="true"
				className="pointer-events-none absolute -inset-4 rounded-[32px] opacity-0 transition-opacity duration-300 group-hover:opacity-100"
				style={{
					background: `radial-gradient(ellipse at center, ${toneColor}22, transparent 70%)`,
					filter: "blur(30px)",
				}}
			/>
			<div
				className="relative mb-5 inline-flex h-12 w-12 items-center justify-center rounded-2xl"
				style={{
					background: `color-mix(in oklab, ${toneColor} 18%, transparent)`,
					color: toneColor,
				}}
			>
				<i className={`${feature.icon} text-xl`} aria-hidden="true" />
			</div>
			<h3 className={`relative mb-2 ${large ? "eth-headline-lg" : "eth-headline-md"}`}>
				{feature.title}
			</h3>
			<p className="relative eth-body-md eth-text-muted">{feature.description}</p>
		</article>
	);
}

function AiCoachSpotlight() {
	return (
		<article
			className="eth-card relative overflow-hidden p-8 sm:p-10 md:col-span-2"
			style={{ background: "var(--eth-surface-container)" }}
		>
			<div
				aria-hidden="true"
				className="pointer-events-none absolute -right-20 -top-20 h-72 w-72 rounded-full"
				style={{
					background: "radial-gradient(closest-side, rgba(110, 235, 224, 0.25), transparent 70%)",
					filter: "blur(50px)",
				}}
			/>
			<div className="relative grid grid-cols-1 gap-8 md:grid-cols-[1fr_1.1fr] md:items-center">
				<div>
					<div
						className="mb-5 inline-flex h-12 w-12 items-center justify-center rounded-2xl"
						style={{ background: "rgba(110, 235, 224, 0.15)", color: "var(--eth-primary)" }}
					>
						<i className="ri-sparkling-2-line text-xl" aria-hidden="true" />
					</div>
					<h3 className="eth-headline-lg mb-3">AI coach that reads your week.</h3>
					<p className="eth-body-lg eth-text-muted">
						Ask anything in plain English. It knows your last week of meals, your goal, and your training — and answers like a trainer, not a search engine.
					</p>
				</div>

				{/* Simulated chat transcript inside the card */}
				<div
					className="eth-card-high space-y-3 p-5"
					style={{ background: "var(--eth-surface-high)", borderRadius: "24px" }}
				>
					<div className="flex items-start gap-2">
						<div
							className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-xs font-semibold"
							style={{ background: "var(--eth-surface-highest)", color: "var(--eth-on-surface-variant)" }}
						>
							E
						</div>
						<div
							className="eth-body-md rounded-2xl px-3.5 py-2.5"
							style={{ background: "var(--eth-surface-highest)" }}
						>
							Why did my weight stall this week?
						</div>
					</div>
					<div className="flex items-start gap-2">
						<div
							className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-xs font-semibold"
							style={{
								background: "linear-gradient(135deg, var(--eth-primary), var(--eth-primary-container))",
								color: "#0A0F1C",
							}}
						>
							M
						</div>
						<div
							className="eth-body-md rounded-2xl px-3.5 py-2.5"
							style={{
								background: "rgba(110, 235, 224, 0.10)",
								color: "var(--eth-on-surface)",
							}}
						>
							Weekend carbs averaged 320g vs. 210g on weekdays — fluid shift, not fat. Your 7-day avg is still <span className="eth-text-primary font-semibold">-0.3 kg</span>. Stay the course.
						</div>
					</div>
					{/* Typing indicator */}
					<div className="flex items-center gap-2 pl-9">
						<span className="inline-flex gap-1">
							<span className="h-1.5 w-1.5 animate-pulse rounded-full" style={{ background: "var(--eth-primary)" }} />
							<span className="h-1.5 w-1.5 animate-pulse rounded-full" style={{ background: "var(--eth-primary)", animationDelay: "120ms" }} />
							<span className="h-1.5 w-1.5 animate-pulse rounded-full" style={{ background: "var(--eth-primary)", animationDelay: "240ms" }} />
						</span>
						<span className="eth-label eth-text-dim">typing</span>
					</div>
				</div>
			</div>
		</article>
	);
}

export function FeatureSection() {
	return (
		<section aria-labelledby="feature-heading" className="py-16 sm:py-24">
			<div className="mb-12 max-w-3xl sm:mb-16">
				<span className="eth-label eth-text-primary">Six instruments</span>
				<h2 id="feature-heading" className="eth-display-md mt-3">
					One workspace. Built like an instrument, not a spreadsheet.
				</h2>
				<p className="eth-body-lg eth-text-muted mt-4 max-w-2xl">
					Stop stitching together three tools. Meals, training, and coaching share one canvas so progress compounds.
				</p>
			</div>

			{/* AI coach spotlight — spans the top row, 2 columns wide. */}
			<div className="grid grid-cols-1 gap-5 md:grid-cols-2">
				<AiCoachSpotlight />
			</div>

			{/* Asymmetric 3-column grid — middle column offset down by 40px. */}
			<div className="mt-5 grid grid-cols-1 gap-5 md:grid-cols-3">
				<div className="space-y-5">
					{COL_LEFT.map((f) => (
						<FeatureCard key={f.title} feature={f} />
					))}
				</div>
				<div className="space-y-5 md:translate-y-10">
					{COL_MIDDLE.map((f) => (
						<FeatureCard key={f.title} feature={f} />
					))}
				</div>
				<div className="space-y-5">
					{COL_RIGHT.map((f) => (
						<FeatureCard key={f.title} feature={f} />
					))}
				</div>
			</div>
		</section>
	);
}
