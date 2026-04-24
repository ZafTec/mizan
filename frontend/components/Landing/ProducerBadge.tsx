// Zaftech attribution pill. Always sits on a dark hero background so the
// inline palette is locked to the hero's color context (teal on translucent
// white) and doesn't switch with the theme like the rest of the page.
export function ProducerBadge({ className = "" }: { className?: string }) {
	return (
		<a
			href="https://zaftech.co"
			target="_blank"
			rel="noopener noreferrer"
			className={`group inline-flex items-center gap-2 rounded-full border border-white/15 bg-white/10 px-3 py-1 text-xs font-semibold uppercase tracking-[0.16em] text-white backdrop-blur-sm transition-colors hover:border-white/25 hover:bg-white/15 ${className}`}
		>
			<span className="relative flex h-1.5 w-1.5">
				<span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-brand-300 opacity-75" />
				<span className="relative inline-flex h-1.5 w-1.5 rounded-full bg-brand-300" />
			</span>
			A Zaftech product
			<i className="ri-arrow-right-up-line text-[11px] transition-transform duration-200 ease-out group-hover:-translate-y-0.5 group-hover:translate-x-0.5" aria-hidden="true" />
		</a>
	);
}
