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
  ViewTransition,
} from "react";
import { useRouter } from "next/navigation";
import { ChevronDown, ChevronRight, X } from "lucide-react";
import { Icon } from "@/components/ui/icon";
import {
  SheetHandle,
  SidePanel,
  useSidePanel,
  type SidePanelContent,
} from "@/components/panels/SidePanel";
import {
  listNotifications,
  markAllNotificationsRead,
  markNotificationRead,
  unreadNotificationCount,
  type Notification,
} from "@/lib/api/notifications";
import { rowMotion } from "@/components/motion/list";
import { changeList } from "@/components/motion/changeList";
import { appToast } from "@/lib/toast";
import { cn } from "@/lib/utils";

/**
 * Notifications are a side panel beside the page, opened from the bell in the top bar. The unread
 * count is read on load, once a minute and whenever the tab comes back; the open panel reloads
 * its list when that count moves.
 */
export const NOTIFICATIONS_PANEL_ID = "notifications-panel";

interface NotificationsState {
  unread: number;
  setUnread: (count: number | ((current: number) => number)) => void;
  refreshUnread: () => void;
}

const NotificationsContext = createContext<NotificationsState | null>(null);

function useNotifications() {
  const state = useContext(NotificationsContext);
  if (!state) throw new Error("useNotifications needs a NotificationsProvider");
  return state;
}

export function NotificationsProvider({ children }: { children: ReactNode }) {
  const [unread, setUnread] = useState(0);
  const active = useRef(true);

  const refreshUnread = useCallback(() => {
    unreadNotificationCount()
      .then((result) => {
        if (active.current) setUnread(result.unreadCount);
      })
      .catch(() => {});
  }, []);

  useEffect(() => {
    active.current = true;
    refreshUnread();
    const timer = window.setInterval(refreshUnread, 60_000);
    const visible = () => {
      if (document.visibilityState === "visible") refreshUnread();
    };
    document.addEventListener("visibilitychange", visible);
    return () => {
      active.current = false;
      window.clearInterval(timer);
      document.removeEventListener("visibilitychange", visible);
    };
  }, [refreshUnread]);

  const value = useMemo(
    () => ({ unread, setUnread, refreshUnread }),
    [unread, refreshUnread],
  );
  return (
    <NotificationsContext.Provider value={value}>
      {children}
    </NotificationsContext.Provider>
  );
}

export function NotificationBell() {
  const { unread } = useNotifications();
  const { active, toggle, launcher } = useSidePanel();
  const open = active === "notifications";
  return (
    <button
      ref={launcher("notifications")}
      type="button"
      onClick={() => toggle("notifications")}
      aria-expanded={open}
      aria-controls={NOTIFICATIONS_PANEL_ID}
      aria-label={
        unread > 0 ? `Notifications, ${unread} unread` : "Notifications"
      }
      className="relative inline-flex size-10 shrink-0 items-center justify-center rounded-md border border-border text-muted-foreground transition-[background-color,color,scale] duration-160 ease-out hover:bg-muted hover:text-foreground active:scale-[0.985] aria-expanded:bg-muted aria-expanded:text-foreground"
    >
      <Icon name="bell" size={18} aria-hidden="true" />
      {unread > 0 && (
        <span
          aria-hidden="true"
          className="absolute -top-1 -right-1 flex h-5 min-w-5 items-center justify-center rounded-full bg-burnt-peach-700 px-1 text-[10px] font-bold text-white tabular-nums transition-[opacity,scale] duration-160 ease-out starting:scale-90 starting:opacity-0 dark:bg-burnt-peach-400 dark:text-charcoal-blue-950"
        >
          {unread > 9 ? "9+" : unread}
        </span>
      )}
    </button>
  );
}

export function NotificationsPanel() {
  return (
    <SidePanel
      name="notifications"
      id={NOTIFICATIONS_PANEL_ID}
      label="Notifications"
    >
      {(content) => <NotificationsInbox {...content} />}
    </SidePanel>
  );
}

const TIME = {
  day: "numeric",
  month: "short",
  hour: "numeric",
  minute: "2-digit",
} as const;

function NotificationsInbox({ phone, focusToken, close }: SidePanelContent) {
  const router = useRouter();
  const { unread, setUnread } = useNotifications();
  const [items, setItems] = useState<Notification[] | null>(null);
  const [failed, setFailed] = useState(false);
  const root = useRef<HTMLDivElement>(null);
  const listUnread = items?.filter((item) => !item.readAt).length ?? 0;

  /** `arriving`: the list is already showing, so what is new fades in and the rest moves down. */
  const load = useCallback(
    async (arriving = false) => {
      try {
        const result = await listNotifications();
        const apply = () => {
          setItems(result.items);
          setUnread(result.unreadCount);
        };
        if (arriving) changeList(apply);
        else apply();
        setFailed(false);
      } catch {
        setFailed(true);
      }
    },
    [setUnread],
  );

  // Each opening reloads the list, then puts focus on the first notification, or the close button.
  const focusPending = useRef(false);
  useEffect(() => {
    if (!focusToken) return;
    focusPending.current = true;
    void load();
  }, [focusToken, load]);
  useEffect(() => {
    if (!focusPending.current || (items === null && !failed)) return;
    focusPending.current = false;
    const first =
      root.current?.querySelector<HTMLElement>("[data-autofocus]") ??
      root.current?.querySelector<HTMLElement>("[data-close]");
    first?.focus();
  }, [items, failed]);

  // The bell's count moved while the panel was open: something arrived or was read elsewhere.
  useEffect(() => {
    if (items && unread !== listUnread) void load(true);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- only a change in the count reloads
  }, [unread]);

  /** Opens the notification's link, or one of its actions' paths. */
  async function open(item: Notification, path = item.linkUrl) {
    try {
      if (!item.readAt) {
        await markNotificationRead(item.id);
        setItems((current) =>
          (current ?? []).map((value) =>
            value.id === item.id
              ? { ...value, readAt: new Date().toISOString() }
              : value,
          ),
        );
        setUnread((count) => Math.max(0, count - 1));
      }
      if (path) {
        // The sheet covers the page it would open, so it steps aside; a docked panel stays.
        if (phone) close();
        router.push(path);
      }
    } catch (error) {
      appToast.error(error, "Could not open notification");
    }
  }

  async function readAll() {
    try {
      await markAllNotificationsRead();
      const now = new Date().toISOString();
      setItems((current) =>
        (current ?? []).map((item) => ({
          ...item,
          readAt: item.readAt ?? now,
        })),
      );
      setUnread(0);
    } catch (error) {
      appToast.error(error, "Could not mark notifications read");
    }
  }

  return (
    <div ref={root} className="flex h-full min-h-0 flex-col">
      {phone && <SheetHandle />}
      <header className="flex items-center gap-1 border-b border-border py-2 pr-2 pl-4">
        <div className="min-w-0 flex-1">
          <h2
            id="notifications-title"
            className="text-base leading-tight font-semibold"
          >
            Notifications
          </h2>
          <p
            className="truncate text-xs text-muted-foreground"
            aria-live="polite"
          >
            {items === null
              ? " "
              : listUnread === 0
                ? "All read"
                : `${listUnread} unread`}
          </p>
        </div>
        {listUnread > 0 && (
          <button
            type="button"
            className="text-link px-2 text-sm"
            onClick={readAll}
          >
            Mark all read
          </button>
        )}
        <button
          type="button"
          data-close
          onClick={close}
          aria-label="Close notifications"
          title="Close notifications"
          className="flex h-11 w-11 shrink-0 items-center justify-center rounded-sm hover:bg-muted"
        >
          {phone ? (
            <ChevronDown aria-hidden="true" size={20} strokeWidth={1.7} />
          ) : (
            <X aria-hidden="true" size={19} strokeWidth={1.7} />
          )}
        </button>
      </header>
      <div className="min-h-0 flex-1 overflow-y-auto overscroll-contain">
        {failed && items === null ? (
          <p className="px-4 py-6 text-sm text-muted-foreground">
            Notifications could not be loaded.{" "}
            <button
              type="button"
              className="text-link text-sm"
              onClick={() => void load()}
            >
              Try again
            </button>
          </p>
        ) : items === null ? null : items.length === 0 ? (
          <p className="px-4 py-6 text-sm text-muted-foreground">
            Achievements, coach messages and household invites will land here.
          </p>
        ) : (
          <ul aria-labelledby="notifications-title">
            {items.map((item, index) => (
              <ViewTransition key={item.id} {...rowMotion}>
                <li className="border-b border-border">
                  <button
                    type="button"
                    data-autofocus={index === 0 ? "" : undefined}
                    onClick={() => open(item)}
                    className="flex min-h-14 w-full items-start gap-3 px-4 py-3 text-left transition-colors duration-160 ease-out hover:bg-muted"
                  >
                    <span
                      aria-hidden="true"
                      className={cn(
                        "mt-1.5 size-2 shrink-0 rounded-full bg-chart-1 transition-opacity duration-200 ease-out",
                        item.readAt && "opacity-0",
                      )}
                    />
                    {item.imageUrl && (
                      // A badge from the app or the media host: a plain image, not an optimised one.
                      // eslint-disable-next-line @next/next/no-img-element
                      <img
                        src={item.imageUrl}
                        alt=""
                        width={40}
                        height={40}
                        loading="lazy"
                        className="size-10 shrink-0 rounded-md object-cover"
                      />
                    )}
                    <span className="min-w-0 flex-1">
                      <span
                        className={cn(
                          "block text-sm",
                          !item.readAt && "font-semibold",
                        )}
                      >
                        {item.title}
                        {!item.readAt && (
                          <span className="sr-only"> (unread)</span>
                        )}
                      </span>
                      {item.body && (
                        <span className="mt-0.5 block text-sm text-muted-foreground">
                          {item.body}
                        </span>
                      )}
                      {item.facts.length > 0 && (
                        <span className="mt-1 flex flex-wrap gap-x-3 gap-y-0.5 text-xs text-muted-foreground">
                          {item.facts.map((fact) => (
                            <span key={fact.label}>
                              {fact.label}{" "}
                              <span className="font-medium text-foreground tabular-nums">
                                {fact.value}
                              </span>
                            </span>
                          ))}
                        </span>
                      )}
                      {/* The browser's zone differs from the server's; the first paint is corrected on the client. */}
                      <time
                        dateTime={item.createdAt}
                        suppressHydrationWarning
                        className="mt-1 block text-xs text-muted-foreground tabular-nums"
                      >
                        {new Date(item.createdAt).toLocaleString(
                          undefined,
                          TIME,
                        )}
                      </time>
                    </span>
                    {item.linkUrl && item.actions.length === 0 && (
                      <ChevronRight
                        aria-hidden="true"
                        size={17}
                        strokeWidth={1.7}
                        className="mt-0.5 shrink-0 text-muted-foreground"
                      />
                    )}
                  </button>
                  {item.actions.length > 0 && (
                    // Beside the row rather than inside it: a button cannot hold buttons.
                    <div className="-mt-1 flex flex-wrap gap-2 pr-4 pb-3 pl-9">
                      {item.actions.map((action) => (
                        <button
                          key={action.path}
                          type="button"
                          onClick={() => open(item, action.path)}
                          className="inline-flex min-h-9 items-center rounded-md border border-border px-3 text-sm font-medium transition-[background-color,scale] duration-160 ease-out hover:bg-muted active:scale-[0.985]"
                        >
                          {action.label}
                        </button>
                      ))}
                    </div>
                  )}
                </li>
              </ViewTransition>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}
