"use client";

import {
  useEffect,
  useId,
  useRef,
  useState,
  type KeyboardEvent as ReactKeyboardEvent,
  type ReactNode,
} from "react";
import { createPortal } from "react-dom";

const FOCUSABLE = [
  "a[href]",
  "button:not([disabled])",
  "input:not([disabled]):not([type='hidden'])",
  "select:not([disabled])",
  "textarea:not([disabled])",
  "[tabindex]:not([tabindex='-1'])",
].join(",");

/** The controls Tab can reach inside the dialog, in document order. */
function focusables(root: HTMLElement): HTMLElement[] {
  return Array.from(root.querySelectorAll<HTMLElement>(FOCUSABLE)).filter(
    (element) => element.getClientRects().length > 0,
  );
}

/**
 * Viewport-pinned modal dialog. Fixes the "modal is centered on the page
 * instead of the screen" bug by:
 *   - Rendering to document.body via React portal (escapes any ancestor with
 *     backdrop-filter / transform / filter, which would otherwise become the
 *     containing block for `position: fixed`).
 *   - Locking body scroll while open.
 *   - Closing on Escape + overlay click.
 *
 * It is a modal dialog for assistive technology and the keyboard: focus moves
 * inside when it opens (unless a child already took it with autoFocus), Tab
 * and Shift+Tab cycle within it, and focus returns to where it was when it
 * closes. It is named by `label`, by the element `labelledBy` points at, or
 * by its first heading.
 *
 * Use this everywhere instead of hand-rolled `fixed inset-0 z-50` wrappers.
 */
export function ModalShell({
  open,
  onClose,
  children,
  closeOnOverlayClick = true,
  label,
  labelledBy,
}: {
  open: boolean;
  onClose: () => void;
  children: ReactNode;
  closeOnOverlayClick?: boolean;
  label?: string;
  labelledBy?: string;
}) {
  const [mounted, setMounted] = useState(false);
  const panel = useRef<HTMLDivElement>(null);
  const fallbackHeadingId = useId();
  const [headingId, setHeadingId] = useState<string | undefined>(undefined);
  // Where focus was when the dialog opened, read while rendering the open:
  // by the time an effect runs, a child's autoFocus has already moved it.
  const [wasOpen, setWasOpen] = useState(open);
  const [restoreTo, setRestoreTo] = useState<HTMLElement | null>(null);
  if (open !== wasOpen) {
    setWasOpen(open);
    if (open && typeof document !== "undefined")
      setRestoreTo(
        document.activeElement instanceof HTMLElement
          ? document.activeElement
          : null,
      );
  }

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

  // Focus in on open, back out on close. Only `open` and `mounted` drive it,
  // so a parent re-rendering with a new onClose does not move focus.
  useEffect(() => {
    if (!open || !mounted) return;
    const node = panel.current;
    const previous =
      restoreTo ??
      (document.activeElement instanceof HTMLElement &&
      !node?.contains(document.activeElement)
        ? document.activeElement
        : null);
    if (node) {
      if (!label && !labelledBy) {
        const heading = node.querySelector<HTMLElement>("h1, h2, h3, h4");
        if (heading) {
          if (!heading.id) heading.id = fallbackHeadingId;
          setHeadingId(heading.id);
        }
      }
      if (!node.contains(document.activeElement))
        (focusables(node)[0] ?? node).focus();
    }
    return () => {
      if (previous?.isConnected) previous.focus();
    };
    // restoreTo changes only in the render that opens the dialog.
  }, [open, mounted, label, labelledBy, fallbackHeadingId, restoreTo]);

  function trapTab(event: ReactKeyboardEvent<HTMLDivElement>) {
    if (event.key !== "Tab" || !panel.current) return;
    const items = focusables(panel.current);
    if (items.length === 0) {
      event.preventDefault();
      panel.current.focus();
      return;
    }
    const first = items[0];
    const last = items[items.length - 1];
    const active = document.activeElement;
    if (event.shiftKey && (active === first || active === panel.current)) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && active === last) {
      event.preventDefault();
      first.focus();
    }
  }

  if (!open || !mounted) return null;

  return createPortal(
    <div
      className="animate-overlay-in fixed inset-0 z-[100] flex items-center justify-center overflow-y-auto bg-black/50 p-4 backdrop-blur-sm"
      onClick={closeOnOverlayClick ? onClose : undefined}
    >
      <div
        ref={panel}
        role="dialog"
        aria-modal="true"
        aria-label={label}
        aria-labelledby={label ? undefined : (labelledBy ?? headingId)}
        tabIndex={-1}
        className="animate-modal-in my-auto w-full max-w-md outline-none"
        onClick={(e) => e.stopPropagation()}
        onKeyDown={trapTab}
      >
        {children}
      </div>
    </div>,
    document.body,
  );
}
