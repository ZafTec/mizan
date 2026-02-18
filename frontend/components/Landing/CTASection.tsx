import Link from "next/link";

export function CTASection() {
	return (
		<section
			data-testid="cta-section"
			className="relative overflow-hidden rounded-3xl bg-linear-to-br from-brand-600 via-brand-700 to-brand-800 p-8 sm:p-12 text-center"
		>
			<div className="absolute inset-0 bg-[url('/grid.svg')] opacity-10" />
			<div className="absolute top-0 left-1/2 -translate-x-1/2 w-[600px] h-[600px] bg-brand-400/20 rounded-full blur-3xl" />

			<div className="relative z-10 max-w-lg mx-auto">
				<h2 className="text-3xl sm:text-4xl font-bold text-white mb-4 tracking-tight">
					Start tracking for free
				</h2>
				<p className="text-white/80 mb-8 leading-relaxed">
					No credit card required. Set up in under a minute. Cancel anytime.
				</p>
				<div className="flex flex-col sm:flex-row gap-4 justify-center">
					<Link
						href="/register"
						className="inline-flex items-center justify-center gap-2 px-8 py-3.5 bg-white dark:bg-slate-900 text-brand-700 dark:text-brand-400 font-semibold rounded-xl hover:bg-brand-50 dark:hover:bg-slate-800 transition-colors shadow-lg text-base"
					>
						Get Started Free
						<i className="ri-arrow-right-line" />
					</Link>
				</div>
			</div>
		</section>
	);
}
