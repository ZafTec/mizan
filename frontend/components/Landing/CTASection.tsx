import Link from "next/link";
import { AnimatedIcon } from "@/components/ui/animated-icon";

export function CTASection() {
	return (
		<section
			data-testid="cta-section"
			aria-labelledby="final-cta-heading"
			className="relative overflow-hidden rounded-[28px] py-10 text-white sm:py-14"
			style={{ background: "var(--color-charcoal-blue-950)" }}
		>
			<div
				aria-hidden="true"
				className="pointer-events-none absolute inset-0"
				style={{
					background:
						"radial-gradient(ellipse at center, color-mix(in oklab, var(--color-brand-500) 18%, transparent), transparent 65%)",
					filter: "blur(60px)",
				}}
			/>
			<div className="relative z-10 mx-auto flex max-w-4xl flex-col items-center gap-5 px-6 text-center sm:flex-row sm:justify-between sm:text-left">
				<div className="max-w-xl">
					<h2 id="final-cta-heading" className="text-2xl font-semibold tracking-tight sm:text-3xl">
						Start free. Upgrade the day you need <span className="text-brand-300">more</span>.
					</h2>
					<p className="mt-2 text-sm text-white/70">
						No credit card · Pro from $1.99 / month · cancel anytime
					</p>
				</div>
				<div className="flex flex-col gap-2 sm:flex-row sm:items-center">
					<Link
						href="/register"
						className="btn-secondary btn-lg border-white/20 bg-white/95 text-brand-700 hover:bg-white dark:bg-white/95 dark:text-brand-700 dark:hover:bg-white"
					>
						Create account
						<AnimatedIcon name="arrowRight" size={16} aria-hidden="true" />
					</Link>
					<Link
						href="#pricing"
						className="btn-lg inline-flex items-center justify-center gap-2 rounded-2xl border border-white/20 bg-white/10 px-5 py-3 text-sm font-semibold text-white backdrop-blur-sm transition-colors duration-200 ease-out hover:bg-white/20 active:scale-[0.97]"
					>
						Compare plans
					</Link>
				</div>
			</div>
		</section>
	);
}
