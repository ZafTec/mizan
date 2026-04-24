"use client";

import { useEffect, useState, type ReactNode } from "react";
import { createPortal } from "react-dom";

/**
 * Viewport-pinned modal wrapper. Fixes the "modal is centered on the page
 * instead of the screen" bug by:
 *   - Rendering to document.body via React portal (escapes any ancestor with
 *     backdrop-filter / transform / filter, which would otherwise become the
 *     containing block for `position: fixed`).
 *   - Locking body scroll while open.
 *   - Closing on Escape + overlay click.
 *
 * Use this everywhere instead of hand-rolled `fixed inset-0 z-50` wrappers.
 */
export function ModalShell({
	open,
	onClose,
	children,
	closeOnOverlayClick = true,
}: {
	open: boolean;
	onClose: () => void;
	children: ReactNode;
	closeOnOverlayClick?: boolean;
}) {
	const [mounted, setMounted] = useState(false);

	useEffect(() => {
		// eslint-disable-next-line react-hooks/set-state-in-effect -- mount detection for SSR-safe portal
		setMounted(true);
	}, []);

	useEffect(() => {
		if (!open) return;
		document.body.style.overflow = "hidden";
		const onEsc = (e: KeyboardEvent) => {
			if (e.key === "Escape") onClose();
		};
		window.addEventListener("keydown", onEsc);
		return () => {
			// Always clear the inline override rather than restoring a saved value -
			// a saved "hidden" from overlapping modals can lock scroll permanently.
			document.body.style.overflow = "";
			window.removeEventListener("keydown", onEsc);
		};
	}, [open, onClose]);

	if (!open || !mounted) return null;

	return createPortal(
		<div
			className="fixed inset-0 z-[100] flex items-center justify-center overflow-y-auto bg-black/50 p-4 backdrop-blur-sm"
			onClick={closeOnOverlayClick ? onClose : undefined}
		>
			<div className="my-auto w-full max-w-md" onClick={(e) => e.stopPropagation()}>
				{children}
			</div>
		</div>,
		document.body
	);
}
