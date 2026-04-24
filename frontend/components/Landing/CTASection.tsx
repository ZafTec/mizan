import Link from "next/link";

export function CTASection() {
	return (
		<section
			data-testid="cta-section"
			aria-labelledby="final-cta-heading"
			className="relative overflow-hidden rounded-[28px] py-24 sm:py-32"
			style={{ background: "var(--eth-surface-low)" }}
		>
			<div
				aria-hidden="true"
				className="pointer-events-none absolute inset-0"
				style={{
					background:
						"radial-gradient(ellipse at center, rgba(110, 235, 224, 0.16), transparent 65%)",
					filter: "blur(80px)",
				}}
			/>
			<div className="relative z-10 mx-auto max-w-3xl px-6 text-center">
				<h2 id="final-cta-heading" className="eth-display-lg">
					Start free.
					<br />
					Upgrade the day you need <span className="eth-text-primary">more</span>.
				</h2>
				<p className="eth-body-lg eth-text-muted mt-6 max-w-xl mx-auto">
					Pro is $0.99 / month or $9 / year. Lifetime is a one-time $29. 14-day refund on every paid plan.
				</p>
				<div className="mt-10 flex flex-col justify-center gap-3 sm:flex-row">
					<Link href="/register" className="eth-btn-primary">
						Create a free account
						<i className="ri-arrow-right-line text-lg" aria-hidden="true" />
					</Link>
					<Link href="#pricing" className="eth-btn-glass">
						Compare plans
					</Link>
				</div>
				<p className="eth-label eth-text-dim mt-10 tracking-normal normal-case text-sm">
					No credit card · 14-day refund · cancel anytime
				</p>
			</div>
		</section>
	);
}
