"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { clientApi } from "@/lib/api.client";
import { ConfirmButton } from "@/components/ConfirmDialog";
import { appToast } from "@/lib/toast";

/**
 * Ending someone else's coaching relationship is not a click you want to make
 * by accident, so it asks - and it says whose access is about to stop.
 */
export default function EndRelationshipButton({
  id,
  trainer,
  client,
}: {
  id: string;
  trainer: string;
  client: string;
}) {
  const router = useRouter();
  const [reason, setReason] = useState("");

  return (
    <ConfirmButton
      className="whitespace-nowrap text-xs text-destructive hover:underline"
      title="End this relationship?"
      description={`${trainer} loses access to ${client}'s log on their next request. The client's sharing choices are kept, so re-accepting restores what they had.`}
      confirmLabel="End relationship"
      pendingLabel="Ending…"
      errorFallback="Could not end the relationship"
      details={
        <label className="block space-y-1">
          <span className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
            Reason (recorded in the audit log)
          </span>
          <input
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder="Client asked by email"
            className="input w-full !py-2 text-sm"
          />
        </label>
      }
      onConfirm={async () => {
        await clientApi(`/api/Admin/Relationships/${id}/end`, {
          method: "POST",
          body: { reason: reason.trim() || null },
        });
        appToast.success("Relationship ended");
        router.refresh();
      }}
    >
      End
    </ConfirmButton>
  );
}
