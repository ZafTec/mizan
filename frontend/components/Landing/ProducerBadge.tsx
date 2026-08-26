// Zaftech attribution pill. Plain eyebrow styling - same token as every other
// pill on the page, since the hero no longer carries its own dark background
// to design around.
export function ProducerBadge({ className = "" }: { className?: string }) {
	return (
		<a
			href="https://zaftech.co"
			target="_blank"
			rel="noopener noreferrer"
			className={`eyebrow gap-2 normal-case tracking-normal hover:border-charcoal-blue-300 dark:hover:border-white/20 ${className}`}
		>
			<span className="relative flex h-1.5 w-1.5">
				<span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-brand-500 opacity-75" />
				<span className="relative inline-flex h-1.5 w-1.5 rounded-full bg-brand-500" />
			</span>
			A Zaftech product
		</a>
	);
}
