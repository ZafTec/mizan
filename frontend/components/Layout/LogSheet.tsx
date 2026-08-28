"use client";

import Link from "next/link";
import { useEffect } from "react";
import { createPortal } from "react-dom";
import { Icon } from "@/components/ui/icon";
import FoodPhotoSheet from "@/components/ai/FoodPhotoSheet";
import { LOG_ACTIONS } from "./nav";

/**
 * The ( + ) sheet - see docs/REFOCUS.md §3.
 *
 * Logging opens over the current context and dismisses back to it. It is never
 * a page navigation, because the whole point of the spine is that recording a
 * meal does not cost you your place in the app.
 */
export default function LogSheet({ open, onClose }: { open: boolean; onClose: () => void }) {
	useEffect(() => {
		if (!open) return;
		const onKey = (e: KeyboardEvent) => {
			if (e.key === "Escape") onClose();
		};
		document.addEventListener("keydown", onKey);
		document.body.style.overflow = "hidden";
		return () => {
			document.removeEventListener("keydown", onKey);
			document.body.style.overflow = "";
		};
	}, [open, onClose]);

	if (!open || typeof document === "undefined") return null;

	return createPortal(
		<div
			className="modal-overlay-in fixed inset-0 z-100 flex items-end justify-center bg-charcoal-blue-950/40 backdrop-blur-sm sm:items-center"
			onClick={onClose}
			role="dialog"
			aria-modal="true"
			aria-label="Log an entry"
		>
			<div
				className="sheet-pop-in surface-panel w-full max-w-md rounded-b-none p-5 pb-[calc(1.25rem+env(safe-area-inset-bottom,0))] sm:rounded-b-2xl sm:pb-5"
				onClick={(e) => e.stopPropagation()}
			>
				<div className="mb-4 flex items-center justify-between">
					<h2 className="text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
						Log an entry
					</h2>
					<button
						type="button"
						onClick={onClose}
						className="flex h-9 w-9 items-center justify-center rounded-xl text-charcoal-blue-500 hover:text-charcoal-blue-900 dark:text-charcoal-blue-300 dark:hover:text-white"
						aria-label="Close"
					>
						<Icon name="x" size={16} />
					</button>
				</div>

				<div className="space-y-2">
					{LOG_ACTIONS.map((action) => (
						<Link
							key={action.href}
							href={action.href}
							onClick={onClose}
							className="flex items-center gap-4 rounded-2xl border border-charcoal-blue-200/70 p-4 transition-colors hover:border-brand-500/40 hover:bg-brand-50/60 dark:border-white/10 dark:hover:border-brand-400/40 dark:hover:bg-white/5"
						>
							<span className="icon-chip h-11 w-11 shrink-0 text-brand-600 dark:text-brand-400">
								<Icon name={action.icon} size={20} />
							</span>
							<span className="min-w-0">
								<span className="block font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
									{action.label}
								</span>
								{action.description && (
									<span className="block text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
										{action.description}
									</span>
								)}
							</span>
						</Link>
					))}

					<FoodPhotoSheet onLogged={onClose} />
				</div>
			</div>
		</div>,
		document.body
	);
}
