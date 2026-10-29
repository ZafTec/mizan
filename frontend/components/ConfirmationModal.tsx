"use client";

import { useEffect, useId, useState } from "react";
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
  const titleId = useId();

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
    document.body.style.overflow = "hidden";
    return () => {
      document.body.style.overflow = "";
    };
  }, [isOpen]);

  if (!isOpen || !mounted) return null;

  const modal = (
    <div
      className="animate-overlay-in fixed inset-0 z-[100] flex items-center justify-center overflow-y-auto bg-black/50 p-4 backdrop-blur-sm"
      onClick={onClose}
    >
      <div
        role="alertdialog"
        aria-modal="true"
        aria-labelledby={titleId}
        className="animate-modal-in card my-auto w-full max-w-md p-6 "
        onClick={(e) => e.stopPropagation()}
      >
        <h2 id={titleId} className="mb-1 text-xl font-semibold">
          {title}
        </h2>
        <p className="text-muted-foreground">{message}</p>

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
            className={isDanger ? "btn-danger" : "btn-primary"}
          >
            {isLoading ? "Working…" : confirmText}
          </button>
        </div>
      </div>
    </div>
  );

  return createPortal(modal, document.body);
}
