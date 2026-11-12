"use client";

import * as React from "react";
import { Drawer as Vaul } from "vaul";
import { NoteFocus } from "@/components/ui/dialog";
import { Icon } from "@/components/ui/icon";
import { cn } from "@/lib/utils";

/**
 * The app's one drawer, on Vaul: a sheet up from the bottom of a phone, or a
 * column in from the right of a wider screen. It is a modal dialog (Radix
 * underneath): focus moves in and stays until it closes, Escape, the overlay
 * and the close button dismiss it, and focus returns to what opened it. A
 * bottom sheet also closes when it is dragged down; a list inside it still
 * scrolls. Centred dialogs use `ui/dialog` instead.
 *
 * Every drawer needs a DrawerTitle (it names the dialog) and a
 * DrawerDescription, visible or `sr-only`.
 */
export type DrawerDirection = "bottom" | "right";

const DirectionContext = React.createContext<DrawerDirection>("bottom");

export interface DrawerProps {
  open?: boolean;
  defaultOpen?: boolean;
  onOpenChange?: (open: boolean) => void;
  /** Runs once the opening or closing movement has finished. */
  onAnimationEnd?: (open: boolean) => void;
  direction?: DrawerDirection;
  /** False while something is saving: no drag, Escape or overlay close. */
  dismissible?: boolean;
  /** Only the handle starts a drag, for content that takes its own gestures. */
  handleOnly?: boolean;
  /** Moves focus to the first control on open; false when the content places it. */
  autoFocus?: boolean;
  children: React.ReactNode;
}

function Drawer({
  direction = "bottom",
  autoFocus = true,
  children,
  ...props
}: DrawerProps) {
  return (
    <DirectionContext.Provider value={direction}>
      <Vaul.Root direction={direction} autoFocus={autoFocus} {...props}>
        {children}
      </Vaul.Root>
    </DirectionContext.Provider>
  );
}

const DrawerTrigger = Vaul.Trigger;
const DrawerClose = Vaul.Close;

const SURFACE =
  "fixed z-[100] flex flex-col bg-background text-foreground outline-none";
const DIRECTIONS: Record<DrawerDirection, string> = {
  bottom:
    "inset-x-0 bottom-0 max-h-[92dvh] rounded-t-md border border-b-0 border-border",
  right: "inset-y-0 right-0 h-dvh w-[420px] max-w-full border-l border-border",
};

const DrawerContent = React.forwardRef<
  React.ElementRef<typeof Vaul.Content>,
  React.ComponentPropsWithoutRef<typeof Vaul.Content> & {
    /** The bar a bottom sheet is dragged by; off when the content draws its own. */
    handle?: boolean;
  }
>(({ className, children, handle = true, onCloseAutoFocus, ...props }, ref) => {
  const direction = React.useContext(DirectionContext);
  const returnTo = React.useRef<HTMLElement | null>(null);
  return (
    <Vaul.Portal>
      <Vaul.Overlay className="fixed inset-0 z-[100] bg-charcoal-blue-950/45" />
      <Vaul.Content
        ref={ref}
        className={cn(SURFACE, DIRECTIONS[direction], className)}
        // Radix returns focus only to a trigger; focus goes back to whatever opened the drawer.
        onCloseAutoFocus={(event) => {
          onCloseAutoFocus?.(event);
          if (event.defaultPrevented) return;
          event.preventDefault();
          if (returnTo.current?.isConnected) returnTo.current.focus();
        }}
        {...props}
      >
        <NoteFocus targetRef={returnTo} />
        {direction === "bottom" && handle && (
          // Vaul styles the handle with an unlayered rule, so the theme's colour needs `!`.
          <Vaul.Handle className="mt-2 mb-1 shrink-0 bg-border! opacity-100!" />
        )}
        {children}
      </Vaul.Content>
    </Vaul.Portal>
  );
});
DrawerContent.displayName = "DrawerContent";

/** The title row: title and description on the left, the close button on the right. */
function DrawerHeader({
  className,
  ...props
}: React.HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      className={cn(
        "flex shrink-0 items-start gap-3 border-b border-border py-2 pr-2 pl-5",
        className,
      )}
      {...props}
    />
  );
}

const DrawerTitle = React.forwardRef<
  React.ElementRef<typeof Vaul.Title>,
  React.ComponentPropsWithoutRef<typeof Vaul.Title>
>(({ className, ...props }, ref) => (
  <Vaul.Title
    ref={ref}
    className={cn("text-base leading-tight font-semibold", className)}
    {...props}
  />
));
DrawerTitle.displayName = "DrawerTitle";

const DrawerDescription = React.forwardRef<
  React.ElementRef<typeof Vaul.Description>,
  React.ComponentPropsWithoutRef<typeof Vaul.Description>
>(({ className, ...props }, ref) => (
  <Vaul.Description
    ref={ref}
    className={cn("mt-1 text-sm text-muted-foreground", className)}
    {...props}
  />
));
DrawerDescription.displayName = "DrawerDescription";

/** The header's close control, a 44px target. */
function DrawerCloseButton({
  label = "Close",
  className,
  ...props
}: React.ComponentPropsWithoutRef<typeof Vaul.Close> & { label?: string }) {
  return (
    <Vaul.Close
      aria-label={label}
      className={cn(
        "-my-1 flex size-11 shrink-0 items-center justify-center rounded-sm text-charcoal-blue-700 hover:bg-muted disabled:opacity-50 dark:text-charcoal-blue-300",
        className,
      )}
      {...props}
    >
      <Icon name="x" size={18} aria-hidden="true" />
    </Vaul.Close>
  );
}

/** The part of a drawer that scrolls; it clears the phone's home indicator. */
function DrawerBody({
  className,
  ...props
}: React.HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      className={cn(
        // Vaul turns off text selection on the drawer; what it shows can still be copied.
        "min-h-0 flex-1 overflow-y-auto overscroll-contain px-5 py-4 pb-[calc(1rem+env(safe-area-inset-bottom,0px))] select-text",
        className,
      )}
      {...props}
    />
  );
}

/**
 * An invisible drag strip over the top of a sheet whose content draws its own
 * handle bar. Vaul only drags from what is rendered inside its content.
 */
function DrawerDragStrip() {
  return (
    <Vaul.Handle className="absolute! inset-x-0 top-0 z-10 m-0! h-4! w-auto! rounded-none! bg-transparent! [&_[data-vaul-handle-hitarea]]:hidden" />
  );
}

export {
  Drawer,
  DrawerDragStrip,
  DrawerTrigger,
  DrawerClose,
  DrawerContent,
  DrawerHeader,
  DrawerTitle,
  DrawerDescription,
  DrawerCloseButton,
  DrawerBody,
};
