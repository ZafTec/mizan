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
import AiChat from "@/components/ai/AiChat";
import { cn } from "@/lib/utils";

/**
 * The assistant is a panel beside whatever the person is doing, not a page they leave for (#124).
 * The shell holds it, so the panel and its conversation stay put across client navigation; the
 * open state and the conversation are remembered per person in this browser.
 *
 * Wide screens dock it on the right and leave the page usable. Below 768px it is a full-height
 * sheet that holds focus until it is closed.
 */
interface AssistantState {
  open: boolean;
  threadId: string | null;
  openPanel: (threadId?: string | null) => void;
  close: () => void;
  toggle: () => void;
  setThreadId: (id: string | null) => void;
  launcher: RefObject<HTMLButtonElement | null>;
}

const AssistantContext = createContext<AssistantState | null>(null);

export const ASSISTANT_PANEL_ID = "assistant-panel";
const PHONE = "(max-width: 767px)";

export function useAssistant() {
  const state = useContext(AssistantContext);
  if (!state) throw new Error("useAssistant needs an AssistantProvider");
  return state;
}

function storageKey(userId: string) {
  return `mizan.assistant.${userId}`;
}

function remembered(userId: string): {
  open: boolean;
  threadId: string | null;
} {
  try {
    const raw = window.localStorage.getItem(storageKey(userId));
    const value = raw ? JSON.parse(raw) : null;
    return {
      open: value?.open === true,
      threadId: typeof value?.threadId === "string" ? value.threadId : null,
    };
  } catch {
    return { open: false, threadId: null };
  }
}

function subscribePhone(changed: () => void) {
  const query = window.matchMedia?.(PHONE);
  query?.addEventListener("change", changed);
  return () => query?.removeEventListener("change", changed);
}

function usePhone() {
  return useSyncExternalStore(
    subscribePhone,
    () => window.matchMedia?.(PHONE).matches ?? false,
    () => false,
  );
}

export function AssistantProvider({
  userId,
  children,
}: {
  userId: string;
  children: ReactNode;
}) {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const [open, setOpen] = useState(false);
  const [threadId, setThreadId] = useState<string | null>(null);
  const [restored, setRestored] = useState(false);
  const launcher = useRef<HTMLButtonElement | null>(null);

  useEffect(() => {
    const saved = remembered(userId);
    // eslint-disable-next-line react-hooks/set-state-in-effect -- browser storage is read after hydration
    setThreadId(saved.threadId);
    // A sheet that covers the screen is not reopened by a reload; a docked panel is.
    if (saved.open && !window.matchMedia?.(PHONE).matches) setOpen(true);
    setRestored(true);
  }, [userId]);

  useEffect(() => {
    if (!restored) return;
    try {
      window.localStorage.setItem(
        storageKey(userId),
        JSON.stringify({ open, threadId }),
      );
    } catch {
      // Remembering is a convenience; a browser that refuses still has a working panel.
    }
  }, [restored, userId, open, threadId]);

  // `/ai` and `/ai?thread=<id>` arrive here as `?assistant=open`, which is consumed once.
  const deepLink = searchParams.get("assistant");
  const linkedThread = searchParams.get("thread");
  useEffect(() => {
    if (!restored || deepLink !== "open") return;
    // eslint-disable-next-line react-hooks/set-state-in-effect -- the address opens the panel once, then is cleared
    if (linkedThread) setThreadId(linkedThread);
    setOpen(true);
    const next = new URLSearchParams(searchParams.toString());
    next.delete("assistant");
    next.delete("thread");
    router.replace(`${pathname}${next.size ? `?${next}` : ""}`, {
      scroll: false,
    });
  }, [restored, deepLink, linkedThread, pathname, router, searchParams]);

  const openPanel = useCallback((id?: string | null) => {
    if (id !== undefined) setThreadId(id);
    setOpen(true);
  }, []);
  const close = useCallback(() => setOpen(false), []);
  const toggle = useCallback(() => setOpen((current) => !current), []);

  const value = useMemo(
    () => ({ open, threadId, openPanel, close, toggle, setThreadId, launcher }),
    [open, threadId, openPanel, close, toggle],
  );

  return (
    <AssistantContext.Provider value={value}>
      {children}
    </AssistantContext.Provider>
  );
}

export function AssistantLauncher() {
  const { open, toggle, launcher } = useAssistant();
  return (
    <button
      ref={launcher}
      type="button"
      onClick={toggle}
      aria-expanded={open}
      aria-controls={ASSISTANT_PANEL_ID}
      className="assistant-launcher"
    >
      Ask Mizan
    </button>
  );
}

const FOCUSABLE =
  'a[href], button:not([disabled]), textarea:not([disabled]), input:not([disabled]):not([type="hidden"]), select:not([disabled]), [tabindex]:not([tabindex="-1"])';

export function AssistantPanel() {
  const { open, close, threadId, setThreadId, launcher } = useAssistant();
  const phone = usePhone();
  const panel = useRef<HTMLDivElement>(null);
  // Mounted on first open and kept afterwards, so closing does not throw the conversation away.
  // Each opening is counted, and the conversation puts focus in itself once it has loaded.
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

  // Closing hands focus back to the launcher.
  const wasOpen = useRef(open);
  useEffect(() => {
    if (!open && wasOpen.current) launcher.current?.focus();
    wasOpen.current = open;
  }, [open, launcher]);

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
          className="assistant-backdrop"
          onClick={close}
        />
      )}
      <div
        ref={panel}
        id={ASSISTANT_PANEL_ID}
        tabIndex={-1}
        hidden={!open}
        role={phone ? "dialog" : "complementary"}
        aria-modal={phone ? true : undefined}
        aria-label="Mizan assistant"
        onKeyDown={onKeyDown}
        className={cn("assistant-panel", phone && "assistant-sheet")}
      >
        {mounted && (
          <AiChat
            threadId={threadId}
            onThreadChange={setThreadId}
            onClose={close}
            phone={phone}
            focusToken={opened}
          />
        )}
      </div>
    </>
  );
}
