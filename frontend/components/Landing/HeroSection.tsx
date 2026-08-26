import Link from "next/link";
import { ProducerBadge } from "./ProducerBadge";
import { AnimatedIcon } from "@/components/ui/animated-icon";

/**
 * Text and one flat stat readout - no dark band, no blur glow, no glass
 * cards floating over it. The page background carries straight through, and
 * the two-column layout collapses to one on mobile rather than hiding the
 * proof entirely (the old version dropped the whole right side below `lg`).
 */
export function HeroSection() {
	const kcal = 1842;
	const kcalTarget = 2200;
	const pct = Math.round((kcal / kcalTarget) * 100);

	return (
		<section
			data-testid="hero-section"
			aria-labelledby="hero-heading"
			className="grid grid-cols-1 items-center gap-10 py-8 sm:py-12 lg:grid-cols-[1.15fr_1fr] lg:gap-14"
		>
			<div className="max-w-2xl">
				<div className="mb-5">
					<ProducerBadge />
				</div>
				<h1
					id="hero-heading"
					className="text-4xl font-medium tracking-tight text-charcoal-blue-900 sm:text-5xl lg:text-6xl dark:text-charcoal-blue-50"
				>
					Your macros. <span className="text-brand-600 dark:text-brand-400">Surgical.</span>
				</h1>
				<p className="mt-5 max-w-xl text-lg leading-relaxed text-charcoal-blue-600 dark:text-charcoal-blue-400">
					The nutrition app built like a HUD, not a spreadsheet. Track, plan, and ship goals in a workspace that actually feels alive.
				</p>
				<div className="mt-8 flex flex-col gap-3 sm:flex-row sm:items-center">
					<Link href="/register" className="btn-primary btn-lg">
						Start tracking for free
						<AnimatedIcon name="arrowRight" size={18} aria-hidden="true" />
					</Link>
					<Link href="#pricing" className="btn-ghost btn-lg">
						See what&apos;s inside
					</Link>
				</div>
				<div className="mt-10 flex flex-wrap items-center gap-x-6 gap-y-2 text-xs font-medium uppercase tracking-[0.14em] text-charcoal-blue-500 dark:text-charcoal-blue-400">
					<span>No credit card</span>
					<span className="h-1 w-1 rounded-full bg-current opacity-50" aria-hidden="true" />
					<span>14-day refund</span>
					<span className="h-1 w-1 rounded-full bg-current opacity-50" aria-hidden="true" />
					<span>Cancel anytime</span>
				</div>
			</div>

			{/* One flat card, not three floating ones. Same DataTable-adjacent
			    surface as the rest of the app, so the proof looks like the
			    product rather than a marketing illustration of it. */}
			<div className="card p-5 sm:p-6">
				<div className="flex items-baseline justify-between">
					<span className="text-xs font-medium uppercase tracking-[0.14em] text-charcoal-blue-500 dark:text-charcoal-blue-400">
						Today
					</span>
					<span className="text-xs font-medium text-charcoal-blue-500 dark:text-charcoal-blue-400">{pct}%</span>
				</div>
				<div className="mt-2 flex items-baseline gap-2">
					<span className="text-4xl font-semibold tabular-nums tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50">
						{kcal.toLocaleString()}
					</span>
					<span className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">of {kcalTarget.toLocaleString()} kcal</span>
				</div>
				<div className="mt-3 h-1.5 overflow-hidden rounded-full bg-charcoal-blue-100 dark:bg-charcoal-blue-800">
					<div className="h-full rounded-full bg-brand-600 dark:bg-brand-400" style={{ width: `${pct}%` }} />
				</div>

				<div className="mt-5 grid grid-cols-3 gap-3 border-t border-charcoal-blue-100 pt-4 dark:border-white/10">
					<MacroReadout label="Protein" value="148g" tone="peach" />
					<MacroReadout label="Carbs" value="210g" tone="sun" />
					<MacroReadout label="Fat" value="62g" tone="sand" />
				</div>

				<div className="mt-5 flex items-center gap-2 border-t border-charcoal-blue-100 pt-4 text-xs text-charcoal-blue-500 dark:border-white/10 dark:text-charcoal-blue-400">
					<AnimatedIcon name="messageCircle" size={14} aria-hidden="true" />
					Logged from Telegram, 4 minutes ago
				</div>
			</div>
		</section>
	);
}

function MacroReadout({ label, value, tone }: { label: string; value: string; tone: "peach" | "sun" | "sand" }) {
	const toneClass =
		tone === "peach"
			? "text-burnt-peach-600 dark:text-burnt-peach-400"
			: tone === "sun"
				? "text-tuscan-sun-600 dark:text-tuscan-sun-400"
				: "text-sandy-brown-600 dark:text-sandy-brown-400";

	return (
		<div>
			<div className="text-[11px] font-medium uppercase tracking-[0.1em] text-charcoal-blue-500 dark:text-charcoal-blue-400">
				{label}
			</div>
			<div className={`mt-0.5 text-sm font-semibold tabular-nums ${toneClass}`}>{value}</div>
		</div>
	);
}
