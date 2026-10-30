"use client";

import {
  useEffect,
  useId,
  useRef,
  useState,
  type KeyboardEvent as ReactKeyboardEvent,
  type ReactNode,
  type RefObject,
} from "react";
import { createPortal } from "react-dom";
import { Icon } from "@/components/ui/icon";
import { cn } from "@/lib/utils";

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

const SIZES = {
  sm: "max-w-sm",
  md: "max-w-md",
  lg: "max-w-lg",
  xl: "max-w-2xl",
} as const;

export type ModalSize = keyof typeof SIZES;

/**
 * Viewport-pinned modal dialog, rendered into document.body so no ancestor's
 * transform or backdrop-filter becomes the containing block for its
 * `position: fixed`. It locks body scroll and closes on Escape and on an
 * overlay click.
 *
 * Given a `title`, it draws the whole dialog: a header with the title and a
 * close button, the children as the body, and `footer` for the actions. The
 * panel is never taller than the visible viewport (dvh, so the phone's
 * browser chrome is excluded); the header and footer stay put and only the
 * body scrolls. Without a `title`, the children are the whole panel and the
 * panel itself scrolls.
 *
 * It is a modal dialog for assistive technology and the keyboard: focus moves
 * inside when it opens (to `initialFocus`, or a child that took it with
 * autoFocus, or the first control in the body, then the footer), Tab and
 * Shift+Tab cycle within it, and focus returns to where it was when it
 * closes.
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
  title,
  description,
  footer,
  size = "md",
  role = "dialog",
  initialFocus,
  className,
  bodyClassName,
  showClose = true,
}: {
  open: boolean;
  onClose: () => void;
  children: ReactNode;
  closeOnOverlayClick?: boolean;
  label?: string;
  labelledBy?: string;
  title?: ReactNode;
  description?: ReactNode;
  footer?: ReactNode;
  size?: ModalSize;
  role?: "dialog" | "alertdialog";
  initialFocus?: RefObject<HTMLElement | null>;
  className?: string;
  bodyClassName?: string;
  /** The header's close button. A confirmation has Cancel instead. */
  showClose?: boolean;
}) {
  const [mounted, setMounted] = useState(false);
  const panel = useRef<HTMLDivElement>(null);
  const body = useRef<HTMLDivElement>(null);
  const footerRef = useRef<HTMLDivElement>(null);
  const titleId = useId();
  const descriptionId = useId();
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

  const structured = title !== undefined;

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
      if (!label && !labelledBy && !structured) {
        const heading = node.querySelector<HTMLElement>("h1, h2, h3, h4");
        if (heading) {
          if (!heading.id) heading.id = fallbackHeadingId;
          setHeadingId(heading.id);
        }
      }
      if (!node.contains(document.activeElement)) {
        const target =
          initialFocus?.current ??
          (body.current && focusables(body.current)[0]) ??
          (footerRef.current && focusables(footerRef.current)[0]) ??
          focusables(node)[0] ??
          node;
        target.focus();
      }
    }
    return () => {
      if (previous?.isConnected) previous.focus();
    };
    // restoreTo changes only in the render that opens the dialog.
  }, [
    open,
    mounted,
    label,
    labelledBy,
    structured,
    fallbackHeadingId,
    restoreTo,
    initialFocus,
  ]);

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
      className="animate-overlay-in fixed inset-0 z-[100] flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm"
      onClick={closeOnOverlayClick ? onClose : undefined}
    >
      <div
        ref={panel}
        role={role}
        aria-modal="true"
        aria-label={label}
        aria-labelledby={
          label ? undefined : (labelledBy ?? (structured ? titleId : headingId))
        }
        aria-describedby={structured && description ? descriptionId : undefined}
        tabIndex={-1}
        className={cn(
          "animate-modal-in flex max-h-[calc(100dvh-2rem)] w-full flex-col outline-none",
          SIZES[size],
          structured ? "card" : "overflow-y-auto overscroll-contain",
          className,
        )}
        onClick={(e) => e.stopPropagation()}
        onKeyDown={trapTab}
      >
        {structured ? (
          <>
            <div className="flex shrink-0 items-start justify-between gap-4 border-b border-charcoal-blue-200 py-4 pr-3 pl-5 dark:border-charcoal-blue-700">
              <div className="min-w-0 space-y-1 pt-1">
                <h2
                  id={titleId}
                  className="text-base font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50"
                >
                  {title}
                </h2>
                {description && (
                  <div
                    id={descriptionId}
                    className="text-sm text-charcoal-blue-600 dark:text-charcoal-blue-400"
                  >
                    {description}
                  </div>
                )}
              </div>
              {showClose && (
                <button
                  type="button"
                  onClick={onClose}
                  aria-label="Close"
                  className="btn-ghost shrink-0 p-2"
                >
                  <Icon name="x" size={16} />
                </button>
              )}
            </div>
            <div
              ref={body}
              data-modal-body=""
              className={cn(
                "min-h-0 flex-1 overflow-y-auto overscroll-contain px-5 py-4",
                bodyClassName,
              )}
            >
              {children}
            </div>
            {footer && (
              <div
                ref={footerRef}
                className="flex shrink-0 flex-col-reverse gap-2 border-t border-charcoal-blue-200 px-5 py-3 sm:flex-row sm:justify-end dark:border-charcoal-blue-700"
              >
                {footer}
              </div>
            )}
          </>
        ) : (
          children
        )}
      </div>
    </div>,
    document.body,
  );
}
