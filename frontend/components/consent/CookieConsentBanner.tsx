"use client";

import { useEffect, useState } from "react";
import { AnimatedIcon } from "@/components/ui/animated-icon";

// Slides up from the bottom with a strong ease-out on enter (400ms) and a
// fast exit (200ms) once the user decides. The banner only mounts when GA
// tracking is configured and the user hasn't chosen yet — see ConsentGate.
export function CookieConsentBanner({
	onDecision,
}: {
	onDecision: (decision: "accepted" | "declined") => void;
}) {
	const [visible, setVisible] = useState(false);
	const [exiting, setExiting] = useState(false);

	useEffect(() => {
		// Let the first paint happen with the banner off-screen, then flip
		// the state on the next frame so the transition actually runs.
		const id = requestAnimationFrame(() => setVisible(true));
		return () => cancelAnimationFrame(id);
	}, []);

	const decide = (decision: "accepted" | "declined") => {
		setExiting(true);
		// Exit is deliberately snappy — the user already made the call.
		window.setTimeout(() => onDecision(decision), 180);
	};

	const offscreen = !visible || exiting;

	return (
		<div
			role="dialog"
			aria-modal="false"
			aria-label="Cookie and analytics consent"
			className="pointer-events-none fixed inset-x-0 bottom-0 z-50 px-4 pb-4 sm:px-6 sm:pb-6"
		>
			<div
				style={{
					transform: offscreen ? "translateY(calc(100% + 1.5rem))" : "translateY(0)",
					opacity: offscreen ? 0 : 1,
					transition: exiting
						? "transform 200ms var(--ease-out), opacity 200ms var(--ease-out)"
						: "transform 420ms var(--ease-out), opacity 420ms var(--ease-out)",
				}}
				className="pointer-events-auto mx-auto flex max-w-3xl flex-col gap-4 rounded-[22px] border border-charcoal-blue-200 bg-white/95 p-5 shadow-2xl shadow-charcoal-blue-950/15 backdrop-blur-md sm:flex-row sm:items-center sm:justify-between sm:p-6 dark:border-white/10 dark:bg-charcoal-blue-900/95"
			>
				<div className="flex items-start gap-3 sm:items-center">
					<span className="mt-0.5 flex h-9 w-9 shrink-0 items-center justify-center rounded-2xl bg-brand-500/15 text-brand-700 sm:mt-0 dark:text-brand-300">
						<AnimatedIcon name="shieldCheck" size={18} aria-hidden="true" />
					</span>
					<div className="text-sm leading-relaxed text-charcoal-blue-700 dark:text-charcoal-blue-200">
						<p className="font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
							Mind if we count visits?
						</p>
						<p className="mt-1 text-charcoal-blue-600 dark:text-charcoal-blue-400">
							We use Google Analytics to see which pages help and which don&apos;t. No ads, no selling data. See our{" "}
							<a
								href="https://zaftech.co/cookies"
								target="_blank"
								rel="noopener noreferrer"
								className="font-medium text-brand-700 underline-offset-2 hover:underline dark:text-brand-300"
							>
								cookie policy
							</a>
							{" "}and{" "}
							<a
								href="https://zaftech.co/privacy"
								target="_blank"
								rel="noopener noreferrer"
								className="font-medium text-brand-700 underline-offset-2 hover:underline dark:text-brand-300"
							>
								privacy policy
							</a>
							.
						</p>
					</div>
				</div>
				<div className="flex shrink-0 items-center gap-2 sm:flex-row-reverse">
					<button
						type="button"
						onClick={() => decide("accepted")}
						className="btn-primary h-10 px-5 text-sm"
					>
						Accept
					</button>
					<button
						type="button"
						onClick={() => decide("declined")}
						className="btn-ghost h-10 px-4 text-sm"
					>
						Decline
					</button>
				</div>
			</div>
		</div>
	);
}
