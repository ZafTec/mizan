const METRICS = [
	"2M+ meals logged",
	"6-second avg log time",
	"94% stick-to-it rate",
	"14-day refund",
	"No ads, ever",
	"Multi-household sharing",
];

// Edge-to-edge marquee. Duplicated track for seamless loop. Hover pauses.
// 8px surface-container-lowest blocks bracket it — no 1px dividers.
export function MetricsTicker() {
	return (
		<section aria-label="Platform metrics" className="eth-marquee-track relative overflow-hidden py-5">
			<div
				aria-hidden="true"
				className="eth-divider absolute inset-x-0 top-0"
				style={{ background: "var(--eth-surface-lowest)" }}
			/>
			<div
				aria-hidden="true"
				className="eth-divider absolute inset-x-0 bottom-0"
				style={{ background: "var(--eth-surface-lowest)" }}
			/>
			<div className="relative overflow-hidden">
				{/* Edge fades to blend the marquee into the surface. */}
				<div
					aria-hidden="true"
					className="pointer-events-none absolute inset-y-0 left-0 z-10 w-24"
					style={{ background: "linear-gradient(to right, var(--eth-surface), transparent)" }}
				/>
				<div
					aria-hidden="true"
					className="pointer-events-none absolute inset-y-0 right-0 z-10 w-24"
					style={{ background: "linear-gradient(to left, var(--eth-surface), transparent)" }}
				/>
				<div className="eth-marquee flex w-max">
					{[...METRICS, ...METRICS].map((metric, i) => (
						<div key={`${metric}-${i}`} className="flex items-center gap-6 px-6 eth-label eth-text-dim whitespace-nowrap">
							<span>{metric}</span>
							<span className="h-1 w-1 rounded-full bg-current opacity-40" aria-hidden="true" />
						</div>
					))}
				</div>
			</div>
		</section>
	);
}
