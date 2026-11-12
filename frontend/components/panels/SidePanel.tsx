"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
  type RefObject,
} from "react";
import { createPortal } from "react-dom";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import {
  Drawer,
  DrawerContent,
  DrawerDragStrip,
  DrawerTitle,
} from "@/components/ui/drawer";
import { useMediaQuery } from "@/lib/hooks/useMediaQuery";

/**
 * The app shell's side panels: the assistant and the notifications. Each is a panel beside
 * whatever the person is doing, not a page they leave for, and one is open at a time - opening
 * one closes the other. The shell holds the host, so a panel stays put across client navigation,
 * and the open panel is remembered per person in this browser.
 *
 * From 1280px the panel docks at 420px on the right and leaves the page usable. Between 768px
 * and 1280px the page could not keep its layout beside it, so the panel is the shared drawer
 * (`ui/drawer`) over the page from the right. Below 768px it is a full-height sheet from the
 * bottom that drags closed by its top edge. Drawer and sheet hold focus until they are closed.
 *
 * A panel opens from the address too: `?panel=assistant` or `?panel=notifications` is consumed
 * once, which is how `/ai` and `/notifications` keep working as links.
 */
export type PanelName = "assistant" | "notifications";

const PANELS: readonly PanelName[] = ["assistant", "notifications"];
/** Parameters that belong to a deep link and are cleared with it (`thread` names a conversation). */
const DEEP_LINK_PARAMS = ["panel", "thread"];
const PHONE = "(max-width: 767px)";
/** Below this the page keeps too little room beside a docked panel (issue #131). */
const NARROW = "(max-width: 1279px)";

interface SidePanelState {
  active: PanelName | null;
  open: (name: PanelName) => void;
  close: () => void;
  toggle: (name: PanelName) => void;
  launcher: (name: PanelName) => RefObject<HTMLButtonElement | null>;
}

const SidePanelContext = createContext<SidePanelState | null>(null);

export function useSidePanel() {
  const state = useContext(SidePanelContext);
  if (!state) throw new Error("useSidePanel needs a SidePanelProvider");
  return state;
}

function storageKey(userId: string) {
  return `mizan.panel.${userId}`;
}

function isPanel(value: unknown): value is PanelName {
  return PANELS.includes(value as PanelName);
}

function remembered(userId: string): PanelName | null {
  try {
    const raw = window.localStorage.getItem(storageKey(userId));
    if (raw) {
      const value = JSON.parse(raw)?.open;
      return isPanel(value) ? value : null;
    }
    // Before notifications became a panel, the assistant remembered its own open state.
    const legacy = window.localStorage.getItem(`mizan.assistant.${userId}`);
    return legacy && JSON.parse(legacy)?.open === true ? "assistant" : null;
  } catch {
    return null;
  }
}

export function usePhone() {
  return useMediaQuery(PHONE);
}

/** True when an open panel covers the page (a drawer or a sheet) instead of docking beside it. */
export function useCoveringPanel() {
  return useMediaQuery(NARROW);
}

export function SidePanelProvider({
  userId,
  children,
}: {
  userId: string;
  children: ReactNode;
}) {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const [active, setActive] = useState<PanelName | null>(null);
  const [restored, setRestored] = useState(false);
  const launchers = useRef<
    Record<PanelName, RefObject<HTMLButtonElement | null>>
  >({
    assistant: { current: null },
    notifications: { current: null },
  });

  useEffect(() => {
    const saved = remembered(userId);
    // A panel that covers the page is not reopened by a reload; a docked panel is.
    // eslint-disable-next-line react-hooks/set-state-in-effect -- browser storage is read after hydration
    if (saved && !window.matchMedia?.(NARROW).matches) setActive(saved);
    setRestored(true);
  }, [userId]);

  useEffect(() => {
    if (!restored) return;
    try {
      window.localStorage.setItem(
        storageKey(userId),
        JSON.stringify({ open: active }),
      );
    } catch {
      // Remembering is a convenience; a browser that refuses still has working panels.
    }
  }, [restored, userId, active]);

  const linked = searchParams.get("panel");
  useEffect(() => {
    if (!restored || !isPanel(linked)) return;
    // eslint-disable-next-line react-hooks/set-state-in-effect -- the address opens the panel once, then is cleared
    setActive(linked);
    const next = new URLSearchParams(searchParams.toString());
    for (const key of DEEP_LINK_PARAMS) next.delete(key);
    router.replace(`${pathname}${next.size ? `?${next}` : ""}`, {
      scroll: false,
    });
  }, [restored, linked, pathname, router, searchParams]);

  const open = useCallback((name: PanelName) => setActive(name), []);
  const close = useCallback(() => setActive(null), []);
  const toggle = useCallback(
    (name: PanelName) =>
      setActive((current) => (current === name ? null : name)),
    [],
  );
  const launcher = useCallback(
    (name: PanelName) => launchers.current[name],
    [],
  );

  const value = useMemo(
    () => ({ active, open, close, toggle, launcher }),
    [active, open, close, toggle, launcher],
  );

  return (
    <SidePanelContext.Provider value={value}>
      {children}
    </SidePanelContext.Provider>
  );
}

export interface SidePanelContent {
  /** A sheet from the bottom of a phone. */
  phone: boolean;
  /** The panel covers the page (a drawer or a sheet), so opening a page should close it. */
  covers: boolean;
  /** Counts each opening, so the content can put focus in itself once it is ready. */
  focusToken: number;
  close: () => void;
}

/**
 * One side panel. Its content is mounted on first open and kept afterwards, so closing does not
 * throw away what it holds (a conversation, a scrolled list). The content renders into one
 * element that moves between the docked column and the drawer, so changing between them, or
 * closing the drawer, keeps it too.
 */
export function SidePanel({
  name,
  id,
  label,
  children,
}: {
  name: PanelName;
  id: string;
  label: string;
  children: (content: SidePanelContent) => ReactNode;
}) {
  const { active, close, launcher } = useSidePanel();
  const open = active === name;
  const phone = usePhone();
  const covers = useCoveringPanel();
  const [mounted, setMounted] = useState(open);
  const [opened, setOpened] = useState(open ? 1 : 0);
  const [seenOpen, setSeenOpen] = useState(open);
  if (open !== seenOpen) {
    setSeenOpen(open);
    if (open) {
      setMounted(true);
      setOpened((count) => count + 1);
    }
  }

  // Created in the browser only; the content is never server-rendered (it mounts on first open).
  const [host] = useState(() => {
    if (typeof document === "undefined") return null;
    const node = document.createElement("div");
    node.className = "flex min-h-0 flex-1 flex-col";
    return node;
  });
  const attach = useCallback(
    (slot: HTMLElement | null) => {
      if (slot && host && host.parentNode !== slot) slot.appendChild(host);
    },
    [host],
  );

  // Closing hands focus back to the launcher, unless another panel took this one's place.
  const wasOpen = useRef(open);
  useEffect(() => {
    if (!open && wasOpen.current && active === null)
      launcher(name).current?.focus();
    wasOpen.current = open;
  }, [open, active, launcher, name]);

  // Docked, Escape inside the panel closes it. The content is portaled into the panel, so its
  // keys reach it through the DOM rather than React; a photo or a confirmation opened from the
  // panel sits outside it and closes itself instead.
  const dock = useCallback(
    (slot: HTMLDivElement | null) => {
      attach(slot);
      if (!slot) return;
      const escape = (event: globalThis.KeyboardEvent) => {
        if (event.key !== "Escape" || event.defaultPrevented) return;
        event.preventDefault();
        close();
      };
      slot.addEventListener("keydown", escape);
      return () => slot.removeEventListener("keydown", escape);
    },
    [attach, close],
  );

  const content =
    mounted && host
      ? createPortal(
          children({ phone, covers, focusToken: opened, close }),
          host,
        )
      : null;

  if (covers)
    return (
      <>
        <Drawer
          open={open}
          onOpenChange={(next) => {
            if (!next) close();
          }}
          direction={phone ? "bottom" : "right"}
          // The content puts focus in itself once it is ready (focusToken).
          autoFocus={false}
        >
          <DrawerContent
            id={id}
            data-side-panel={name}
            aria-modal="true"
            aria-describedby={undefined}
            handle={false}
            // The launcher takes focus back as the panel closes (above).
            onCloseAutoFocus={(event) => event.preventDefault()}
            className={phone ? "h-dvh max-h-none rounded-none border-0" : ""}
          >
            <DrawerTitle className="sr-only">{label}</DrawerTitle>
            {phone && <DrawerDragStrip />}
            <div ref={attach} className="flex min-h-0 flex-1 flex-col" />
          </DrawerContent>
        </Drawer>
        {content}
      </>
    );

  return (
    <>
      <div
        ref={dock}
        id={id}
        data-side-panel={name}
        tabIndex={-1}
        hidden={!open}
        role="complementary"
        aria-label={label}
        // Docked, it fades in a few pixels from the edge it sits on and closes at once, so the
        // page never waits on an empty column.
        className="flex h-full min-w-0 flex-[0_0_420px] flex-col border-l border-border bg-background transition-[opacity,translate] duration-200 ease-out focus:outline-none starting:translate-x-3 starting:opacity-0 [&[hidden]]:hidden"
      />
      {content}
    </>
  );
}

/** The handle of a phone sheet: a short bar that says the sheet came up from below. */
export function SheetHandle() {
  return (
    <div aria-hidden="true" className="flex justify-center pt-2">
      <span className="h-1 w-9 rounded-full bg-border" />
    </div>
  );
}
