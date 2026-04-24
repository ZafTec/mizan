import Link from "next/link";
import { AnimatedIcon } from "@/components/ui/animated-icon";

export function CTASection() {
	return (
		<section
			data-testid="cta-section"
			aria-labelledby="final-cta-heading"
			className="surface-panel relative overflow-hidden rounded-[32px] bg-charcoal-blue-900 p-8 text-center sm:p-12 dark:bg-charcoal-blue-950"
		>
			<div className="absolute inset-0 bg-[url('/grid.svg')] opacity-10" aria-hidden="true" />
			<div className="absolute left-1/2 top-0 h-150 w-150 -translate-x-1/2 rounded-full bg-white/6 blur-3xl" aria-hidden="true" />
			<div className="absolute -bottom-16 right-0 h-48 w-48 rounded-full bg-brand-500/10 blur-3xl" aria-hidden="true" />

			<div className="relative z-10 mx-auto max-w-xl text-white">
				<div className="mb-5 inline-flex items-center gap-2 rounded-full border border-white/15 bg-white/10 px-3 py-1 text-xs font-semibold uppercase tracking-[0.18em] backdrop-blur-sm">
					<AnimatedIcon name="rocket" size={14} aria-hidden="true" />
					Ready when you are
				</div>
				<h2
					id="final-cta-heading"
					className="mb-4 text-3xl font-semibold tracking-tight sm:text-4xl"
				>
					Start free. Upgrade the day you need more.
				</h2>
				<p className="mb-8 text-white/85">
					No credit card to start. Pro is $0.99 / month or $9 / year. Lifetime is a $29 one-time payment. 14-day refund on every paid plan.
				</p>
				<div className="flex flex-col justify-center gap-4 sm:flex-row">
					<Link
						href="/register"
						className="btn-secondary btn-lg border-white/20 bg-white/95 text-brand-700 shadow-xl shadow-charcoal-blue-950/10 hover:bg-white dark:bg-charcoal-blue-900/60 dark:text-brand-400 dark:hover:bg-charcoal-blue-900"
					>
						Create a free account
						<AnimatedIcon name="arrowRight" size={18} aria-hidden="true" />
					</Link>
					<Link
						href="#pricing"
						className="btn-lg inline-flex items-center justify-center gap-2 rounded-2xl border border-white/20 bg-white/10 px-6 py-3.5 font-semibold text-white backdrop-blur-sm transition-colors duration-200 ease-out hover:bg-white/20 active:scale-[0.97]"
					>
						Compare plans
					</Link>
				</div>
			</div>
		</section>
	);
}
