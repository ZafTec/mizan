"use client";

import { useEffect, useState } from "react";
import { createPortal } from "react-dom";
import Loading from "@/components/Loading";

interface DeleteConfirmModalProps {
	isOpen: boolean;
	onClose: () => void;
	onConfirm: () => void;
	itemName?: string;
}

export function DeleteConfirmModal({
	isOpen,
	onClose,
	onConfirm,
	itemName,
}: DeleteConfirmModalProps) {
	const [mounted, setMounted] = useState(false);
	const [isDeleting, setIsDeleting] = useState(false);

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

	const handleConfirm = async () => {
		setIsDeleting(true);
		await onConfirm();
		setIsDeleting(false);
		onClose();
	};

	const modal = (
		<div
			className="fixed inset-0 z-[100] flex items-center justify-center overflow-y-auto bg-black/50 p-4"
			data-testid="delete-confirm-modal"
			onClick={onClose}
		>
			<div
				className="my-auto w-full max-w-md rounded-2xl bg-white p-6 shadow-xl dark:bg-charcoal-blue-900"
				onClick={(e) => e.stopPropagation()}
			>
				<div className="flex items-center gap-3 mb-4">
					<div className="w-12 h-12 rounded-2xl bg-red-100 dark:bg-red-900/30 flex items-center justify-center">
						<i className="ri-delete-bin-line text-2xl text-red-600 dark:text-red-400" />
					</div>
					<h3 className="text-xl font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100">
						Delete {itemName || "Item"}?
					</h3>
				</div>

				<p className="text-charcoal-blue-600 dark:text-charcoal-blue-400 mb-6">
					This action cannot be undone. Are you sure you want to delete this{" "}
					{itemName?.toLowerCase() || "item"}?
				</p>

				<div className="flex gap-3 justify-end">
					<button
						onClick={onClose}
						disabled={isDeleting}
						className="btn-secondary"
					>
						Cancel
					</button>
					<button
						onClick={handleConfirm}
						disabled={isDeleting}
						className="btn-danger"
					>
						{isDeleting && <Loading size="sm" />}
						Delete
					</button>
				</div>
			</div>
		</div>
	);

	return createPortal(modal, document.body);
}
