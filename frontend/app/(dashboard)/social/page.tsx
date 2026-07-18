import Link from "next/link";
import { ApiError } from "@/lib/api";
import { serverApi } from "@/lib/api.server";
import type { FeedItemDto, SocialProfileDto } from "@/types/social";
import SocialFeed from "./SocialFeed";

export const dynamic = "force-dynamic";

export default async function SocialPage() {
  let profile: SocialProfileDto | null = null;
  try { profile = await serverApi<SocialProfileDto>("/api/Social/profile"); }
  catch (error) { if (!(error instanceof ApiError) || error.status !== 404) throw error; }
  if (!profile) return <div className="card grid items-center gap-8 p-8 md:grid-cols-2"><img src="/illustrations/hero-social-share.svg" alt="" className="mx-auto w-full max-w-md" /><div><p className="eyebrow">Private by default</p><h1 className="mt-3 text-3xl font-semibold">Share training with people you approve</h1><p className="mt-3 text-charcoal-blue-500">Create a profile, share a private link, and approve every follower before they can see a workout.</p><Link href="/social/profile" className="btn-primary mt-6">Create social profile</Link></div></div>;
  const feed = await serverApi<{ items: FeedItemDto[] }>("/api/Social/feed?page=1&pageSize=30");
  return <div className="space-y-6"><header className="flex items-end justify-between"><div><p className="eyebrow">Training circle</p><h1 className="mt-2 text-3xl font-semibold sm:text-4xl">Feed</h1></div><Link href="/social/profile" className="btn-secondary">Profile and followers</Link></header><SocialFeed initialItems={feed.items} currentUserId={profile.userId} /></div>;
}
