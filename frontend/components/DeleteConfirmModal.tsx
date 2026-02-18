"use client";

import { useState } from "react";
import Loading from "@/components/Loading";

interface DeleteConfirmModalProps {
    isOpen: boolean;
    onClose: () => void;
    onConfirm: () => void;
    itemName?: string;
}

export function DeleteConfirmModal({ isOpen, onClose, onConfirm, itemName }: DeleteConfirmModalProps) {
    const [isDeleting, setIsDeleting] = useState(false);

    if (!isOpen) return null;

    const handleConfirm = async () => {
        setIsDeleting(true);
        await onConfirm();
        setIsDeleting(false);
        onClose();
    };

    return (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50" data-testid="delete-confirm-modal">
            <div className="bg-white dark:bg-slate-900 rounded-2xl p-6 max-w-md w-full mx-4 shadow-xl">
                <div className="flex items-center gap-3 mb-4">
                    <div className="w-12 h-12 rounded-full bg-red-100 dark:bg-red-900/30 flex items-center justify-center">
                        <i className="ri-delete-bin-line text-2xl text-red-600 dark:text-red-400" />
                    </div>
                    <h3 className="text-xl font-semibold text-slate-900 dark:text-slate-100">
                        Delete {itemName || "Item"}?
                    </h3>
                </div>

                <p className="text-slate-600 dark:text-slate-400 mb-6">
                    This action cannot be undone. Are you sure you want to delete this {itemName?.toLowerCase() || "item"}?
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
}
