import Link from "next/link";
import { AnimatedIcon } from "@/components/ui/animated-icon";

// Pricing tiers. For now all CTAs point at /register; once Paddle is live
// they'll route to a Paddle Checkout session keyed by the price IDs.
const tiers: Array<{
	id: "free" | "pro" | "lifetime";
	name: string;
	price: string;
	cadence: string;
	altPrice?: string;
	description: string;
	features: string[];
	cta: string;
	ctaHref: string;
	highlight?: boolean;
	badge?: string;
}> = [
	{
		id: "free",
		name: "Free",
		price: "$0",
		cadence: "forever",
		description: "Get the basics locked in. No credit card, no trial clock.",
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
		id: "pro",
		name: "Pro",
		price: "$0.99",
		cadence: "per month",
		altPrice: "or $9 / year",
		description: "Everything in Free, plus the tools that make progress visible.",
		features: [
			"Unlimited meal plans & shopping lists",
			"Household invitations (up to 6 members)",
			"AI coach + food-image analysis",
			"Trainer–client chat and goals",
			"Advanced analytics & body-composition trends",
		],
		cta: "Go Pro",
		ctaHref: "/register?plan=pro",
		highlight: true,
		badge: "Most popular",
	},
	{
		id: "lifetime",
		name: "Lifetime",
		price: "$29",
		cadence: "one-time",
		description: "Pay once. Pro forever, plus every feature we ship later.",
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
		<section aria-labelledby="pricing-heading" className="py-12 sm:py-16" id="pricing">
			<div className="mb-10 text-center sm:mb-12">
				<div className="eyebrow mb-4">
					<AnimatedIcon name="shieldCheck" size={14} aria-hidden="true" />
					Honest, flat pricing
				</div>
				<h2
					id="pricing-heading"
					className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl"
				>
					Pay less than a coffee. Or once, and never again.
				</h2>
				<p className="mx-auto mt-3 max-w-2xl text-charcoal-blue-500 dark:text-charcoal-blue-400">
					14-day refund on every paid plan. Cancel anytime from your account.
				</p>
			</div>
			<div className="grid grid-cols-1 gap-6 md:grid-cols-3">
				{tiers.map((tier, index) => (
					<article
						key={tier.id}
						className={`stagger-item relative flex flex-col rounded-[28px] border p-6 transition-shadow duration-300 sm:p-7 ${
							tier.highlight
								? "border-brand-500/40 bg-gradient-to-b from-brand-500/10 via-white to-white shadow-xl shadow-brand-500/10 dark:from-brand-500/10 dark:via-charcoal-blue-900/60 dark:to-charcoal-blue-900/60"
								: "border-charcoal-blue-200 bg-white dark:border-white/10 dark:bg-charcoal-blue-900/60"
						}`}
						style={{ animationDelay: `${index * 70}ms` }}
					>
						{tier.badge && (
							<span className="absolute -top-3 left-1/2 inline-flex -translate-x-1/2 items-center gap-1.5 rounded-full border border-brand-500/30 bg-brand-500 px-3 py-1 text-[11px] font-semibold uppercase tracking-[0.18em] text-white shadow-lg shadow-brand-500/25">
								<AnimatedIcon name="sparkles" size={12} aria-hidden="true" />
								{tier.badge}
							</span>
						)}
						<header>
							<h3 className="text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
								{tier.name}
							</h3>
							<div className="mt-3 flex items-baseline gap-2">
								<span className="text-4xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-5xl">
									{tier.price}
								</span>
								<span className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
									{tier.cadence}
								</span>
							</div>
							{tier.altPrice && (
								<p className="mt-1 text-sm text-brand-700 dark:text-brand-300">{tier.altPrice}</p>
							)}
							<p className="mt-3 text-sm leading-relaxed text-charcoal-blue-600 dark:text-charcoal-blue-400">
								{tier.description}
							</p>
						</header>
						<ul className="mt-6 space-y-3 text-sm text-charcoal-blue-700 dark:text-charcoal-blue-300">
							{tier.features.map((feature) => (
								<li key={feature} className="flex items-start gap-2">
									<span className="mt-0.5 inline-flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-brand-500/15 text-brand-700 dark:text-brand-300">
										<AnimatedIcon name="circleCheck" size={14} aria-hidden="true" />
									</span>
									<span>{feature}</span>
								</li>
							))}
						</ul>
						<div className="mt-7 pt-6 border-t border-charcoal-blue-100 dark:border-white/10">
							<Link
								href={tier.ctaHref}
								className={
									tier.highlight
										? "btn-primary btn-lg w-full justify-center"
										: "btn-secondary btn-lg w-full justify-center"
								}
							>
								{tier.cta}
								<AnimatedIcon name="arrowRight" size={16} aria-hidden="true" />
							</Link>
						</div>
					</article>
				))}
			</div>
			<p className="mt-8 text-center text-xs text-charcoal-blue-500 dark:text-charcoal-blue-500">
				Billing and invoicing handled by Paddle, our Merchant of Record. Prices shown in USD.
			</p>
		</section>
	);
}
