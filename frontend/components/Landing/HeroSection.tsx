import Link from "next/link";
import { ProducerBadge } from "./ProducerBadge";

// Asymmetric hero. Left: editorial headline + CTAs. Right: HUD progress ring
// bleeding off-canvas with three stacked glass macro cards at offset angles.
// No drop shadows — ambient teal glows replace them.
export function HeroSection() {
	const kcalPct = 1842 / 2200; // 0.837
	const ringSize = 520;
	const stroke = 22;
	const radius = (ringSize - stroke) / 2;
	const circumference = 2 * Math.PI * radius;
	const dashOffset = circumference * (1 - kcalPct);

	return (
		<section
			data-testid="hero-section"
			aria-labelledby="hero-heading"
			className="relative overflow-hidden rounded-[28px]"
			style={{ background: "var(--eth-surface-low)" }}
		>
			{/* Ambient teal glow behind the ring — no shadow, pure radial blur. */}
			<div
				aria-hidden="true"
				className="pointer-events-none absolute -right-40 top-1/2 h-[640px] w-[640px] -translate-y-1/2 rounded-full"
				style={{
					background: "radial-gradient(closest-side, rgba(110, 235, 224, 0.22), transparent 70%)",
					filter: "blur(60px)",
				}}
			/>
			{/* Softer peach glow in the opposite corner for asymmetric depth. */}
			<div
				aria-hidden="true"
				className="pointer-events-none absolute -left-24 -top-20 h-[420px] w-[420px] rounded-full"
				style={{
					background: "radial-gradient(closest-side, rgba(255, 180, 162, 0.10), transparent 70%)",
					filter: "blur(60px)",
				}}
			/>

			<div className="relative z-10 grid grid-cols-1 gap-10 px-6 py-16 sm:px-10 sm:py-20 lg:grid-cols-[1.25fr_1fr] lg:gap-6 lg:px-14 lg:py-24">
				{/* Left: editorial stack */}
				<div className="max-w-2xl">
					<div className="eth-reveal mb-7 flex items-center gap-4" style={{ animationDelay: "0ms" }}>
						<ProducerBadge />
						<span className="eth-label eth-text-dim">Est. 2024</span>
					</div>
					<h1
						id="hero-heading"
						className="eth-display-lg eth-reveal"
						style={{ animationDelay: "60ms" }}
					>
						Your macros.{" "}
						<span className="eth-text-primary">Surgical.</span>
					</h1>
					<p
						className="eth-body-lg eth-text-muted eth-reveal mt-6 max-w-xl"
						style={{ animationDelay: "120ms" }}
					>
						The nutrition app built like a HUD, not a spreadsheet. Track, plan, and ship goals in a workspace that actually feels alive.
					</p>
					<div
						className="eth-reveal mt-10 flex flex-col gap-3 sm:flex-row sm:items-center"
						style={{ animationDelay: "180ms" }}
					>
						<Link href="/register" className="eth-btn-primary">
							Start tracking — free
							<i className="ri-arrow-right-line text-lg" aria-hidden="true" />
						</Link>
						<Link href="#pricing" className="eth-btn-glass">
							<i className="ri-play-fill text-lg" aria-hidden="true" />
							See what&apos;s inside
						</Link>
					</div>
					<div
						className="eth-reveal mt-12 flex flex-wrap items-center gap-x-8 gap-y-3 eth-label eth-text-dim"
						style={{ animationDelay: "260ms" }}
					>
						<span>No credit card</span>
						<span className="h-1 w-1 rounded-full bg-current opacity-40" aria-hidden="true" />
						<span>14-day refund</span>
						<span className="h-1 w-1 rounded-full bg-current opacity-40" aria-hidden="true" />
						<span>Cancel anytime</span>
					</div>
				</div>

				{/* Right: HUD ring + stacked glass macro cards bleeding right */}
				<div className="relative hidden min-h-[520px] lg:block">
					{/* SVG ring positioned to bleed off the right edge. */}
					<div
						className="absolute top-1/2 right-[-80px] -translate-y-1/2 eth-reveal"
						style={{ animationDelay: "200ms" }}
					>
						<svg width={ringSize} height={ringSize} viewBox={`0 0 ${ringSize} ${ringSize}`} aria-hidden="true">
							<defs>
								<linearGradient id="eth-ring-grad" x1="0" y1="0" x2="1" y2="1">
									<stop offset="0%" stopColor="#6EEBE0" />
									<stop offset="100%" stopColor="#4DCFC4" />
								</linearGradient>
							</defs>
							<circle
								cx={ringSize / 2}
								cy={ringSize / 2}
								r={radius}
								fill="none"
								stroke="rgba(181, 196, 217, 0.08)"
								strokeWidth={stroke}
							/>
							<circle
								cx={ringSize / 2}
								cy={ringSize / 2}
								r={radius}
								fill="none"
								stroke="url(#eth-ring-grad)"
								strokeWidth={stroke}
								strokeDasharray={circumference}
								strokeDashoffset={dashOffset}
								strokeLinecap="round"
								transform={`rotate(-90 ${ringSize / 2} ${ringSize / 2})`}
							/>
						</svg>
						{/* Hero number inside the ring, aligned asymmetrically with the left text. */}
						<div className="pointer-events-none absolute inset-0 flex flex-col items-center justify-center">
							<span className="eth-label eth-text-dim">Today</span>
							<span
								className="mt-1 font-bold tracking-[-0.04em] leading-none"
								style={{ fontSize: "clamp(3.2rem, 6vw, 5.2rem)" }}
							>
								1,842
							</span>
							<span className="eth-label eth-text-muted mt-2">of 2,200 kcal</span>
						</div>
					</div>

					{/* Three glass macro cards — stacked diagonally, asymmetric offsets. */}
					<div
						className="eth-card-glass eth-reveal absolute"
						style={{
							top: "8%",
							left: "-8%",
							width: "190px",
							animationDelay: "320ms",
							padding: "1rem 1.25rem",
						}}
					>
						<span className="eth-chip eth-chip-protein mb-2">Protein</span>
						<div className="eth-headline-md">148<span className="eth-text-dim text-base font-normal ml-1">g</span></div>
						<div className="eth-label eth-text-dim mt-1">76% of daily</div>
					</div>
					<div
						className="eth-card-glass eth-reveal absolute"
						style={{
							top: "44%",
							left: "-14%",
							width: "190px",
							animationDelay: "400ms",
							padding: "1rem 1.25rem",
						}}
					>
						<span className="eth-chip eth-chip-carbs mb-2">Carbs</span>
						<div className="eth-headline-md">210<span className="eth-text-dim text-base font-normal ml-1">g</span></div>
						<div className="eth-label eth-text-dim mt-1">Under goal</div>
					</div>
					<div
						className="eth-card-glass eth-reveal absolute"
						style={{
							bottom: "10%",
							left: "4%",
							width: "190px",
							animationDelay: "480ms",
							padding: "1rem 1.25rem",
						}}
					>
						<span className="eth-chip eth-chip-fat mb-2">Fat</span>
						<div className="eth-headline-md">62<span className="eth-text-dim text-base font-normal ml-1">g</span></div>
						<div className="eth-label eth-text-dim mt-1">Right in range</div>
					</div>
				</div>

				{/* Mobile/tablet: simplified HUD below the hero copy */}
				<div className="lg:hidden">
					<div className="eth-card-glass relative mx-auto max-w-sm">
						<div className="flex items-center justify-between">
							<div>
								<span className="eth-label eth-text-dim">Today</span>
								<div className="eth-display-md mt-1">1,842</div>
								<div className="eth-label eth-text-muted mt-1">of 2,200 kcal</div>
							</div>
							<div className="relative h-20 w-20">
								<svg viewBox="0 0 100 100" className="h-full w-full">
									<circle cx="50" cy="50" r="42" fill="none" stroke="rgba(181, 196, 217, 0.12)" strokeWidth="8" />
									<circle
										cx="50"
										cy="50"
										r="42"
										fill="none"
										stroke="#6EEBE0"
										strokeWidth="8"
										strokeDasharray={2 * Math.PI * 42}
										strokeDashoffset={2 * Math.PI * 42 * (1 - kcalPct)}
										strokeLinecap="round"
										transform="rotate(-90 50 50)"
									/>
								</svg>
							</div>
						</div>
						<div className="mt-4 flex flex-wrap gap-2">
							<span className="eth-chip eth-chip-protein">P 148g</span>
							<span className="eth-chip eth-chip-carbs">C 210g</span>
							<span className="eth-chip eth-chip-fat">F 62g</span>
						</div>
					</div>
				</div>
			</div>
		</section>
	);
}
