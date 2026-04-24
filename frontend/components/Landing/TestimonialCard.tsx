"use client";

import { useRef, useState } from "react";
import { AnimatedIcon } from "@/components/ui/animated-icon";

const TESTIMONIALS = [
	{
		name: "Sarah M.",
		role: "Lost 12 kg in 4 months",
		quote: "The first tracker I've used that doesn't feel like homework. Exactly the level of detail I want.",
		initials: "SM",
		accent: "brand" as const,
		size: "lg" as const,
	},
	{
		name: "Daniel K.",
		role: "National-level cyclist",
		quote: "Finally, a macro tracker that handles periodised carbs properly.",
		initials: "DK",
		accent: "peach" as const,
		size: "md" as const,
	},
	{
		name: "Amira T.",
		role: "Certified trainer, 120+ clients",
		quote: "I run my entire coaching practice through Mizan. The shared goal tracking is the whole reason I switched.",
		initials: "AT",
		accent: "sun" as const,
		size: "md" as const,
	},
	{
		name: "Mesfin B.",
		role: "Home cook, weekend athlete",
		quote: "The recipe workshop with scaling is art.",
		initials: "MB",
		accent: "sand" as const,
		size: "sm" as const,
	},
];

const ACCENT_CLASS = {
	brand: { bg: "bg-brand-500/15", text: "text-brand-700 dark:text-brand-300" },
	peach: { bg: "bg-burnt-peach-500/15", text: "text-burnt-peach-700 dark:text-burnt-peach-300" },
	sun: { bg: "bg-tuscan-sun-500/20", text: "text-tuscan-sun-700 dark:text-tuscan-sun-300" },
	sand: { bg: "bg-sandy-brown-500/15", text: "text-sandy-brown-700 dark:text-sandy-brown-300" },
};

// Magnetic draggable testimonial. Idle: inner .eth-drift wrapper traces a
// tiny circle so the card hints "grab me". Drag: outer translate tracks the
// pointer with rubber-band damping. Release: 420ms ease-out snap to origin.
function DraggableTestimonial({
	testimonial,
	spanClass,
	offsetClass,
	driftDelay,
}: {
	testimonial: (typeof TESTIMONIALS)[number];
	spanClass: string;
	offsetClass: string;
	driftDelay: string;
}) {
	const outerRef = useRef<HTMLElement>(null);
	const [isDragging, setIsDragging] = useState(false);
	const startPos = useRef({ x: 0, y: 0, pointerId: -1 });

	const setTransform = (x: number, y: number, snap: boolean) => {
		const el = outerRef.current;
		if (!el) return;
		el.style.transition = snap ? "transform 420ms cubic-bezier(0.23, 1, 0.32, 1)" : "none";
		el.style.transform = `translate3d(${x}px, ${y}px, 0)`;
	};

	const onPointerDown = (e: React.PointerEvent<HTMLElement>) => {
		if (e.button !== 0 && e.pointerType === "mouse") return;
		startPos.current = { x: e.clientX, y: e.clientY, pointerId: e.pointerId };
		outerRef.current?.setPointerCapture(e.pointerId);
		setIsDragging(true);
	};

	const onPointerMove = (e: React.PointerEvent<HTMLElement>) => {
		if (!isDragging || startPos.current.pointerId !== e.pointerId) return;
		const dx = e.clientX - startPos.current.x;
		const dy = e.clientY - startPos.current.y;
		const distance = Math.hypot(dx, dy);
		const damping = distance > 120 ? 120 / distance + 0.5 * (1 - 120 / distance) : 1;
		setTransform(dx * damping, dy * damping, false);
	};

	const release = (e: React.PointerEvent<HTMLElement>) => {
		if (startPos.current.pointerId !== e.pointerId) return;
		setIsDragging(false);
		setTransform(0, 0, true);
		startPos.current.pointerId = -1;
	};

	const accent = ACCENT_CLASS[testimonial.accent];

	return (
		<article
			ref={outerRef}
			onPointerDown={onPointerDown}
			onPointerMove={onPointerMove}
			onPointerUp={release}
			onPointerCancel={release}
			data-dragging={isDragging ? "true" : "false"}
			className={`card relative select-none touch-none ${spanClass} ${offsetClass} p-6 sm:p-7`}
			style={{
				cursor: isDragging ? "grabbing" : "grab",
				willChange: "transform",
			}}
		>
			<div className="eth-drift" style={{ animationDelay: driftDelay }}>
				<p
					className={`italic text-charcoal-blue-800 dark:text-charcoal-blue-100 ${
						testimonial.size === "lg" ? "text-lg font-medium" : "text-base"
					}`}
				>
					&ldquo;{testimonial.quote}&rdquo;
				</p>
				<div className="mt-4 flex items-center gap-3">
					<div
						className={`flex h-9 w-9 items-center justify-center rounded-full text-xs font-semibold ${accent.bg} ${accent.text}`}
					>
						{testimonial.initials}
					</div>
					<div>
						<p className="text-sm font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100">{testimonial.name}</p>
						<p className="text-xs uppercase tracking-[0.12em] text-charcoal-blue-500 dark:text-charcoal-blue-400">{testimonial.role}</p>
					</div>
				</div>
			</div>
		</article>
	);
}

export function TestimonialSection() {
	const layouts: Array<{ span: string; offset: string }> = [
		{ span: "md:col-span-4", offset: "" },
		{ span: "md:col-span-2", offset: "md:translate-y-6" },
		{ span: "md:col-span-3", offset: "md:-translate-y-4" },
		{ span: "md:col-span-3", offset: "" },
	];

	return (
		<section aria-labelledby="testimonial-heading" className="py-12 sm:py-16">
			<div className="mb-10 max-w-3xl sm:mb-12">
				<div className="eyebrow mb-4">
					<AnimatedIcon name="heart" size={14} aria-hidden="true" />
					Real outcomes
				</div>
				<h2 id="testimonial-heading" className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
					People who take this seriously.
				</h2>
			</div>

			<div className="grid grid-cols-1 gap-4 md:grid-cols-6">
				{TESTIMONIALS.map((t, i) => (
					<DraggableTestimonial
						key={t.name}
						testimonial={t}
						spanClass={layouts[i].span}
						offsetClass={layouts[i].offset}
						driftDelay={`${i * -1.7}s`}
					/>
				))}
			</div>
		</section>
	);
}
