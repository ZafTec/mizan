"use client";

import React, { useEffect, useState } from "react";
import { createPortal } from "react-dom";

interface ModalProps {
	isOpen: boolean;
	onClose: () => void;
	children: React.ReactNode;
}

const Modal: React.FC<ModalProps> = ({ isOpen, onClose, children }) => {
	const [mounted, setMounted] = useState(false);

	useEffect(() => {
		// eslint-disable-next-line react-hooks/set-state-in-effect -- mount detection for SSR-safe portal
		setMounted(true);
	}, []);

	useEffect(() => {
		if (!isOpen) return;
		document.body.style.overflow = "hidden";
		const handleEscape = (e: KeyboardEvent) => {
			if (e.key === "Escape") onClose();
		};
		window.addEventListener("keydown", handleEscape);
		return () => {
			window.removeEventListener("keydown", handleEscape);
			document.body.style.overflow = "";
		};
	}, [isOpen, onClose]);

	if (!isOpen || !mounted) return null;

	const modal = (
		<div
			className="fixed inset-0 z-[100] flex items-center justify-center overflow-y-auto bg-black/40 p-4"
			onClick={onClose}
		>
			<div
				className="relative my-auto w-full min-w-[320px] max-w-xl rounded-lg bg-white p-6 shadow-lg dark:bg-charcoal-blue-900"
				onClick={(e) => e.stopPropagation()}
			>
				<button
					className="absolute top-2 right-2 text-2xl text-charcoal-blue-400 hover:text-charcoal-blue-700 dark:text-charcoal-blue-500 dark:hover:text-charcoal-blue-300"
					onClick={onClose}
					aria-label="Close modal"
				>
					&times;
				</button>
				{children}
			</div>
		</div>
	);

	return createPortal(modal, document.body);
};

export default Modal;
