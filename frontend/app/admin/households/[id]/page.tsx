import Link from "next/link";
import { notFound } from "next/navigation";
import AdminPageHeader from "@/components/admin/AdminPageHeader";
import AdminFacts from "@/components/admin/AdminFacts";
import SupportNotesPanel from "@/components/admin/SupportNotesPanel";
import {
  getAdminHouseholdServer,
  listSupportNotesServer,
} from "@/lib/api/admin-ops.server";
import UsageThisMonth from "@/components/admin/platform/UsageThisMonth";
import AdminMemberRow from "./AdminMemberRow";

export const dynamic = "force-dynamic";

export const metadata = {
  title: "Household | Mizan admin",
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
    <div className="flex flex-col gap-10 [&>header]:mb-0">
      <AdminPageHeader
        title={detail.name}
        lead={`Created ${day(detail.createdAt)} · ${detail.members.length} ${detail.members.length === 1 ? "member" : "members"}`}
        back={{ href: "/admin/households", label: "Households" }}
      />

      <AdminFacts
        facts={[
          {
            label: "Owner",
            value: detail.ownerUserId ? (
              <Link
                href={`/admin/users/${detail.ownerUserId}`}
                className="text-[16px] hover:underline"
              >
                {detail.ownerName || detail.ownerEmail}
              </Link>
            ) : (
              <span className="text-[14px] font-normal text-muted-foreground">
                No owner among the members
              </span>
            ),
          },
          { label: "Meal plans", value: detail.mealPlanCount },
          { label: "Shopping lists", value: detail.shoppingListCount },
        ]}
      />

      <section aria-labelledby="members-title" className="flex flex-col">
        <div className="log-section-header">
          <h2 id="members-title" className="text-[20px] font-semibold">
            Members
          </h2>
        </div>
        <ul>
          {detail.members.map((m) => (
            <AdminMemberRow key={m.userId} householdId={detail.id} member={m} />
          ))}
        </ul>
      </section>

      <UsageThisMonth householdId={detail.id} />

      {detail.pendingInvitations.length > 0 && (
        <section aria-labelledby="invites-title" className="flex flex-col">
          <div className="log-section-header">
            <h2 id="invites-title" className="text-[20px] font-semibold">
              Pending invitations
            </h2>
          </div>
          <ul>
            {detail.pendingInvitations.map((inv) => (
              <li key={inv.id} className="ledger-row text-sm">
                <div className="min-w-0">
                  <p className="truncate font-medium">
                    {inv.invitedName ?? inv.invitedEmail}
                  </p>
                  <p className="truncate text-xs text-muted-foreground">
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
