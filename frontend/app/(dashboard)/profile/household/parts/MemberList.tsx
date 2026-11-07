"use client";

import { useRouter } from "next/navigation";
import {
  removeHouseholdMember,
  respondToInvitation,
  type HouseholdMemberDto,
  type PendingInviteAdminDto,
} from "@/data/household";
import { appToast } from "@/lib/toast";
import { ConfirmButton } from "@/components/ConfirmDialog";

export function MemberList({
  householdId,
  members,
  pendingInvites,
  canManage,
}: {
  householdId: string;
  members: HouseholdMemberDto[];
  pendingInvites: PendingInviteAdminDto[];
  canManage: boolean;
}) {
  const router = useRouter();

  // The confirmation dialogs show a failure themselves and stay open.
  const handleRemove = async (userId: string) => {
    const result = await removeHouseholdMember(householdId, userId);
    if (!result.success)
      throw new Error(result.message ?? "Could not remove member.");
    appToast.success("Member removed.");
    router.refresh();
  };

  const handleRevoke = async (invitationId: string) => {
    const result = await respondToInvitation(invitationId, "revoke");
    if (!result.success)
      throw new Error(result.message ?? "Could not revoke invitation.");
    appToast.success("Invitation revoked.");
    router.refresh();
  };

  return (
    <div className="space-y-4">
      <ul className="divide-y divide-charcoal-blue-100 dark:divide-white/10">
        {members.map((m) => (
          <li
            key={m.userId}
            className="flex items-center justify-between gap-3 py-3"
          >
            <div className="min-w-0 flex-1">
              <p className="truncate text-sm font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">
                {m.name ?? m.email ?? "Unknown"}
              </p>
              <p className="truncate text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
                {m.email} · {m.role ?? "member"}
              </p>
            </div>
            {canManage && m.role !== "owner" && (
              <ConfirmButton
                className="btn-ghost-destructive h-8"
                title={`Remove ${m.name ?? m.email ?? "this member"} from the household?`}
                description="They lose access to the household's shared recipes, meal plans and shopping lists. Their own log is not affected."
                confirmLabel="Remove member"
                pendingLabel="Removing…"
                errorFallback="Could not remove member."
                onConfirm={() => handleRemove(m.userId)}
              >
                Remove
              </ConfirmButton>
            )}
          </li>
        ))}
      </ul>

      {pendingInvites.length > 0 && (
        <div>
          <h3 className="mb-2 text-xs font-semibold uppercase tracking-[0.14em] text-charcoal-blue-500 dark:text-charcoal-blue-400">
            Pending invitations
          </h3>
          <ul className="space-y-2">
            {pendingInvites.map((inv) => (
              <li
                key={inv.id}
                className="flex items-center justify-between gap-3 rounded-2xl border border-dashed border-charcoal-blue-200 bg-charcoal-blue-50/40 px-3 py-2 text-sm dark:border-white/10 dark:bg-charcoal-blue-900/40"
              >
                <div>
                  <p className="font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">
                    {inv.invitedName ?? inv.invitedEmail}
                  </p>
                  <p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
                    {inv.invitedEmail} · {inv.role} · expires{" "}
                    {new Date(inv.expiresAt).toLocaleDateString()}
                  </p>
                </div>
                {canManage && (
                  <ConfirmButton
                    className="btn-ghost-destructive h-8"
                    title={`Revoke the invitation to ${inv.invitedName ?? inv.invitedEmail}?`}
                    description="The invitation link stops working. You can invite them again later."
                    confirmLabel="Revoke invitation"
                    pendingLabel="Revoking…"
                    errorFallback="Could not revoke invitation."
                    onConfirm={() => handleRevoke(inv.id)}
                  >
                    Revoke
                  </ConfirmButton>
                )}
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
