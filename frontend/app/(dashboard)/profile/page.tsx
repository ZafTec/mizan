"use client";

import { useEffect, useState } from "react";
import Image from "next/image";
import Link from "next/link";
import { ChevronRight } from "lucide-react";
import { useSession } from "@/lib/auth-client";
import {
  getProfileObservations,
  type ProfileObservations,
} from "@/lib/api/profile";

const ACCOUNT_LINKS = [
  {
    href: "/profile/settings",
    title: "Settings",
    detail: "Name, time zone, appearance, assistant",
  },
  {
    href: "/profile/sessions",
    title: "Sessions",
    detail: "Where you are signed in",
  },
  {
    href: "/profile/mcp",
    title: "Connected apps",
    detail: "What AI assistants may do",
  },
  { href: "/billing", title: "Billing", detail: "Your plan and invoices" },
  {
    href: "/profile/settings#export",
    title: "Export my data",
    detail: "Everything you have logged, as one file",
  },
] as const;

const longDate = (value: string) =>
  new Date(value).toLocaleDateString(undefined, {
    day: "numeric",
    month: "short",
    year: "numeric",
  });

/** Only the account. Features live in More; this page says who you are to Mizan and where the account settings are. */
export default function ProfilePage() {
  const { data: session, isPending } = useSession();
  const [facts, setFacts] = useState<ProfileObservations | null>(null);

  useEffect(() => {
    if (!session?.user) return;
    let cancelled = false;
    getProfileObservations()
      .then((result) => {
        if (!cancelled) setFacts(result);
      })
      .catch(() => {
        /* The identity rows still render; the streak row shows a dash. */
      });
    return () => {
      cancelled = true;
    };
  }, [session?.user]);

  if (isPending) {
    return (
      <div className="log-page" aria-busy="true">
        <header className="log-page-header block!">
          <h1>Profile</h1>
        </header>
        <div className="h-14 border-y border-border" />
      </div>
    );
  }
  if (!session?.user) return null;

  const user = session.user;
  const rows: Array<{ label: string; value: string }> = [
    { label: "Member since", value: facts ? longDate(facts.joinedAt) : "—" },
    {
      label: "Time zone",
      value: user.timeZoneId?.replace(/_/g, " ") || "Not set",
    },
    ...(user.role && user.role !== "user"
      ? [
          {
            label: "Role",
            value: user.role === "admin" ? "Administrator" : "Trainer",
          },
        ]
      : []),
    {
      label: "Streak",
      value: facts
        ? `${facts.streakCount} day${facts.streakCount === 1 ? "" : "s"}`
        : "—",
    },
    { label: "Targets", value: facts?.goalSummary || "—" },
  ];

  return (
    <div className="log-page space-y-8" data-testid="profile-page">
      <header className="log-page-header block!">
        <h1>Profile</h1>
        <p className="log-muted mt-2">Who you are to Mizan.</p>
      </header>

      <div className="flex items-center gap-4 border-y border-t-foreground border-b-border py-4">
        {user.image ? (
          <span className="relative h-14 w-14 shrink-0 overflow-hidden rounded-full">
            <Image
              src={user.image}
              alt=""
              fill
              sizes="56px"
              className="object-cover"
            />
          </span>
        ) : (
          <span
            aria-hidden="true"
            className="flex h-14 w-14 shrink-0 items-center justify-center rounded-full bg-muted text-xl font-semibold"
          >
            {(user.name || user.email || "?").charAt(0).toUpperCase()}
          </span>
        )}
        <div className="min-w-0">
          <p className="truncate text-base font-semibold">
            {user.name || user.email}
          </p>
          <p className="truncate text-sm log-muted">{user.email}</p>
        </div>
      </div>

      <dl>
        {rows.map((row) => (
          <div key={row.label} className="ledger-row">
            <dt className="flex-1">{row.label}</dt>
            <dd className="num text-right text-sm">{row.value}</dd>
          </div>
        ))}
      </dl>

      <section aria-labelledby="account-title">
        <header className="log-section-header">
          <h2 id="account-title">Account</h2>
        </header>
        <ul>
          {ACCOUNT_LINKS.map((link) => (
            <li key={link.href}>
              <Link
                href={link.href}
                className="ledger-row transition-colors hover:bg-muted"
              >
                <span className="min-w-0 flex-1">
                  <span className="block font-medium">{link.title}</span>
                  <span className="mt-0.5 block text-sm log-muted">
                    {link.detail}
                  </span>
                </span>
                <ChevronRight
                  aria-hidden="true"
                  size={17}
                  strokeWidth={1.7}
                  className="log-muted"
                />
              </Link>
            </li>
          ))}
        </ul>
      </section>
    </div>
  );
}
