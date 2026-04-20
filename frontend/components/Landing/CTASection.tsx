import Link from "next/link";
import { AnimatedIcon } from "@/components/ui/animated-icon";

export function CTASection() {
	return (
		<section
			data-testid="cta-section"
			className="surface-panel relative overflow-hidden rounded-[32px] bg-charcoal-blue-900 p-8 text-center sm:p-12 dark:bg-charcoal-blue-950"
		>
			<div className="absolute inset-0 bg-[url('/grid.svg')] opacity-10" />
			<div className="absolute left-1/2 top-0 h-150 w-150 -translate-x-1/2 rounded-full bg-white/6 blur-3xl" />
			<div className="absolute -bottom-16 right-0 h-48 w-48 rounded-full bg-brand-500/10 blur-3xl" />

			<div className="relative z-10 max-w-lg mx-auto">
				<div className="mb-5 inline-flex items-center gap-2 rounded-full border border-white/15 bg-white/10 px-3 py-1 text-xs font-semibold uppercase tracking-[0.18em] backdrop-blur-sm">
					<AnimatedIcon name="rocket" size={14} aria-hidden="true" />
					Ready when you are
				</div>
				<h2 className="mb-4 text-3xl font-semibold tracking-tight sm:text-4xl">
					Start tracking for free
				</h2>
				<p className="mb-8 leading-relaxed">
					No credit card required. Set up in under a minute. Cancel anytime.
				</p>
				<div className="flex flex-col sm:flex-row gap-4 justify-center">
					<Link
						href="/register"
						className="btn-secondary btn-lg border-white/20 bg-white/95 dark:bg-charcoal-blue-900/60 text-brand-700 dark:text-brand-400 shadow-xl shadow-charcoal-blue-950/10 hover:bg-white dark:hover:bg-charcoal-blue-900"
					>
						Get Started Free
						<AnimatedIcon name="arrowRight" size={18} aria-hidden="true" />
					</Link>
				</div>
			</div>
		</section>
	);
}
