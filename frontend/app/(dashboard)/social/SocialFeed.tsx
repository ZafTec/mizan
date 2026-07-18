"use client";
import { useState } from "react";
import { clientApi } from "@/lib/api.client";
import { appToast } from "@/lib/toast";
import type { FeedItemDto } from "@/types/social";

const EMOJIS = ["👍", "❤️", "💪", "🔥", "👏", "🎉", "🏆"];

export default function SocialFeed({ initialItems, currentUserId }: { initialItems: FeedItemDto[]; currentUserId: string }) {
  const [items, setItems] = useState(initialItems);
  const [drafts, setDrafts] = useState<Record<string, string>>({});
  async function react(id: string, emoji: string) {
    try { const result = await clientApi<{ id: string }>(`/api/Social/feed/${id}/reactions`, { method: "POST", body: { emoji } }); setItems((all) => all.map((item) => item.id === id && !item.reactions.some((r) => r.userId === currentUserId && r.emoji === emoji) ? { ...item, reactions: [...item.reactions, { id: result.id, userId: currentUserId, emoji }] } : item)); }
    catch (error) { appToast.error(error, "Could not react"); }
  }
  async function comment(id: string) {
    const body = drafts[id]?.trim(); if (!body) return;
    try { const result = await clientApi<{ id: string }>(`/api/Social/feed/${id}/comments`, { method: "POST", body: { body } }); setItems((all) => all.map((item) => item.id === id ? { ...item, comments: [...item.comments, { id: result.id, userId: currentUserId, displayName: "You", body, createdAt: new Date().toISOString() }] } : item)); setDrafts((all) => ({ ...all, [id]: "" })); }
    catch (error) { appToast.error(error, "Could not comment"); }
  }
  if (!items.length) return <EmptyFeed />;
  return <div className="mx-auto max-w-3xl space-y-4">{items.map((item) => <article key={item.id} className="card">
    <header className="flex items-center gap-3 p-5"><span className="grid h-11 w-11 place-items-center rounded-2xl bg-brand-600 font-bold text-white">{item.displayName[0]?.toUpperCase()}</span><span><strong>{item.displayName}</strong><small className="block text-charcoal-blue-500">{new Date(item.createdAt).toLocaleString()}</small></span></header>
    {item.workout && <section className="border-y border-charcoal-blue-200 p-5 dark:border-white/10"><p className="eyebrow">Workout complete</p><h2 className="mt-2 text-2xl font-semibold">{item.workout.name}</h2><p className="mt-1 text-sm text-charcoal-blue-500">{item.workout.exercises.length} exercises · {Math.round(item.workout.totalVolumeKg).toLocaleString()} kg</p><div className="mt-4 flex flex-wrap gap-2">{item.workout.exercises.map((exercise) => <span key={exercise.name} className="rounded-xl bg-charcoal-blue-100 px-3 py-2 text-xs dark:bg-charcoal-blue-900">{exercise.name} · {exercise.topWeightKg} kg × {exercise.topReps}</span>)}</div></section>}
    <div className="space-y-4 p-5">{item.caption && <p>{item.caption}</p>}<div className="flex flex-wrap gap-2">{EMOJIS.map((emoji) => <button key={emoji} className="press-feedback rounded-xl border border-charcoal-blue-200 px-2 py-1 dark:border-white/10" onClick={() => react(item.id, emoji)}>{emoji} {item.reactions.filter((r) => r.emoji === emoji).length || ""}</button>)}</div>{item.comments.map((entry) => <p key={entry.id} className="rounded-2xl bg-charcoal-blue-50 p-3 text-sm dark:bg-charcoal-blue-900"><strong>{entry.displayName}</strong> {entry.body}</p>)}<div className="flex gap-2"><input className="input !py-2" value={drafts[item.id] ?? ""} onChange={(event) => setDrafts((all) => ({ ...all, [item.id]: event.target.value }))} placeholder="Write a comment" maxLength={500} /><button className="btn-primary btn-sm" onClick={() => comment(item.id)}>Post</button></div></div>
  </article>)}</div>;
}

function EmptyFeed() { return <div className="card p-10 text-center"><img src="/illustrations/empty-feed.svg" alt="" className="mx-auto w-60" /><h2 className="mt-4 text-xl font-semibold">Your feed is quiet</h2><p className="mt-1 text-sm text-charcoal-blue-500">Share your profile link or publish a workout.</p></div>; }
