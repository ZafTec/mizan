"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { clientApi } from "@/lib/api.client";
import { AnimatedIcon } from "@/components/ui/animated-icon";

export function NotificationBell() {
  const [count, setCount] = useState(0);
  useEffect(() => {
    let active = true;
    const load = () => clientApi<{ unreadCount: number }>("/api/Notifications/unread-count").then((result) => { if (active) setCount(result.unreadCount); }).catch(() => {});
    load();
    const timer = window.setInterval(load, 60_000);
    const visible = () => { if (document.visibilityState === "visible") load(); };
    document.addEventListener("visibilitychange", visible);
    return () => { active = false; window.clearInterval(timer); document.removeEventListener("visibilitychange", visible); };
  }, []);
  return <Link href="/notifications" className="press-feedback relative flex h-10 w-10 items-center justify-center rounded-2xl border border-charcoal-blue-200 text-charcoal-blue-600 hover:text-charcoal-blue-900 dark:border-white/10 dark:text-charcoal-blue-200" aria-label={count ? `${count} unread notifications` : "Notifications"}><AnimatedIcon name="bell" size={18} />{count > 0 && <span className="absolute -right-1 -top-1 flex h-5 min-w-5 items-center justify-center rounded-full bg-burnt-peach-600 px-1 text-[10px] font-bold text-white">{count > 9 ? "9+" : count}</span>}</Link>;
}
