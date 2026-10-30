"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { adminDeleteHousehold } from "@/data/admin/household";
import { ConfirmButton } from "@/components/ConfirmDialog";
import { appToast } from "@/lib/toast";

export default function HouseholdRowActions({
  id,
  name,
}: {
  id: string;
  name: string;
}) {
  const router = useRouter();

  return (
    <div className="flex items-center justify-end gap-2">
      <Link href={`/admin/households/${id}`} className="btn-ghost h-8">
        Inspect
      </Link>
      <ConfirmButton
        className="btn-ghost-destructive h-8"
        title={`Delete “${name}”?`}
        description="The household, its memberships and its shared plans and lists are deleted. This cannot be undone."
        confirmLabel="Delete household"
        pendingLabel="Deleting…"
        errorFallback="Delete failed."
        onConfirm={async () => {
          const result = await adminDeleteHousehold(id);
          if (!result.success)
            throw new Error(result.message ?? "Delete failed.");
          appToast.success("Household deleted.");
          router.refresh();
        }}
      >
        Delete
      </ConfirmButton>
    </div>
  );
}
