import Link from "next/link";
import { AnimatedIcon } from "@/components/ui/animated-icon";

/**
 * A ruled band, not a dark spotlight - it closes the page instead of
 * interrupting it. Same border/background language as the metrics ticker.
 */
export function CTASection() {
	return (
		<section
			data-testid="cta-section"
			aria-labelledby="final-cta-heading"
			className="flex flex-col items-center gap-5 border-t border-charcoal-blue-200 py-10 text-center sm:flex-row sm:justify-between sm:py-14 sm:text-left dark:border-white/10"
		>
			<div className="max-w-xl">
				<h2 id="final-cta-heading" className="text-2xl font-medium tracking-tight text-charcoal-blue-900 sm:text-3xl dark:text-charcoal-blue-50">
					Start free. Upgrade the day you need <span className="text-brand-600 dark:text-brand-400">more</span>.
				</h2>
				<p className="mt-2 text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
					No credit card &middot; Pro from $1.99 / month &middot; cancel anytime
				</p>
			</div>
			<div className="flex flex-col gap-2 sm:flex-row sm:items-center">
				<Link href="/register" className="btn-primary btn-lg">
					Create account
					<AnimatedIcon name="arrowRight" size={16} aria-hidden="true" />
				</Link>
				<Link href="#pricing" className="btn-ghost btn-lg">
					Compare plans
				</Link>
			</div>
		</section>
	);
}
