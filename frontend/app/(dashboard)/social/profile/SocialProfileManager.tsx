"use client";
import Image from "next/image";
import { useEffect, useState } from "react";
import { clientApi } from "@/lib/api.client";
import { appToast } from "@/lib/toast";
import type { FollowDto, SocialProfileDto } from "@/types/social";

export default function SocialProfileManager() {
  const [profile, setProfile] = useState<SocialProfileDto | null>(null);
  const [requests, setRequests] = useState<FollowDto[]>([]);
  const [name, setName] = useState("");
  const [bio, setBio] = useState("");
  const [publish, setPublish] = useState(false);
  useEffect(() => {
    clientApi<SocialProfileDto>("/api/Social/profile", {
      expectedStatuses: [404],
    })
      .then((p) => {
        setProfile(p);
        setName(p.displayName);
        setBio(p.bio ?? "");
        setPublish(p.defaultPublishWorkouts);
      })
      .catch(() => {});
    clientApi<FollowDto[]>("/api/Social/follows?direction=in&status=Pending")
      .then(setRequests)
      .catch(() => {});
  }, []);
  async function save() {
    try {
      await clientApi("/api/Social/profile", {
        method: "POST",
        body: {
          displayName: name,
          bio: bio || null,
          defaultPublishWorkouts: publish,
        },
      });
      setProfile(await clientApi("/api/Social/profile"));
      appToast.success("Profile saved");
    } catch (error) {
      appToast.error(error, "Could not save profile");
    }
  }
  async function respond(id: string, accept: boolean) {
    await clientApi(`/api/Social/follows/${id}/respond`, {
      method: "POST",
      body: { accept },
    });
    setRequests((all) => all.filter((item) => item.id !== id));
  }
  async function rotate() {
    const result = await clientApi<{ shareToken: string }>(
      "/api/Social/profile/rotate-token",
      { method: "POST" },
    );
    setProfile((p) => (p ? { ...p, shareToken: result.shareToken } : p));
  }
  const shareUrl =
    profile?.shareToken && typeof window !== "undefined"
      ? `${window.location.origin}/u/share?t=${profile.shareToken}`
      : "";
  return (
    <div className="grid gap-5 lg:grid-cols-2">
      <section className="card space-y-4 p-6">
        <h2 className="section-title">Profile</h2>
        <label>
          <span className="label">Display name</span>
          <input
            className="input"
            value={name}
            onChange={(e) => setName(e.target.value)}
          />
        </label>
        <label>
          <span className="label">Bio</span>
          <textarea
            className="input min-h-24"
            value={bio}
            onChange={(e) => setBio(e.target.value)}
          />
        </label>
        <label className="flex items-center justify-between rounded-2xl border p-4">
          <span>
            <strong className="block">Share by default</strong>
            <small className="text-charcoal-blue-500">
              Decide again after every workout.
            </small>
          </span>
          <input
            className="toggle"
            type="checkbox"
            checked={publish}
            onChange={(e) => setPublish(e.target.checked)}
          />
        </label>
        <button className="btn-primary" disabled={!name.trim()} onClick={save}>
          Save profile
        </button>
        {shareUrl && (
          <div className="rounded-2xl bg-brand-50 p-4 dark:bg-brand-950/30">
            <strong>Private share link</strong>
            <input
              className="input mt-2"
              readOnly
              value={shareUrl}
              onFocus={(e) => e.target.select()}
            />
            <div className="mt-2 flex gap-2">
              <button
                className="btn-secondary btn-sm"
                onClick={() => navigator.clipboard.writeText(shareUrl)}
              >
                Copy
              </button>
              <button className="btn-ghost btn-sm" onClick={rotate}>
                Reset
              </button>
            </div>
          </div>
        )}
      </section>
      <section className="card p-6">
        <h2 className="section-title">Follow requests</h2>
        {!requests.length ? (
          <div className="py-8 text-center">
            <Image
              src="/illustrations/empty-followers.svg"
              alt=""
              width={192}
              height={154}
              className="mx-auto h-auto w-48"
            />
            <p className="text-sm text-charcoal-blue-500">
              No pending requests.
            </p>
          </div>
        ) : (
          <div className="mt-4 space-y-3">
            {requests.map((item) => (
              <div
                key={item.id}
                className="flex items-center gap-3 rounded-2xl border p-3"
              >
                <strong className="flex-1">{item.displayName}</strong>
                <button
                  className="btn-primary btn-sm"
                  onClick={() => respond(item.id, true)}
                >
                  Accept
                </button>
                <button
                  className="btn-ghost btn-sm"
                  onClick={() => respond(item.id, false)}
                >
                  Decline
                </button>
              </div>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}
