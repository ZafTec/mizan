"use client";

import { useRouter } from "next/navigation";
import { adminRemoveHouseholdMember } from "@/data/admin/household";
import type { AdminHouseholdDetail } from "@/lib/api/admin-ops";
import { appToast } from "@/lib/toast";
import { ConfirmButton } from "@/components/ConfirmDialog";

export default function AdminMemberRow({
  householdId,
  member,
}: {
  householdId: string;
  member: AdminHouseholdDetail["members"][number];
}) {
  const router = useRouter();
  const label = member.name ?? member.email;

  const handleRemove = async () => {
    const result = await adminRemoveHouseholdMember(householdId, member.userId);
    if (!result.success)
      throw new Error(result.message ?? "Could not remove member.");
    appToast.success("Member removed.");
    router.refresh();
  };

  return (
    <li className="flex items-center justify-between gap-3 py-3">
      <div className="min-w-0 flex-1">
        <p className="truncate text-sm font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">
          <a href={`/admin/users/${member.userId}`} className="hover:underline">
            {member.name ?? "(no name)"}
          </a>
          {member.isOwner && (
            <span className="ml-2 text-xs font-normal text-charcoal-blue-500 dark:text-charcoal-blue-400">
              Owner
            </span>
          )}
        </p>
        <p className="truncate text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
          {member.email} · {member.role} · joined{" "}
          {new Date(member.joinedAt).toLocaleDateString("en-GB", {
            day: "numeric",
            month: "short",
            year: "numeric",
            timeZone: "UTC",
          })}
        </p>
      </div>
      <ConfirmButton
        className="btn-ghost-destructive h-8"
        title={`Remove ${label} from this household?`}
        description="They lose access to the household's shared plans and shopping lists. Their own log is not affected."
        confirmLabel="Remove member"
        pendingLabel="Removing…"
        errorFallback="Could not remove member."
        onConfirm={handleRemove}
      >
        Remove
      </ConfirmButton>
    </li>
  );
}
