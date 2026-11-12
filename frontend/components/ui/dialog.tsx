"use client";

import * as React from "react";
import * as DialogPrimitive from "@radix-ui/react-dialog";

import { Icon } from "@/components/ui/icon";
import { cn } from "@/lib/utils";

/**
 * The app's centred modal dialog, on Radix. Focus moves to the first control
 * in the body (or the footer), Tab stays inside, Escape and the overlay close
 * it, and focus returns to where it was. Drawers and sheets use `ui/drawer`.
 *
 * `Modal` below is the whole dialog in one component; the parts are for a
 * dialog whose layout they do not cover.
 */
const Dialog = DialogPrimitive.Root;

const DialogTrigger = DialogPrimitive.Trigger;

const DialogPortal = DialogPrimitive.Portal;

const DialogClose = DialogPrimitive.Close;

const DialogOverlay = React.forwardRef<
  React.ElementRef<typeof DialogPrimitive.Overlay>,
  React.ComponentPropsWithoutRef<typeof DialogPrimitive.Overlay>
>(({ className, ...props }, ref) => (
  <DialogPrimitive.Overlay
    ref={ref}
    className={cn(
      "animate-overlay-in data-[state=closed]:animate-overlay-out fixed inset-0 z-[100] bg-charcoal-blue-950/45",
      className,
    )}
    {...props}
  />
));
DialogOverlay.displayName = DialogPrimitive.Overlay.displayName;

/**
 * Notes what had focus as the dialog mounts. It renders first inside the
 * content, so its layout effect runs before a child's autoFocus moves focus.
 */
export function NoteFocus({
  targetRef,
}: {
  targetRef: React.RefObject<HTMLElement | null>;
}) {
  React.useLayoutEffect(() => {
    targetRef.current =
      document.activeElement instanceof HTMLElement
        ? document.activeElement
        : null;
    // Once, as the content mounts.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);
  return null;
}

const DialogContent = React.forwardRef<
  React.ElementRef<typeof DialogPrimitive.Content>,
  React.ComponentPropsWithoutRef<typeof DialogPrimitive.Content> & {
    /** The corner close button. A confirmation has Cancel instead. */
    showClose?: boolean;
    closeLabel?: string;
  }
>(
  (
    {
      className,
      children,
      showClose = true,
      closeLabel = "Close",
      onCloseAutoFocus,
      ...props
    },
    ref,
  ) => {
    const returnTo = React.useRef<HTMLElement | null>(null);
    return (
      <DialogPortal>
        <DialogOverlay />
        <DialogPrimitive.Content
          ref={ref}
          aria-modal="true"
          // Radix returns focus only to a Dialog.Trigger; most dialogs here open
          // from state, so focus goes back to whatever opened them.
          onCloseAutoFocus={(event) => {
            onCloseAutoFocus?.(event);
            if (event.defaultPrevented) return;
            event.preventDefault();
            if (returnTo.current?.isConnected) returnTo.current.focus();
          }}
          className={cn(
            // A bounded column: the header and footer keep their place and only a
            // DialogBody scrolls, so the actions never leave the screen.
            "animate-dialog-in data-[state=closed]:animate-dialog-out fixed top-[50%] left-[50%] z-[100] flex max-h-[calc(100dvh-2rem)] w-[calc(100%-2rem)] max-w-lg flex-col gap-4 overflow-hidden rounded-md border border-border bg-background p-6 text-foreground outline-none",
            className,
          )}
          {...props}
        >
          <NoteFocus targetRef={returnTo} />
          {children}
          {showClose && (
            <DialogPrimitive.Close
              aria-label={closeLabel}
              className="absolute top-3 right-3 flex size-10 items-center justify-center rounded-sm text-muted-foreground transition-colors hover:bg-muted hover:text-foreground disabled:pointer-events-none"
            >
              <Icon name="x" size={16} aria-hidden="true" />
            </DialogPrimitive.Close>
          )}
        </DialogPrimitive.Content>
      </DialogPortal>
    );
  },
);
DialogContent.displayName = DialogPrimitive.Content.displayName;

const DialogHeader = ({
  className,
  ...props
}: React.HTMLAttributes<HTMLDivElement>) => (
  <div
    className={cn(
      "flex shrink-0 flex-col space-y-1.5 pr-8 text-left",
      className,
    )}
    {...props}
  />
);
DialogHeader.displayName = "DialogHeader";

const DialogFooter = ({
  className,
  ...props
}: React.HTMLAttributes<HTMLDivElement>) => (
  <div
    className={cn(
      "flex shrink-0 flex-col-reverse gap-2 sm:flex-row sm:justify-end",
      className,
    )}
    {...props}
  />
);
DialogFooter.displayName = "DialogFooter";

/** The part of a dialog that scrolls when it is taller than the screen. */
const DialogBody = ({
  className,
  ...props
}: React.HTMLAttributes<HTMLDivElement>) => (
  <div
    data-modal-body=""
    className={cn(
      "-mx-6 min-h-0 flex-1 overflow-y-auto overscroll-contain px-6",
      className,
    )}
    {...props}
  />
);
DialogBody.displayName = "DialogBody";

const DialogTitle = React.forwardRef<
  React.ElementRef<typeof DialogPrimitive.Title>,
  React.ComponentPropsWithoutRef<typeof DialogPrimitive.Title>
>(({ className, ...props }, ref) => (
  <DialogPrimitive.Title
    ref={ref}
    className={cn(
      "text-lg leading-tight font-semibold tracking-tight",
      className,
    )}
    {...props}
  />
));
DialogTitle.displayName = DialogPrimitive.Title.displayName;

const DialogDescription = React.forwardRef<
  React.ElementRef<typeof DialogPrimitive.Description>,
  React.ComponentPropsWithoutRef<typeof DialogPrimitive.Description>
>(({ className, ...props }, ref) => (
  <DialogPrimitive.Description
    ref={ref}
    className={cn("text-sm text-muted-foreground", className)}
    {...props}
  />
));
DialogDescription.displayName = DialogPrimitive.Description.displayName;

const SIZES = {
  sm: "max-w-sm",
  md: "max-w-md",
  lg: "max-w-lg",
  xl: "max-w-2xl",
} as const;

export type ModalSize = keyof typeof SIZES;

export interface ModalProps {
  open: boolean;
  onClose: () => void;
  title: React.ReactNode;
  description?: React.ReactNode;
  children?: React.ReactNode;
  /** The actions, kept in view under a scrolling body. */
  footer?: React.ReactNode;
  size?: ModalSize;
  /** `alertdialog` for a confirmation that interrupts. */
  role?: "dialog" | "alertdialog";
  /** Takes focus on open instead of the first control. */
  initialFocus?: React.RefObject<HTMLElement | null>;
  /** False while something is saving, so a stray click does not lose it. */
  closeOnOverlayClick?: boolean;
  showClose?: boolean;
  className?: string;
  bodyClassName?: string;
}

/** A whole dialog: a title (and description), a scrolling body and a footer of actions. */
function Modal({
  open,
  onClose,
  title,
  description,
  children,
  footer,
  size = "md",
  role = "dialog",
  initialFocus,
  closeOnOverlayClick = true,
  showClose = true,
  className,
  bodyClassName,
}: ModalProps) {
  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        if (!next) onClose();
      }}
    >
      <DialogContent
        role={role}
        showClose={showClose}
        className={cn(SIZES[size], className)}
        // Without a description nothing describes the dialog, and Radix is told so.
        {...(description ? {} : { "aria-describedby": undefined })}
        onOpenAutoFocus={(event) => {
          const target = initialFocus?.current;
          if (!target) return;
          event.preventDefault();
          target.focus();
        }}
        onPointerDownOutside={(event) => {
          if (!closeOnOverlayClick) event.preventDefault();
        }}
      >
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          {description && (
            <DialogDescription asChild>
              <div>{description}</div>
            </DialogDescription>
          )}
        </DialogHeader>
        {children !== undefined && (
          <DialogBody className={bodyClassName}>{children}</DialogBody>
        )}
        {footer && <DialogFooter>{footer}</DialogFooter>}
      </DialogContent>
    </Dialog>
  );
}

export {
  Dialog,
  DialogPortal,
  DialogOverlay,
  DialogTrigger,
  DialogClose,
  DialogContent,
  DialogHeader,
  DialogBody,
  DialogFooter,
  DialogTitle,
  DialogDescription,
  Modal,
};
