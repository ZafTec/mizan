import Link from "next/link";
import { ProducerBadge } from "./ProducerBadge";
import { AnimatedIcon } from "@/components/ui/animated-icon";

// Asymmetric hero using the project theme tokens. Left: editorial headline +
// CTAs. Right: HUD progress ring bleeding off-canvas with three glass macro
// cards at offset angles. Colors come from the shared palette (brand = verdigris,
// burnt-peach, tuscan-sun, sandy-brown) so light and dark modes both work.
export function HeroSection() {
	const kcalPct = 1842 / 2200;
	const ringSize = 520;
	const stroke = 22;
	const radius = (ringSize - stroke) / 2;
	const circumference = 2 * Math.PI * radius;
	const dashOffset = circumference * (1 - kcalPct);

	return (
		<section
			data-testid="hero-section"
			aria-labelledby="hero-heading"
			className="relative overflow-hidden rounded-[32px] shadow-panel"
			style={{ background: "var(--color-charcoal-blue-950)" }}
		>
			<div
				aria-hidden="true"
				className="pointer-events-none absolute -right-40 top-1/2 h-[640px] w-[640px] -translate-y-1/2 rounded-full"
				style={{
					background:
						"radial-gradient(closest-side, color-mix(in oklab, var(--color-brand-500) 22%, transparent), transparent 70%)",
					filter: "blur(60px)",
				}}
			/>
			<div
				aria-hidden="true"
				className="pointer-events-none absolute -left-24 -top-20 h-[420px] w-[420px] rounded-full"
				style={{
					background:
						"radial-gradient(closest-side, color-mix(in oklab, var(--color-burnt-peach-400) 12%, transparent), transparent 70%)",
					filter: "blur(60px)",
				}}
			/>

			<div className="relative z-10 grid grid-cols-1 gap-10 px-6 py-14 sm:px-10 sm:py-16 lg:grid-cols-[1.25fr_1fr] lg:gap-6 lg:px-14 lg:py-20">
				<div className="max-w-2xl text-white">
					<div className="mb-6 flex items-center gap-4">
						<ProducerBadge />
						<span className="text-xs font-medium uppercase tracking-[0.14em] text-white/50">Est. 2024</span>
					</div>
					<h1
						id="hero-heading"
						className="text-4xl font-semibold tracking-tight text-white sm:text-5xl lg:text-6xl"
					>
						Your macros.{" "}
						<span className="text-brand-300">Surgical.</span>
					</h1>
					<p className="mt-5 max-w-xl text-lg leading-relaxed text-white/80 sm:text-xl">
						The nutrition app built like a HUD, not a spreadsheet. Track, plan, and ship goals in a workspace that actually feels alive.
					</p>
					<div className="mt-8 flex flex-col gap-3 sm:flex-row sm:items-center">
						<Link
							href="/register"
							className="btn-secondary btn-lg border-white/20 bg-white/95 text-brand-700 hover:bg-white dark:bg-white/95 dark:text-brand-700 dark:hover:bg-white"
						>
							Start tracking for free
							<AnimatedIcon name="arrowRight" size={18} aria-hidden="true" />
						</Link>
						<Link
							href="#pricing"
							className="btn-lg inline-flex items-center justify-center gap-2 rounded-2xl border border-white/20 bg-white/10 px-6 py-3.5 font-semibold text-white backdrop-blur-sm transition-colors duration-200 ease-out hover:bg-white/20 active:scale-[0.97]"
						>
							See what&apos;s inside
						</Link>
					</div>
					<div className="mt-10 flex flex-wrap items-center gap-x-6 gap-y-2 text-xs font-medium uppercase tracking-[0.14em] text-white/50">
						<span>No credit card</span>
						<span className="h-1 w-1 rounded-full bg-current opacity-50" aria-hidden="true" />
						<span>14-day refund</span>
						<span className="h-1 w-1 rounded-full bg-current opacity-50" aria-hidden="true" />
						<span>Cancel anytime</span>
					</div>
				</div>

				{/* Right: HUD ring + stacked glass macro cards */}
				<div className="relative hidden min-h-[480px] lg:block">
					<div className="absolute right-[-80px] top-1/2 -translate-y-1/2">
						<svg width={ringSize} height={ringSize} viewBox={`0 0 ${ringSize} ${ringSize}`} aria-hidden="true">
							<defs>
								<linearGradient id="hero-ring-grad" x1="0" y1="0" x2="1" y2="1">
									<stop offset="0%" stopColor="var(--color-brand-300)" />
									<stop offset="100%" stopColor="var(--color-brand-600)" />
								</linearGradient>
							</defs>
							<circle
								cx={ringSize / 2}
								cy={ringSize / 2}
								r={radius}
								fill="none"
								stroke="rgba(255,255,255,0.08)"
								strokeWidth={stroke}
							/>
							<circle
								cx={ringSize / 2}
								cy={ringSize / 2}
								r={radius}
								fill="none"
								stroke="url(#hero-ring-grad)"
								strokeWidth={stroke}
								strokeDasharray={circumference}
								strokeDashoffset={dashOffset}
								strokeLinecap="round"
								transform={`rotate(-90 ${ringSize / 2} ${ringSize / 2})`}
							/>
						</svg>
						<div className="pointer-events-none absolute inset-0 flex flex-col items-center justify-center text-white">
							<span className="text-xs font-medium uppercase tracking-[0.14em] text-white/60">Today</span>
							<span
								className="mt-1 font-bold leading-none tracking-[-0.04em]"
								style={{ fontSize: "clamp(3rem, 5.5vw, 4.5rem)" }}
							>
								1,842
							</span>
							<span className="mt-2 text-xs font-medium uppercase tracking-[0.14em] text-white/60">of 2,200 kcal</span>
						</div>
					</div>

					<GlassMacroCard top="8%" left="-8%" tone="protein" label="Protein" value="148" sub="76% of daily" />
					<GlassMacroCard top="44%" left="-14%" tone="carbs" label="Carbs" value="210" sub="Under goal" />
					<GlassMacroCard top="80%" left="4%" tone="fat" label="Fat" value="62" sub="Right in range" />
				</div>

				{/* Mobile HUD */}
				<div className="lg:hidden">
					<div className="relative mx-auto flex max-w-sm items-center justify-between gap-3 rounded-[24px] border border-white/15 bg-white/10 p-4 text-white backdrop-blur-md">
						<div>
							<span className="text-xs font-medium uppercase tracking-[0.14em] text-white/60">Today</span>
							<div className="mt-1 text-3xl font-bold tracking-tight">1,842</div>
							<div className="text-xs text-white/60">of 2,200 kcal</div>
						</div>
						<div className="relative h-20 w-20">
							<svg viewBox="0 0 100 100" className="h-full w-full">
								<circle cx="50" cy="50" r="42" fill="none" stroke="rgba(255,255,255,0.15)" strokeWidth="8" />
								<circle
									cx="50"
									cy="50"
									r="42"
									fill="none"
									stroke="var(--color-brand-300)"
									strokeWidth="8"
									strokeDasharray={2 * Math.PI * 42}
									strokeDashoffset={2 * Math.PI * 42 * (1 - kcalPct)}
									strokeLinecap="round"
									transform="rotate(-90 50 50)"
								/>
							</svg>
						</div>
					</div>
				</div>
			</div>
		</section>
	);
}

function GlassMacroCard({
	top,
	left,
	tone,
	label,
	value,
	sub,
}: {
	top: string;
	left: string;
	tone: "protein" | "carbs" | "fat";
	label: string;
	value: string;
	sub: string;
}) {
	const chipClass =
		tone === "protein"
			? "bg-burnt-peach-500/15 text-burnt-peach-300"
			: tone === "carbs"
				? "bg-tuscan-sun-500/15 text-tuscan-sun-300"
				: "bg-sandy-brown-500/15 text-sandy-brown-300";

	return (
		<div
			className="absolute w-[190px] rounded-[24px] border border-white/15 bg-white/10 p-4 text-white backdrop-blur-md"
			style={{ top, left }}
		>
			<span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${chipClass}`}>
				{label}
			</span>
			<div className="mt-2 text-xl font-bold">
				{value}
				<span className="ml-1 text-sm font-normal text-white/60">g</span>
			</div>
			<div className="mt-0.5 text-xs text-white/60">{sub}</div>
		</div>
	);
}
