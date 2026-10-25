import Link from "next/link";
import { notFound } from "next/navigation";
import SupportNotesPanel from "@/components/admin/SupportNotesPanel";
import {
  getAdminHouseholdServer,
  listSupportNotesServer,
} from "@/lib/api/admin-ops.server";
import AdminMemberRow from "./AdminMemberRow";

export const dynamic = "force-dynamic";

export const metadata = {
  title: "Household detail | Mizan admin",
};

function day(value: string) {
  return new Date(value).toLocaleDateString("en-GB", {
    day: "numeric",
    month: "short",
    year: "numeric",
    timeZone: "UTC",
  });
}

/**
 * One household for support (docs/ADMIN.md#households-users-and-support-notes):
 * the owner, members with roles and join dates, pending invitations, how many
 * plans and lists it holds, and the administrators' notes.
 */
export default async function AdminHouseholdDetail({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  const [detail, notes] = await Promise.all([
    getAdminHouseholdServer(id),
    listSupportNotesServer("Household", id),
  ]);

  if (!detail) {
    notFound();
  }

  return (
    <div className="space-y-6 lg:space-y-8">
      <header className="space-y-2">
        <Link
          href="/admin/households"
          className="text-xs uppercase tracking-[0.14em] text-brand-700 hover:underline dark:text-brand-300"
        >
          ← All households
        </Link>
        <h1 className="break-words text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
          {detail.name}
        </h1>
        <p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
          Created {day(detail.createdAt)} · {detail.members.length}{" "}
          {detail.members.length === 1 ? "member" : "members"}
        </p>
      </header>

      <dl className="grid grid-cols-1 gap-3 sm:grid-cols-3">
        <div className="border border-charcoal-blue-200 p-4 dark:border-charcoal-blue-700">
          <dt className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
            Owner
          </dt>
          <dd className="mt-1 break-words text-sm font-medium">
            {detail.ownerUserId ? (
              <a
                href={`/admin/users/${detail.ownerUserId}`}
                className="hover:underline"
              >
                {detail.ownerName || detail.ownerEmail}
              </a>
            ) : (
              "No owner among the members"
            )}
          </dd>
        </div>
        <div className="border border-charcoal-blue-200 p-4 dark:border-charcoal-blue-700">
          <dt className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
            Meal plans
          </dt>
          <dd className="mt-1 text-2xl font-semibold tabular-nums">
            {detail.mealPlanCount}
          </dd>
        </div>
        <div className="border border-charcoal-blue-200 p-4 dark:border-charcoal-blue-700">
          <dt className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
            Shopping lists
          </dt>
          <dd className="mt-1 text-2xl font-semibold tabular-nums">
            {detail.shoppingListCount}
          </dd>
        </div>
      </dl>

      <section className="card p-5 sm:p-6" aria-labelledby="members-title">
        <h2
          id="members-title"
          className="mb-4 text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50"
        >
          Members
        </h2>
        <ul className="divide-y divide-charcoal-blue-100 dark:divide-white/10">
          {detail.members.map((m) => (
            <AdminMemberRow key={m.userId} householdId={detail.id} member={m} />
          ))}
        </ul>
      </section>

      {detail.pendingInvitations.length > 0 && (
        <section className="card p-5 sm:p-6" aria-labelledby="invites-title">
          <h2
            id="invites-title"
            className="mb-4 text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50"
          >
            Pending invitations
          </h2>
          <ul className="space-y-2">
            {detail.pendingInvitations.map((inv) => (
              <li
                key={inv.id}
                className="flex items-center justify-between rounded-2xl border border-dashed border-charcoal-blue-200 bg-charcoal-blue-50/40 px-4 py-3 text-sm dark:border-white/10 dark:bg-charcoal-blue-900/40"
              >
                <div className="min-w-0">
                  <p className="truncate font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">
                    {inv.invitedName ?? inv.invitedEmail}
                  </p>
                  <p className="truncate text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
                    {inv.invitedEmail} · {inv.role} · expires{" "}
                    {day(inv.expiresAt)}
                  </p>
                </div>
              </li>
            ))}
          </ul>
        </section>
      )}

      <SupportNotesPanel
        subjectType="Household"
        subjectId={detail.id}
        initialNotes={notes}
      />
    </div>
  );
}
