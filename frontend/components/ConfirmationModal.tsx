"use client";

import { useEffect, useState } from "react";
import { createPortal } from "react-dom";

interface ConfirmationModalProps {
    isOpen: boolean;
    onClose: () => void;
    onConfirm: () => void;
    title: string;
    message: string;
    confirmText?: string;
    cancelText?: string;
    isLoading?: boolean;
    isDanger?: boolean;
}

export default function ConfirmationModal({
    isOpen,
    onClose,
    onConfirm,
    title,
    message,
    confirmText = "Confirm",
    cancelText = "Cancel",
    isLoading = false,
    isDanger = false,
}: ConfirmationModalProps) {
    const [mounted, setMounted] = useState(false);

    useEffect(() => {
        // eslint-disable-next-line react-hooks/set-state-in-effect -- mount detection for SSR-safe portal
        setMounted(true);
    }, []);

    // Close on Escape key
    useEffect(() => {
        if (!isOpen) return;
        const handleEscape = (e: KeyboardEvent) => {
            if (e.key === "Escape") onClose();
        };
        window.addEventListener("keydown", handleEscape);
        return () => window.removeEventListener("keydown", handleEscape);
    }, [isOpen, onClose]);

    // Lock body scroll while open so the modal truly overlays the viewport.
    useEffect(() => {
        if (!isOpen) return;
        const prev = document.body.style.overflow;
        document.body.style.overflow = "hidden";
        return () => {
            document.body.style.overflow = prev;
        };
    }, [isOpen]);

    if (!isOpen || !mounted) return null;

    const modal = (
        <div
            className="fixed inset-0 z-[100] flex items-center justify-center overflow-y-auto bg-black/50 p-4 backdrop-blur-sm"
            onClick={onClose}
        >
            <div
                className="card my-auto w-full max-w-md p-6 shadow-2xl animate-in fade-in zoom-in duration-200"
                onClick={(e) => e.stopPropagation()}
            >
                <div className="flex items-start gap-4 mb-4">
                    <div
                        className={`w-12 h-12 rounded-2xl flex items-center justify-center flex-shrink-0 ${
                            isDanger
                                ? "bg-red-100 dark:bg-red-900/30"
                                : "bg-brand-100 dark:bg-brand-900/30"
                        }`}
                    >
                        <i
                            className={`text-2xl ${
                                isDanger
                                    ? "ri-error-warning-line text-red-600 dark:text-red-400"
                                    : "ri-question-line text-brand-600 dark:text-brand-400"
                            }`}
                        />
                    </div>
                    <div className="flex-1">
                        <h2 className="text-xl font-bold text-charcoal-blue-900 dark:text-charcoal-blue-100 mb-1">
                            {title}
                        </h2>
                        <p className="text-charcoal-blue-600 dark:text-charcoal-blue-400">
                            {message}
                        </p>
                    </div>
                </div>

                <div className="flex gap-3 justify-end mt-6">
                    <button
                        onClick={onClose}
                        disabled={isLoading}
                        className="btn-secondary"
                    >
                        {cancelText}
                    </button>
                    <button
                        onClick={onConfirm}
                        disabled={isLoading}
                        className={`btn-primary flex items-center gap-2 ${
                            isDanger ? "!bg-red-600 hover:!bg-red-700" : ""
                        }`}
                    >
                        {isLoading ? (
                            <>
                                <i className="ri-loader-4-line animate-spin" />
                                {isDanger ? "Deleting..." : "Processing..."}
                            </>
                        ) : (
                            <>
                                <i
                                    className={
                                        isDanger ? "ri-delete-bin-line" : "ri-check-line"
                                    }
                                />
                                {confirmText}
                            </>
                        )}
                    </button>
                </div>
            </div>
        </div>
    );

    return createPortal(modal, document.body);
}
