"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { ChevronRight } from "lucide-react";
import { clientApi } from "@/lib/api.client";
import { appToast } from "@/lib/toast";
import type { components } from "@/types/api.generated";

export type NotificationDto = components["schemas"]["NotificationDto"];

export default function NotificationList({
  initial,
}: {
  initial: NotificationDto[];
}) {
  const router = useRouter();
  const [items, setItems] = useState(initial);
  const unread = items.filter((item) => !item.readAt).length;

  async function open(item: NotificationDto) {
    try {
      if (!item.readAt)
        await clientApi(`/api/Notifications/${item.id}/read`, {
          method: "POST",
        });
      setItems((current) =>
        current.map((value) =>
          value.id === item.id
            ? { ...value, readAt: new Date().toISOString() }
            : value,
        ),
      );
      if (item.linkUrl) router.push(item.linkUrl);
    } catch (error) {
      appToast.error(error, "Could not open notification");
    }
  }

  async function readAll() {
    try {
      await clientApi("/api/Notifications/read-all", { method: "POST" });
      setItems((current) =>
        current.map((item) => ({
          ...item,
          readAt: item.readAt ?? new Date().toISOString(),
        })),
      );
    } catch (error) {
      appToast.error(error, "Could not mark notifications read");
    }
  }

  return (
    <section aria-labelledby="inbox-title">
      <header className="log-section-header">
        <h2 id="inbox-title">
          {unread === 0 ? "All read" : `${unread} unread`}
        </h2>
        {unread > 0 && (
          <button type="button" className="text-link" onClick={readAll}>
            Mark all read
          </button>
        )}
      </header>
      {items.length === 0 ? (
        <div className="log-empty">
          <p>Nothing yet.</p>
          <p className="text-muted-foreground mt-2 text-sm">
            Achievements, coach messages and household invites land here.
          </p>
        </div>
      ) : (
        <ul>
          {items.map((item) => (
            <li key={item.id}>
              <button
                type="button"
                onClick={() => open(item)}
                className="ledger-row w-full text-left transition-colors hover:bg-muted"
              >
                <span
                  aria-hidden="true"
                  className={`h-2 w-2 shrink-0 rounded-full ${item.readAt ? "bg-transparent" : "bg-brand-700 dark:bg-brand-300"}`}
                />
                <span className="min-w-0 flex-1">
                  <span
                    className={`block ${item.readAt ? "" : "font-semibold"}`}
                  >
                    {item.title}
                    {!item.readAt && <span className="sr-only"> (unread)</span>}
                  </span>
                  {item.body && (
                    <span className="mt-0.5 block text-sm text-muted-foreground">
                      {item.body}
                    </span>
                  )}
                </span>
                {/* The browser's zone differs from the server's; the first paint is corrected on the client. */}
                <time
                  dateTime={item.createdAt}
                  suppressHydrationWarning
                  className="tabular-nums shrink-0 text-xs text-muted-foreground"
                >
                  {new Date(item.createdAt).toLocaleString(undefined, {
                    day: "numeric",
                    month: "short",
                    hour: "numeric",
                    minute: "2-digit",
                  })}
                </time>
                {item.linkUrl && (
                  <ChevronRight
                    aria-hidden="true"
                    size={17}
                    strokeWidth={1.7}
                    className="text-muted-foreground shrink-0"
                  />
                )}
              </button>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
