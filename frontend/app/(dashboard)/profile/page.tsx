"use client";

import { useEffect, useState } from "react";
import Image from "next/image";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ChevronRight } from "lucide-react";
import { listSessions, signOut, useSession } from "@/lib/auth-client";
import { clearAppearanceCookie } from "@/lib/appearance-cookie";
import {
  getProfileObservations,
  type ProfileObservations,
} from "@/lib/api/profile";
import { useSubscription } from "@/lib/hooks/useSubscription";
import { appToast } from "@/lib/toast";

const monthYear = (value: string) =>
  new Date(value).toLocaleDateString(undefined, {
    month: "long",
    year: "numeric",
  });

const plural = (count: number, one: string) =>
  `${count} ${one}${count === 1 ? "" : "s"}`;

/**
 * Who you are to Mizan (design board Profile.dc.html): the name, how long you
 * have been here, three facts the API keeps, and where the account is managed.
 * Features live in More; settings live on their own page.
 */
export default function ProfilePage() {
  const { data: session, isPending } = useSession();
  const router = useRouter();
  const { subscription } = useSubscription();
  const [facts, setFacts] = useState<ProfileObservations | null>(null);
  const [sessionCount, setSessionCount] = useState<number | null>(null);
  const [signingOut, setSigningOut] = useState(false);

  useEffect(() => {
    if (!session?.user) return;
    let cancelled = false;
    getProfileObservations()
      .then((result) => {
        if (!cancelled) setFacts(result);
      })
      .catch(() => {
        /* The name and the links still render; the facts show a dash. */
      });
    listSessions()
      .then((sessions) => {
        if (!cancelled)
          setSessionCount(
            sessions.filter((item) => new Date(item.expiresAt) > new Date())
              .length,
          );
      })
      .catch(() => {});
    return () => {
      cancelled = true;
    };
  }, [session?.user]);

  if (isPending) {
    return (
      <div className="mx-auto max-w-[900px]" aria-busy="true">
        <h1 className="sr-only">Profile</h1>
        <div className="h-24 border-b border-foreground" />
      </div>
    );
  }
  if (!session?.user) return null;

  const user = session.user;
  const plan = subscription
    ? `${subscription.plan}${subscription.isLifetime ? ", lifetime" : ""}`
    : null;
  const role =
    user.role === "admin"
      ? "Administrator"
      : user.role === "trainer"
        ? "Trainer"
        : null;
  const since = [
    facts ? `Member since ${monthYear(facts.joinedAt)}` : null,
    plan,
    role,
  ].filter(Boolean);

  const manage = [
    {
      href: "/profile/settings",
      title: "Settings",
      detail: "Account, your day and prices, assistant, appearance",
    },
    {
      href: "/profile/household",
      title: "Household",
      detail: "Share plans and shopping lists",
    },
    {
      href: "/profile/mcp",
      title: "Connected apps",
      detail: "Telegram, Claude and other assistants",
    },
    {
      href: "/billing",
      title: "Subscription",
      detail: plan ?? "Your plan and invoices",
    },
    {
      href: "/profile/sessions",
      title: "Signed-in devices",
      detail:
        sessionCount === null
          ? "Where you are signed in"
          : `${plural(sessionCount, "active session")}`,
    },
  ];

  async function logout() {
    setSigningOut(true);
    try {
      await signOut();
      clearAppearanceCookie();
      router.push("/");
      router.refresh();
    } catch (error) {
      appToast.error(error, "Could not sign out");
      setSigningOut(false);
    }
  }

  return (
    <div
      className="mx-auto flex max-w-[900px] flex-col gap-8"
      data-testid="profile-page"
    >
      <header className="flex flex-wrap items-center gap-5 border-b border-foreground pb-6">
        {user.image ? (
          <span className="relative h-[72px] w-[72px] shrink-0 overflow-hidden rounded-full">
            <Image
              src={user.image}
              alt=""
              fill
              sizes="72px"
              className="object-cover"
            />
          </span>
        ) : (
          <span
            aria-hidden="true"
            className="flex h-[72px] w-[72px] shrink-0 items-center justify-center rounded-full bg-hair text-[28px] font-semibold"
          >
            {(user.name || user.email || "?").charAt(0).toUpperCase()}
          </span>
        )}
        <div className="flex min-w-0 flex-[1_1_240px] flex-col gap-1.5">
          <p className="text-xs font-semibold tracking-[0.14em] uppercase text-muted-foreground">
            Profile
          </p>
          <h1 className="truncate text-[30px] leading-tight font-semibold tracking-tight sm:text-[34px]">
            {user.name || user.email}
          </h1>
          <p className="text-sm text-muted-foreground">
            {since.length > 0 ? since.join(" · ") : user.email}
          </p>
        </div>
        <Link href="/profile/settings#account" className="btn-secondary min-h-11">
          Edit profile
        </Link>
      </header>

      <section
        aria-label="Your progress"
        className="grid grid-cols-1 border-b border-border sm:grid-cols-3"
      >
        <Fact label="Streak" first>
          <span className="tabular-nums text-[30px] leading-none">
            {facts ? plural(facts.streakCount, "day") : "—"}
          </span>
          <span className="text-[13px] text-muted-foreground">Log today to keep it</span>
        </Fact>
        <Fact label="Goal">
          <span className="text-lg leading-snug">
            {facts?.goalSummary || "—"}
          </span>
          <Link href="/progress" className="text-link">
            See progress
          </Link>
        </Fact>
        <Fact label="Achievements">
          <span className="tabular-nums text-[30px] leading-none">
            {facts ? facts.achievementCount : "—"}
          </span>
          <Link href="/achievements" className="text-link">
            All achievements
          </Link>
        </Fact>
      </section>

      <section aria-labelledby="manage-title">
        <h2
          id="manage-title"
          className="mb-1.5 text-xs font-semibold tracking-[0.14em] uppercase text-muted-foreground"
        >
          Manage
        </h2>
        <ul>
          {manage.map((link) => (
            <li key={link.href}>
              <Link
                href={link.href}
                className="ledger-row transition-colors hover:bg-hair"
              >
                <span className="min-w-0 flex-1">
                  <span className="block text-[15px] font-medium">
                    {link.title}
                  </span>
                  <span className="mt-0.5 block text-[13px] text-muted-foreground">
                    {link.detail}
                  </span>
                </span>
                <ChevronRight
                  aria-hidden="true"
                  size={18}
                  strokeWidth={1.6}
                  className="text-muted-foreground"
                />
              </Link>
            </li>
          ))}
        </ul>
      </section>

      <button
        type="button"
        onClick={logout}
        disabled={signingOut}
        className="text-link self-start"
      >
        {signingOut ? "Signing out…" : "Sign out"}
      </button>
    </div>
  );
}

function Fact({
  label,
  first,
  children,
}: {
  label: string;
  first?: boolean;
  children: React.ReactNode;
}) {
  return (
    <div
      className={
        first
          ? "flex flex-col gap-1.5 pt-1 pr-5 pb-5"
          : "flex flex-col gap-1.5 border-t border-border py-5 sm:border-t-0 sm:border-l sm:pt-1 sm:pr-5 sm:pl-5"
      }
    >
      <span className="text-[10px] font-semibold tracking-[0.14em] uppercase text-muted-foreground">
        {label}
      </span>
      {children}
    </div>
  );
}
