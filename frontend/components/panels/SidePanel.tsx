"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  useSyncExternalStore,
  type KeyboardEvent,
  type ReactNode,
  type RefObject,
} from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { cn } from "@/lib/utils";

/**
 * The app shell's side panels: the assistant and the notifications. Each is a panel beside
 * whatever the person is doing, not a page they leave for, and one is open at a time - opening
 * one closes the other. The shell holds the host, so a panel stays put across client navigation,
 * and the open panel is remembered per person in this browser.
 *
 * Wide screens dock the panel at 420px on the right and leave the page usable. Below 768px it is
 * a full-height sheet that holds focus until it is closed.
 *
 * A panel opens from the address too: `?panel=assistant` or `?panel=notifications` is consumed
 * once, which is how `/ai` and `/notifications` keep working as links.
 */
export type PanelName = "assistant" | "notifications";

const PANELS: readonly PanelName[] = ["assistant", "notifications"];
/** Parameters that belong to a deep link and are cleared with it (`thread` names a conversation). */
const DEEP_LINK_PARAMS = ["panel", "thread"];
const PHONE = "(max-width: 767px)";

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

function subscribePhone(changed: () => void) {
  const query = window.matchMedia?.(PHONE);
  query?.addEventListener("change", changed);
  return () => query?.removeEventListener("change", changed);
}

export function usePhone() {
  return useSyncExternalStore(
    subscribePhone,
    () => window.matchMedia?.(PHONE).matches ?? false,
    () => false,
  );
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
    // A sheet that covers the screen is not reopened by a reload; a docked panel is.
    // eslint-disable-next-line react-hooks/set-state-in-effect -- browser storage is read after hydration
    if (saved && !window.matchMedia?.(PHONE).matches) setActive(saved);
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

const FOCUSABLE =
  'a[href], button:not([disabled]), textarea:not([disabled]), input:not([disabled]):not([type="hidden"]), select:not([disabled]), [tabindex]:not([tabindex="-1"])';

export interface SidePanelContent {
  phone: boolean;
  /** Counts each opening, so the content can put focus in itself once it is ready. */
  focusToken: number;
  close: () => void;
}

/**
 * One side panel. Its content is mounted on first open and kept afterwards, so closing does not
 * throw away what it holds (a conversation, a scrolled list).
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
  const panel = useRef<HTMLDivElement>(null);
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

  // Closing hands focus back to the launcher, unless another panel took this one's place.
  const wasOpen = useRef(open);
  useEffect(() => {
    if (!open && wasOpen.current && active === null)
      launcher(name).current?.focus();
    wasOpen.current = open;
  }, [open, active, launcher, name]);

  // The phone sheet is modal: what is behind it cannot be reached until it closes.
  useEffect(() => {
    const root = panel.current;
    const shell = root?.parentElement;
    if (!open || !phone || !shell) return;
    const behind = Array.from(shell.children).filter(
      (child): child is HTMLElement =>
        child !== root && child instanceof HTMLElement && !child.inert,
    );
    for (const element of behind) element.inert = true;
    return () => {
      for (const element of behind) element.inert = false;
    };
  }, [open, phone]);

  // A sheet whose focused control just went away (a list row it replaced) still closes on Escape.
  useEffect(() => {
    if (!open || !phone) return;
    function escape(event: globalThis.KeyboardEvent) {
      if (event.key === "Escape" && document.activeElement === document.body)
        close();
    }
    document.addEventListener("keydown", escape);
    return () => document.removeEventListener("keydown", escape);
  }, [open, phone, close]);

  function onKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    // A photo or a confirmation opened from the panel is portaled out of it, but its keys still
    // bubble here through React. Those close themselves, not the panel.
    if (!panel.current?.contains(event.target as Node)) return;
    if (event.key === "Escape" && !event.defaultPrevented) {
      event.preventDefault();
      close();
      return;
    }
    if (event.key !== "Tab" || !phone) return;
    const items = Array.from(
      panel.current?.querySelectorAll<HTMLElement>(FOCUSABLE) ?? [],
    ).filter((item) => item.offsetParent !== null);
    if (items.length === 0) return;
    const first = items[0];
    const last = items[items.length - 1];
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  }

  return (
    <>
      {open && phone && (
        <div
          aria-hidden="true"
          className="fixed inset-0 z-[59] animate-overlay-in bg-charcoal-blue-950/45"
          onClick={close}
        />
      )}
      <div
        ref={panel}
        id={id}
        data-side-panel={name}
        tabIndex={-1}
        hidden={!open}
        role={phone ? "dialog" : "complementary"}
        aria-modal={phone ? true : undefined}
        aria-label={label}
        onKeyDown={onKeyDown}
        className={cn(
          "flex h-full min-w-0 flex-[0_0_420px] flex-col border-l border-border bg-background focus:outline-none [&[hidden]]:hidden",
          // Docked, it fades in a few pixels from the edge it sits on and
          // closes at once, so the page never waits on an empty column.
          !phone &&
            "transition-[opacity,translate] duration-200 ease-out starting:translate-x-3 starting:opacity-0",
          // On a phone it is a sheet: up from the bottom, and back down.
          phone &&
            "fixed inset-0 z-60 h-dvh border-l-0 transition-[translate,display] transition-discrete duration-280 ease-(--ease-drawer) starting:translate-y-full [&[hidden]]:translate-y-full [&[hidden]]:duration-200 [&[hidden]]:ease-out",
        )}
      >
        {mounted && children({ phone, focusToken: opened, close })}
      </div>
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
