const METRICS = [
	"2M+ meals logged",
	"6-second avg log time",
	"94% stick-to-it rate",
	"14-day refund",
	"No ads, ever",
	"Multi-household sharing",
];

// Horizontal ticker using the eth-marquee keyframe (confirmed working pair).
// Edge fades pick up the page background so the loop appears to melt into it.
export function MetricsTicker() {
	return (
		<section aria-label="Platform metrics" className="eth-marquee-track relative overflow-hidden py-5">
			<div
				aria-hidden="true"
				className="pointer-events-none absolute inset-y-0 left-0 z-10 w-24"
				style={{ background: "linear-gradient(to right, var(--background, var(--color-charcoal-blue-50)), transparent)" }}
			/>
			<div
				aria-hidden="true"
				className="pointer-events-none absolute inset-y-0 right-0 z-10 w-24"
				style={{ background: "linear-gradient(to left, var(--background, var(--color-charcoal-blue-50)), transparent)" }}
			/>
			<div className="eth-marquee flex w-max">
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
