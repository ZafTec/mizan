import { notFound } from "next/navigation";
import { serverApi } from "@/lib/api.server";
import type { SocialProfileDto } from "@/types/social";
import FollowRequestButton from "./FollowRequestButton";

export default async function SharePage({ searchParams }: { searchParams: Promise<{ t?: string }> }) {
  const token = (await searchParams).t; if (!token) notFound();
  const profile = await serverApi<SocialProfileDto>(`/api/Social/share/${encodeURIComponent(token)}`, { requireAuth: false });
  return <main className="mx-auto max-w-2xl py-10"><div className="card grid gap-8 p-8 text-center sm:grid-cols-[12rem_1fr] sm:text-left"><img src="/illustrations/hero-social-share.svg" alt="" className="w-full" /><div><p className="eyebrow">Mizan training circle</p><h1 className="mt-3 text-3xl font-semibold">{profile.displayName}</h1><p className="mt-3 text-charcoal-blue-500">This profile shares workouts only with approved followers.</p><div className="mt-6"><FollowRequestButton token={token} /></div></div></div></main>;
}
