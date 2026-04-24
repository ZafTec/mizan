import Link from "next/link";

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
		price: "$0.99",
		cadence: "per month",
		altPrice: "or $9 / year",
		description: "Everything in Free, plus the tools that make progress visible.",
		features: [
			"Unlimited meal plans & shopping lists",
			"Household invitations (up to 6)",
			"AI coach + food-image analysis",
			"Trainer–client chat and goals",
			"Advanced analytics + trends",
		],
		cta: "Go Pro",
		ctaHref: "/register?plan=pro",
		highlight: true,
	},
	{
		id: "lifetime" as const,
		name: "Lifetime",
		price: "$29",
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
		<section aria-labelledby="pricing-heading" className="py-16 sm:py-24" id="pricing">
			<div className="mb-12 max-w-3xl sm:mb-16">
				<span className="eth-label eth-text-primary">Three tiers, no fine print</span>
				<h2 id="pricing-heading" className="eth-display-md mt-3">
					Less than a coffee. Or once, and never again.
				</h2>
				<p className="eth-body-lg eth-text-muted mt-4 max-w-2xl">
					14-day refund on every paid plan. Cancel from your account in two clicks.
				</p>
			</div>

			<div className="relative grid grid-cols-1 items-stretch gap-6 md:grid-cols-3 md:gap-5">
				{/* Ambient glow behind the Pro tier. */}
				<div
					aria-hidden="true"
					className="pointer-events-none absolute left-1/2 top-1/2 -z-10 h-[420px] w-[420px] -translate-x-1/2 -translate-y-1/2 rounded-full hidden md:block"
					style={{
						background: "radial-gradient(closest-side, rgba(110, 235, 224, 0.18), transparent 70%)",
						filter: "blur(60px)",
					}}
				/>

				{TIERS.map((tier) => {
					const highlight = tier.highlight;
					return (
						<article
							key={tier.id}
							className="relative flex flex-col overflow-hidden p-7 sm:p-8"
							style={{
								background: highlight ? "var(--eth-surface-high)" : "var(--eth-surface-low)",
								borderRadius: "28px",
								transform: highlight ? "translateY(-24px)" : "translateY(0)",
							}}
						>
							{/* Gradient top bar for the highlighted tier. */}
							{highlight && (
								<div
									aria-hidden="true"
									className="absolute inset-x-0 top-0 h-1"
									style={{
										background: "linear-gradient(90deg, var(--eth-primary), var(--eth-primary-container))",
									}}
								/>
							)}
							{highlight && (
								<span
									className="absolute -top-3 right-6 inline-flex items-center gap-1 rounded-full px-3 py-1 text-[10px] font-semibold uppercase tracking-[0.18em]"
									style={{
										background: "linear-gradient(135deg, var(--eth-primary), var(--eth-primary-container))",
										color: "#0A0F1C",
									}}
								>
									<i className="ri-star-fill" aria-hidden="true" />
									Most popular
								</span>
							)}
							<header>
								<h3 className="eth-headline-md">{tier.name}</h3>
								<div className="mt-3 flex items-baseline gap-2">
									<span className="eth-display-md">{tier.price}</span>
									<span className="eth-label eth-text-dim">{tier.cadence}</span>
								</div>
								{tier.altPrice && (
									<p className="eth-label eth-text-primary mt-1 tracking-normal normal-case text-sm">{tier.altPrice}</p>
								)}
								<p className="eth-body-md eth-text-muted mt-3">{tier.description}</p>
							</header>
							<ul className="mt-6 space-y-3 flex-1">
								{tier.features.map((feature) => (
									<li key={feature} className="flex items-start gap-2">
										<span
											className="mt-0.5 inline-flex h-5 w-5 shrink-0 items-center justify-center rounded-full"
											style={{ background: "rgba(110, 235, 224, 0.16)", color: "var(--eth-primary)" }}
										>
											<i className="ri-check-line text-sm" aria-hidden="true" />
										</span>
										<span className="eth-body-md">{feature}</span>
									</li>
								))}
							</ul>
							<div className="mt-7 pt-6 eth-divider" aria-hidden="true" />
							<Link
								href={tier.ctaHref}
								className={highlight ? "eth-btn-primary mt-6 w-full" : "eth-btn-glass mt-6 w-full"}
							>
								{tier.cta}
								<i className="ri-arrow-right-line text-lg" aria-hidden="true" />
							</Link>
						</article>
					);
				})}
			</div>
			<p className="eth-label eth-text-dim mt-12 text-center tracking-normal normal-case text-sm">
				Billing handled by Paddle (our Merchant of Record). Prices in USD.
			</p>
		</section>
	);
}
