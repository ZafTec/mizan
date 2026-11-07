"use client";

import { useRef, useState, type ReactNode } from "react";
import { ModalShell } from "@/components/ModalShell";
import { getErrorMessage } from "@/lib/toast";

export interface ConfirmDialogProps {
  open: boolean;
  onClose: () => void;
  /** Names the action, e.g. "Delete this meal?". */
  title: ReactNode;
  /** Says what will be removed or changed. */
  description: ReactNode;
  /** Anything else the person should see before deciding. */
  children?: ReactNode;
  confirmLabel?: string;
  /** Shown on the confirm button while the request runs. */
  pendingLabel?: string;
  cancelLabel?: string;
  /**
   * Runs the action. When it resolves the dialog closes; when it throws, the
   * error is shown in the dialog and the dialog stays open to retry.
   */
  onConfirm: () => unknown;
  /** The caller's own pending flag, for actions that run elsewhere. */
  pending?: boolean;
  /** The caller's own error, shown inline. */
  error?: string | null;
  /** Fallback message when the thrown error has none. */
  errorFallback?: string;
  /** Red for anything that removes or ends something; the default. */
  destructive?: boolean;
  confirmDisabled?: boolean;
}

/**
 * The one confirmation for destructive actions and sign-out. Cancel takes
 * focus first, so Enter on open never destroys anything; the confirm button
 * is the filled destructive style, shows progress while the request runs and
 * the dialog shows a failure inline instead of closing.
 */
export function ConfirmDialog({
  open,
  onClose,
  title,
  description,
  children,
  confirmLabel = "Delete",
  pendingLabel,
  cancelLabel = "Cancel",
  onConfirm,
  pending: externalPending = false,
  error: externalError = null,
  errorFallback = "That did not work. Try again.",
  destructive = true,
  confirmDisabled = false,
}: ConfirmDialogProps) {
  const cancel = useRef<HTMLButtonElement>(null);
  const [running, setRunning] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [wasOpen, setWasOpen] = useState(open);
  if (open !== wasOpen) {
    setWasOpen(open);
    if (open) setError(null);
  }

  const pending = running || externalPending;
  const shownError = error ?? externalError;

  async function confirm() {
    setError(null);
    setRunning(true);
    try {
      await onConfirm();
      setRunning(false);
      onClose();
    } catch (caught) {
      setRunning(false);
      setError(getErrorMessage(caught, errorFallback));
    }
  }

  const close = () => {
    if (!pending) onClose();
  };

  return (
    <ModalShell
      open={open}
      onClose={close}
      closeOnOverlayClick={!pending}
      role="alertdialog"
      size="md"
      title={title}
      initialFocus={cancel}
      showClose={false}
      footer={
        <>
          <button
            ref={cancel}
            type="button"
            onClick={close}
            disabled={pending}
            className="btn-secondary"
          >
            {cancelLabel}
          </button>
          <button
            type="button"
            onClick={() => void confirm()}
            disabled={pending || confirmDisabled}
            aria-busy={pending || undefined}
            className={destructive ? "btn-danger" : "btn-primary"}
          >
            {pending ? (pendingLabel ?? `${confirmLabel}…`) : confirmLabel}
          </button>
        </>
      }
    >
      <div className="space-y-3 text-sm text-charcoal-blue-700 dark:text-charcoal-blue-300">
        <div>{description}</div>
        {children}
        {shownError && (
          <p
            role="alert"
            className="border-l-2 border-destructive bg-danger-wash px-3 py-2 text-destructive"
          >
            {shownError}
          </p>
        )}
      </div>
    </ModalShell>
  );
}

/**
 * A button that asks before it acts. The common case for a row's delete or
 * remove: the trigger, the confirmation and the request in one place.
 */
export function ConfirmButton({
  children,
  details,
  className = "btn-ghost-destructive btn-sm",
  disabled,
  "aria-label": ariaLabel,
  ...dialog
}: Omit<ConfirmDialogProps, "open" | "onClose" | "children"> & {
  /** The trigger's label. */
  children: ReactNode;
  /** Extra content for the dialog body, such as a reason field. */
  details?: ReactNode;
  className?: string;
  disabled?: boolean;
  "aria-label"?: string;
}) {
  const [open, setOpen] = useState(false);
  return (
    <>
      <button
        type="button"
        className={className}
        disabled={disabled}
        aria-label={ariaLabel}
        onClick={() => setOpen(true)}
      >
        {children}
      </button>
      <ConfirmDialog {...dialog} open={open} onClose={() => setOpen(false)}>
        {details}
      </ConfirmDialog>
    </>
  );
}
