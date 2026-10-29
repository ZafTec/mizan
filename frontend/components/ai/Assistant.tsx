"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
  type RefObject,
} from "react";
import { useSearchParams } from "next/navigation";
import AiChat from "@/components/ai/AiChat";
import { SidePanel, useSidePanel } from "@/components/panels/SidePanel";

/**
 * The assistant is a side panel beside whatever the person is doing, not a page they leave for
 * (#124). The [side panel host](../panels/SidePanel.tsx) decides whether it is open; this keeps
 * the conversation it shows, remembered per person in this browser.
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

export function useAssistant() {
  const state = useContext(AssistantContext);
  if (!state) throw new Error("useAssistant needs an AssistantProvider");
  return state;
}

function storageKey(userId: string) {
  return `mizan.assistant.${userId}`;
}

function rememberedThread(userId: string): string | null {
  try {
    const raw = window.localStorage.getItem(storageKey(userId));
    const value = raw ? JSON.parse(raw) : null;
    return typeof value?.threadId === "string" ? value.threadId : null;
  } catch {
    return null;
  }
}

export function AssistantProvider({
  userId,
  children,
}: {
  userId: string;
  children: ReactNode;
}) {
  const panels = useSidePanel();
  const searchParams = useSearchParams();
  const [threadId, setThreadId] = useState<string | null>(null);
  const [restored, setRestored] = useState(false);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect -- browser storage is read after hydration
    setThreadId(rememberedThread(userId));
    setRestored(true);
  }, [userId]);

  useEffect(() => {
    if (!restored) return;
    try {
      window.localStorage.setItem(
        storageKey(userId),
        JSON.stringify({ threadId }),
      );
    } catch {
      // Remembering is a convenience; a browser that refuses still has a working panel.
    }
  }, [restored, userId, threadId]);

  // `/ai?thread=<id>` arrives as `?panel=assistant&thread=<id>`; the host opens the panel.
  const linkedThread =
    searchParams.get("panel") === "assistant"
      ? searchParams.get("thread")
      : null;
  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect -- the address names the conversation once
    if (restored && linkedThread) setThreadId(linkedThread);
  }, [restored, linkedThread]);

  const { active, open, close, toggle, launcher } = panels;
  const openPanel = useCallback(
    (id?: string | null) => {
      if (id !== undefined) setThreadId(id);
      open("assistant");
    },
    [open],
  );
  const toggleAssistant = useCallback(() => toggle("assistant"), [toggle]);

  const value = useMemo(
    () => ({
      open: active === "assistant",
      threadId,
      openPanel,
      close,
      toggle: toggleAssistant,
      setThreadId,
      launcher: launcher("assistant"),
    }),
    [active, threadId, openPanel, close, toggleAssistant, launcher],
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
      className="inline-flex min-h-10 items-center rounded-md border border-border px-3.5 text-[13px] font-medium text-foreground transition-[background-color] duration-160 ease-[ease-out] hover:bg-muted aria-expanded:bg-muted max-sm:px-3"
    >
      Ask Mizan
    </button>
  );
}

export function AssistantPanel() {
  const { threadId, setThreadId } = useAssistant();
  return (
    <SidePanel name="assistant" id={ASSISTANT_PANEL_ID} label="Mizan assistant">
      {({ phone, focusToken, close }) => (
        <AiChat
          threadId={threadId}
          onThreadChange={setThreadId}
          onClose={close}
          phone={phone}
          focusToken={focusToken}
        />
      )}
    </SidePanel>
  );
}
