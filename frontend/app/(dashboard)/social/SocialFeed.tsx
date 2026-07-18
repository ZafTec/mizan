"use client";

import Image from "next/image";
import { useState } from "react";
import { clientApi } from "@/lib/api.client";
import { appToast } from "@/lib/toast";
import type { FeedItemDto } from "@/types/social";

const EMOJIS = ["👍", "❤️", "💪", "🔥", "👏", "🎉", "🏆"];
const PAGE_SIZE = 30;

type FeedResult = {
  items: FeedItemDto[];
  totalCount: number;
  page: number;
  pageSize: number;
};

export default function SocialFeed({
  initialItems,
  initialTotalCount,
  currentUserId,
  currentDisplayName,
}: {
  initialItems: FeedItemDto[];
  initialTotalCount: number;
  currentUserId: string;
  currentDisplayName: string;
}) {
  const [items, setItems] = useState(initialItems);
  const [totalCount, setTotalCount] = useState(initialTotalCount);
  const [page, setPage] = useState(1);
  const [loadingMore, setLoadingMore] = useState(false);
  const [drafts, setDrafts] = useState<Record<string, string>>({});

  async function toggleReaction(id: string, emoji: string) {
    const item = items.find((candidate) => candidate.id === id);
    const existing = item?.reactions.find(
      (reaction) =>
        reaction.userId === currentUserId && reaction.emoji === emoji,
    );
    try {
      if (existing) {
        await clientApi(
          `/api/Social/feed/${id}/reactions?emoji=${encodeURIComponent(emoji)}`,
          { method: "DELETE" },
        );
        setItems((all) =>
          all.map((candidate) =>
            candidate.id === id
              ? {
                  ...candidate,
                  reactions: candidate.reactions.filter(
                    (reaction) => reaction.id !== existing.id,
                  ),
                }
              : candidate,
          ),
        );
        return;
      }

      const result = await clientApi<{ id: string }>(
        `/api/Social/feed/${id}/reactions`,
        { method: "POST", body: { emoji } },
      );
      setItems((all) =>
        all.map((candidate) =>
          candidate.id === id
            ? {
                ...candidate,
                reactions: [
                  ...candidate.reactions,
                  { id: result.id, userId: currentUserId, emoji },
                ],
              }
            : candidate,
        ),
      );
    } catch (error) {
      appToast.error(error, "Could not update reaction");
    }
  }

  async function comment(id: string) {
    const body = drafts[id]?.trim();
    if (!body) return;
    try {
      const result = await clientApi<{ id: string }>(
        `/api/Social/feed/${id}/comments`,
        { method: "POST", body: { body } },
      );
      setItems((all) =>
        all.map((item) =>
          item.id === id
            ? {
                ...item,
                comments: [
                  ...item.comments,
                  {
                    id: result.id,
                    userId: currentUserId,
                    displayName: currentDisplayName,
                    body,
                    createdAt: new Date().toISOString(),
                  },
                ],
              }
            : item,
        ),
      );
      setDrafts((all) => ({ ...all, [id]: "" }));
    } catch (error) {
      appToast.error(error, "Could not comment");
    }
  }

  async function loadMore() {
    setLoadingMore(true);
    try {
      const nextPage = page + 1;
      const result = await clientApi<FeedResult>(
        `/api/Social/feed?page=${nextPage}&pageSize=${PAGE_SIZE}`,
      );
      setItems((current) => {
        const knownIds = new Set(current.map((item) => item.id));
        return [
          ...current,
          ...result.items.filter((item) => !knownIds.has(item.id)),
        ];
      });
      setPage(result.page);
      setTotalCount(result.totalCount);
    } catch (error) {
      appToast.error(error, "Could not load more posts");
    } finally {
      setLoadingMore(false);
    }
  }

  if (!items.length) return <EmptyFeed />;

  return (
    <div className="mx-auto max-w-3xl space-y-4">
      {items.map((item) => (
        <article key={item.id} className="card">
          <header className="flex items-center gap-3 p-5">
            <span className="grid h-11 w-11 place-items-center rounded-2xl bg-brand-600 font-bold text-white">
              {item.displayName[0]?.toUpperCase()}
            </span>
            <span>
              <strong>{item.displayName}</strong>
              <small className="block text-charcoal-blue-500">
                {new Date(item.createdAt).toLocaleString()}
              </small>
            </span>
          </header>
          {item.workout && (
            <section className="border-y border-charcoal-blue-200 p-5 dark:border-white/10">
              <p className="eyebrow">Workout complete</p>
              <h2 className="mt-2 text-2xl font-semibold">
                {item.workout.name}
              </h2>
              <p className="mt-1 text-sm text-charcoal-blue-500">
                {item.workout.exercises.length} exercises ·{" "}
                {Math.round(item.workout.totalVolumeKg).toLocaleString()} kg
              </p>
              <div className="mt-4 flex flex-wrap gap-2">
                {item.workout.exercises.map((exercise) => (
                  <span
                    key={exercise.name}
                    className="rounded-xl bg-charcoal-blue-100 px-3 py-2 text-xs dark:bg-charcoal-blue-900"
                  >
                    {exercise.name} · {exercise.topWeightKg} kg ×{" "}
                    {exercise.topReps}
                  </span>
                ))}
              </div>
            </section>
          )}
          <div className="space-y-4 p-5">
            {item.caption && <p>{item.caption}</p>}
            <div className="flex flex-wrap gap-2">
              {EMOJIS.map((emoji) => {
                const selected = item.reactions.some(
                  (reaction) =>
                    reaction.userId === currentUserId &&
                    reaction.emoji === emoji,
                );
                return (
                  <button
                    key={emoji}
                    aria-pressed={selected}
                    className={`press-feedback rounded-xl border px-2 py-1 ${selected ? "border-brand-500 bg-brand-50 text-brand-800 dark:bg-brand-950/40 dark:text-brand-200" : "border-charcoal-blue-200 dark:border-white/10"}`}
                    onClick={() => toggleReaction(item.id, emoji)}
                  >
                    {emoji}{" "}
                    {item.reactions.filter(
                      (reaction) => reaction.emoji === emoji,
                    ).length || ""}
                  </button>
                );
              })}
            </div>
            {item.comments.map((entry) => (
              <p
                key={entry.id}
                className="rounded-2xl bg-charcoal-blue-50 p-3 text-sm dark:bg-charcoal-blue-900"
              >
                <strong>{entry.displayName}</strong> {entry.body}
              </p>
            ))}
            <div className="flex gap-2">
              <input
                className="input !py-2"
                value={drafts[item.id] ?? ""}
                onChange={(event) =>
                  setDrafts((all) => ({
                    ...all,
                    [item.id]: event.target.value,
                  }))
                }
                placeholder="Write a comment"
                maxLength={500}
              />
              <button
                className="btn-primary btn-sm"
                disabled={!drafts[item.id]?.trim()}
                onClick={() => comment(item.id)}
              >
                Post
              </button>
            </div>
          </div>
        </article>
      ))}
      {items.length < totalCount && (
        <button
          className="btn-secondary w-full"
          disabled={loadingMore}
          onClick={loadMore}
        >
          {loadingMore
            ? "Loading…"
            : `Load more · ${totalCount - items.length} remaining`}
        </button>
      )}
    </div>
  );
}

function EmptyFeed() {
  return (
    <div className="card p-10 text-center">
      <Image
        src="/illustrations/empty-feed.svg"
        alt=""
        width={240}
        height={190}
        className="mx-auto h-auto w-60"
      />
      <h2 className="mt-4 text-xl font-semibold">Your feed is quiet</h2>
      <p className="mt-1 text-sm text-charcoal-blue-500">
        Share your profile link or publish a workout.
      </p>
    </div>
  );
}
