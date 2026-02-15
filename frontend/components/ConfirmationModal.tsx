"use client";

import { useEffect } from "react";

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
    isDanger = false
}: ConfirmationModalProps) {
    // Close on Escape key
    useEffect(() => {
        const handleEscape = (e: KeyboardEvent) => {
            if (e.key === "Escape" && isOpen) {
                onClose();
            }
        };
        window.addEventListener("keydown", handleEscape);
        return () => window.removeEventListener("keydown", handleEscape);
    }, [isOpen, onClose]);

    if (!isOpen) return null;

    return (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm">
            <div className="card p-6 max-w-md w-full shadow-2xl animate-in fade-in zoom-in duration-200">
                <div className="flex items-start gap-4 mb-4">
                    <div className={`w-12 h-12 rounded-full flex items-center justify-center flex-shrink-0 ${isDanger ? 'bg-red-100 dark:bg-red-900/30' : 'bg-brand-100 dark:bg-brand-900/30'
                        }`}>
                        <i className={`text-2xl ${isDanger ? 'ri-error-warning-line text-red-600 dark:text-red-400' : 'ri-question-line text-brand-600 dark:text-brand-400'
                            }`} />
                    </div>
                    <div className="flex-1">
                        <h2 className="text-xl font-bold text-slate-900 dark:text-slate-100 mb-1">{title}</h2>
                        <p className="text-slate-600 dark:text-slate-400">{message}</p>
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
                        className={`btn-primary flex items-center gap-2 ${isDanger ? '!bg-red-600 hover:!bg-red-700' : ''
                            }`}
                    >
                        {isLoading ? (
                            <>
                                <i className="ri-loader-4-line animate-spin" />
                                {isDanger ? 'Deleting...' : 'Processing...'}
                            </>
                        ) : (
                            <>
                                <i className={isDanger ? "ri-delete-bin-line" : "ri-check-line"} />
                                {confirmText}
                            </>
                        )}
                    </button>
                </div>
            </div>
        </div>
    );
}
