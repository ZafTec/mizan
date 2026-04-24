import Link from "next/link";
import { AnimatedIcon } from "@/components/ui/animated-icon";

const TIERS = [
	{
		id: "free" as const,
		name: "Free",
		price: "$0",
		cadence: "forever",
		description: "Get the basics locked in. No credit card.",
		features: [
			"Unlimited meal logging",
			"Recipe browser",
			"One meal plan, one shopping list",
			"Achievements + streaks",
		],
		cta: "Start free",
		ctaHref: "/register",
	},
	{
		id: "pro" as const,
		name: "Pro",
		price: "$1.99",
		cadence: "per month",
		altPrice: "or $15 / year",
		description: "Everything in Free, plus the tools that make progress visible.",
		features: [
			"Unlimited meal plans & shopping lists",
			"Household invitations (up to 6)",
			"AI coach + food-image analysis",
			"Trainer + client chat and goals",
			"Advanced analytics + trends",
		],
		cta: "Go Pro",
		ctaHref: "/register?plan=pro",
		highlight: true,
	},
	{
		id: "lifetime" as const,
		name: "Lifetime",
		price: "$48",
		cadence: "one-time",
		description: "Pay once. Pro forever, plus every feature we ship next.",
		features: [
			"Everything in Pro",
			"All future features included",
			"Priority support",
			"No renewal, no subscription decay",
		],
		cta: "Buy Lifetime",
		ctaHref: "/register?plan=lifetime",
	},
];

export function PricingSection() {
	return (
		<section aria-labelledby="pricing-heading" id="pricing" className="py-12 sm:py-16">
			<div className="mb-8 max-w-3xl sm:mb-10">
				<div className="eyebrow mb-4">
					<AnimatedIcon name="shieldCheck" size={14} aria-hidden="true" />
					Three tiers, no fine print
				</div>
				<h2 id="pricing-heading" className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
					Less than a coffee. Or once, and never again.
				</h2>
				<p className="mt-3 text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
					14-day refund on every paid plan. Cancel from your account in two clicks.
				</p>
			</div>

			<div className="relative grid grid-cols-1 items-stretch gap-4 md:grid-cols-3">
				<div
					aria-hidden="true"
					className="pointer-events-none absolute left-1/2 top-1/2 -z-10 hidden h-[320px] w-[320px] -translate-x-1/2 -translate-y-1/2 rounded-full md:block"
					style={{
						background: "radial-gradient(closest-side, color-mix(in oklab, var(--color-brand-500) 18%, transparent), transparent 70%)",
						filter: "blur(55px)",
					}}
				/>
				{TIERS.map((tier) => {
					const highlight = tier.highlight;
					return (
						<article
							key={tier.id}
							className={`relative flex flex-col overflow-hidden rounded-[28px] p-6 ${
								highlight
									? "border border-brand-500/30 bg-white shadow-xl shadow-brand-500/10 dark:border-brand-500/30 dark:bg-charcoal-blue-900"
									: "border border-charcoal-blue-100 bg-white dark:border-white/10 dark:bg-charcoal-blue-900/60"
							}`}
						>
							{highlight && (
								<div
									aria-hidden="true"
									className="absolute inset-x-0 top-0 h-0.5 bg-gradient-to-r from-brand-500 to-brand-300"
								/>
							)}
							<header>
								<h3 className="text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">{tier.name}</h3>
								<div className="mt-2 flex items-baseline gap-2">
									<span className="text-4xl font-bold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-5xl">
										{tier.price}
									</span>
									<span className="text-xs font-medium uppercase tracking-[0.14em] text-charcoal-blue-500 dark:text-charcoal-blue-400">
										{tier.cadence}
									</span>
								</div>
								{tier.altPrice && (
									<p className="mt-1 text-xs font-medium text-brand-700 dark:text-brand-300">{tier.altPrice}</p>
								)}
								<p className="mt-3 text-sm text-charcoal-blue-600 dark:text-charcoal-blue-400">{tier.description}</p>
							</header>
							<ul className="mt-5 flex-1 space-y-2.5">
								{tier.features.map((feature) => (
									<li key={feature} className="flex items-start gap-2 text-sm text-charcoal-blue-700 dark:text-charcoal-blue-200">
										<span className="mt-1 inline-block h-1.5 w-1.5 shrink-0 rounded-full bg-brand-500" aria-hidden="true" />
										<span>{feature}</span>
									</li>
								))}
							</ul>
							<Link
								href={tier.ctaHref}
								className={`mt-6 w-full ${highlight ? "btn-primary" : "btn-secondary"}`}
							>
								{tier.cta}
							</Link>
						</article>
					);
				})}
			</div>
			<p className="mt-8 text-center text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
				Billing handled by Paddle · Prices in USD
			</p>
		</section>
	);
}
