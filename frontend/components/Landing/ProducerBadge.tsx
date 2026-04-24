import { AnimatedIcon } from "@/components/ui/animated-icon";

// A small pill that attributes Mizan to Zaftech. Links out to the producer site
// in a new tab so the marketing landing still holds the user's attention.
export function ProducerBadge({ className = "" }: { className?: string }) {
	return (
		<a
			href="https://zaftech.co"
			target="_blank"
			rel="noopener noreferrer"
			className={`group inline-flex items-center gap-2 rounded-full border border-white/15 bg-white/5 px-3 py-1 text-xs font-semibold uppercase tracking-[0.18em] text-white/90 backdrop-blur-sm transition-colors hover:border-white/25 hover:bg-white/10 ${className}`}
		>
			<span className="relative flex h-1.5 w-1.5">
				<span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-brand-300/75 opacity-75" />
				<span className="relative inline-flex h-1.5 w-1.5 rounded-full bg-brand-300" />
			</span>
			A Zaftech product
			<i className="ri-arrow-right-up-line text-[11px] transition-transform duration-200 ease-out group-hover:translate-x-0.5 group-hover:-translate-y-0.5" aria-hidden="true" />
		</a>
	);
}
