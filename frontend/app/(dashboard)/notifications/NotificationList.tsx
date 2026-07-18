"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { clientApi } from "@/lib/api.client";
import { appToast } from "@/lib/toast";
import type { components } from "@/types/api.generated";

export type NotificationDto = components["schemas"]["NotificationDto"];

export default function NotificationList({ initial }: { initial: NotificationDto[] }) {
  const router = useRouter();
  const [items, setItems] = useState(initial);

  async function open(item: NotificationDto) {
    try { if (!item.readAt) await clientApi(`/api/Notifications/${item.id}/read`, { method: "POST" }); setItems((current) => current.map((value) => value.id === item.id ? { ...value, readAt: new Date().toISOString() } : value)); if (item.linkUrl) router.push(item.linkUrl); }
    catch (error) { appToast.error(error, "Could not open notification"); }
  }

  async function readAll() {
    try { await clientApi("/api/Notifications/read-all", { method: "POST" }); setItems((current) => current.map((item) => ({ ...item, readAt: item.readAt ?? new Date().toISOString() }))); }
    catch (error) { appToast.error(error, "Could not mark notifications read"); }
  }

  if (items.length === 0) return <div className="card p-10 text-center"><img src="/illustrations/empty-notifications.svg" alt="" className="mx-auto w-60" /><h2 className="mt-4 text-xl font-semibold">All caught up</h2><p className="mt-1 text-sm text-charcoal-blue-500">New activity will appear here.</p></div>;
  return <div className="space-y-3"><div className="flex justify-end"><button className="btn-ghost btn-sm" onClick={readAll}>Mark all read</button></div>{items.map((item) => <button key={item.id} onClick={() => open(item)} className={`card press-feedback w-full p-4 text-left ${item.readAt ? "" : "border-brand-400 bg-brand-50 dark:border-brand-500/30 dark:bg-brand-950/30"}`}><div className="flex items-start gap-3"><span className="mt-1 size-2 rounded-full bg-brand-600" /><span className="flex-1"><strong className="block">{item.title}</strong>{item.body && <span className="mt-1 block text-sm text-charcoal-blue-500">{item.body}</span>}<span className="mt-2 block text-xs text-charcoal-blue-400">{new Date(item.createdAt).toLocaleString()}</span></span></div></button>)}</div>;
}
