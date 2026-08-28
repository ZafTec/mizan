import Link from "next/link";

const TIERS = [
	{
		id: "free" as const,
		name: "Free",
		price: "$0",
		cadence: "forever",
		features: [
			"Unlimited meals, workouts, measurements",
			"Recipes, meal plans, shopping lists",
			"Streaks and achievements",
			"A small daily assistant allowance",
		],
		cta: "Start logging",
		ctaHref: "/register",
	},
	{
		id: "pro" as const,
		name: "Pro",
		price: "$2.99",
		cadence: "/mo",
		highlight: true,
		features: [
			"Everything in Free",
			"Photo analysis — snap a plate, confirm the estimate",
			"A working daily assistant allowance",
			"Telegram bot",
			"Coach relationships",
		],
		cta: "Go Pro",
		ctaHref: "/register?plan=pro",
	},
	{
		id: "self-hosted" as const,
		name: "Self-hosted",
		price: "$0",
		cadence: "",
		features: [
			"The whole thing, your machine",
			"Docker Compose, PostgreSQL, Redis",
			"Bring your own AI provider key",
			"Your database, your backups",
		],
		cta: "Read the setup guide",
		ctaHref: "https://github.com/ZafTec/mizan#self-hosting",
	},
];

export function PricingSection() {
	return (
		<section aria-labelledby="pricing-heading" id="pricing" className="border-t border-charcoal-blue-200 py-16 sm:py-20">
			<div className="mx-auto mb-10 max-w-2xl text-center">
				<p className="eyebrow">Pricing</p>
				<h2 id="pricing-heading" className="mt-3 text-3xl font-medium tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
					Logging is free. Forever.
				</h2>
				<p className="mt-3 text-[15px] leading-relaxed text-charcoal-blue-600 dark:text-charcoal-blue-400">
					A tracker that paywalls the thing it exists to do is a tracker you stop opening. Pro pays for what actually costs money: the AI, and photo analysis.
				</p>
			</div>

			<div className="mx-auto grid max-w-4xl grid-cols-1 border border-charcoal-blue-200 bg-charcoal-blue-200 gap-px sm:grid-cols-3 dark:border-charcoal-blue-700 dark:bg-charcoal-blue-700">
				{TIERS.map((tier) => (
					<article
						key={tier.id}
						className={`flex flex-col gap-4 bg-white p-7 dark:bg-charcoal-blue-950 ${
							tier.highlight ? "border-t-[3px] border-t-charcoal-blue-900 dark:border-t-charcoal-blue-50" : ""
						}`}
					>
						<div>
							<div className="flex items-center gap-2.5">
								<h3 className="text-lg font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">
									{tier.name}
								</h3>
								{tier.highlight && <span className="eyebrow text-verdigris-700 dark:text-verdigris-400">Most people</span>}
							</div>
							<div className="num mt-1.5 text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50">
								{tier.price}
								{tier.cadence && <span className="text-sm font-normal text-charcoal-blue-500 dark:text-charcoal-blue-500">{tier.cadence}</span>}
							</div>
						</div>
						<div className="flex flex-col gap-1.5 text-[13px] leading-relaxed text-charcoal-blue-600 dark:text-charcoal-blue-400">
							{tier.features.map((feature) => (
								<div key={feature}>{feature}</div>
							))}
						</div>
						<Link
							href={tier.ctaHref}
							className={`mt-auto justify-center ${tier.highlight ? "btn-primary" : "btn-secondary"}`}
						>
							{tier.cta}
						</Link>
					</article>
				))}
			</div>
			<p className="mt-6 text-center text-xs text-charcoal-blue-500 dark:text-charcoal-blue-500">
				Billing handled by Paddle · Prices in USD
			</p>
		</section>
	);
}
