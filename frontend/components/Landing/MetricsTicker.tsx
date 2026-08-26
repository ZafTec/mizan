const METRICS = [
	"2M+ meals logged",
	"6-second avg log time",
	"94% stick-to-it rate",
	"14-day refund",
	"No ads, ever",
	"Multi-household sharing",
];

// A ruled strip, not a floating pill row - hairline top/bottom borders match
// the rest of the Daylight language instead of melting into the page.
export function MetricsTicker() {
	return (
		<section
			aria-label="Platform metrics"
			className="marquee-hover relative overflow-hidden border-y border-charcoal-blue-200 bg-white py-4 dark:border-white/10 dark:bg-charcoal-blue-950"
		>
			<div
				aria-hidden="true"
				className="pointer-events-none absolute inset-y-0 left-0 z-10 w-24"
				style={{ background: "linear-gradient(to right, var(--color-card), transparent)" }}
			/>
			<div
				aria-hidden="true"
				className="pointer-events-none absolute inset-y-0 right-0 z-10 w-24"
				style={{ background: "linear-gradient(to left, var(--color-card), transparent)" }}
			/>
			<div className="animate-marquee flex w-max">
				{[...METRICS, ...METRICS].map((metric, i) => (
					<div
						key={`${metric}-${i}`}
						className="flex items-center gap-6 whitespace-nowrap px-6 text-xs font-medium uppercase tracking-[0.14em] text-charcoal-blue-500 dark:text-charcoal-blue-400"
					>
						<span>{metric}</span>
						<span className="h-1 w-1 rounded-full bg-current opacity-40" aria-hidden="true" />
					</div>
				))}
			</div>
		</section>
	);
}
