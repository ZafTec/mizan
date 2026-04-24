export function ProducerBadge({ className = "" }: { className?: string }) {
	return (
		<a
			href="https://zaftech.co"
			target="_blank"
			rel="noopener noreferrer"
			className={`group inline-flex items-center gap-2 rounded-full px-3 py-1 eth-label transition-colors duration-200 ${className}`}
			style={{
				background: "rgba(110, 235, 224, 0.08)",
				color: "var(--eth-primary)",
			}}
		>
			<span className="relative flex h-1.5 w-1.5">
				<span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-current opacity-60" />
				<span className="relative inline-flex h-1.5 w-1.5 rounded-full bg-current" />
			</span>
			A Zaftech product
			<i className="ri-arrow-right-up-line text-[11px] transition-transform duration-200 ease-out group-hover:translate-x-0.5 group-hover:-translate-y-0.5" aria-hidden="true" />
		</a>
	);
}
