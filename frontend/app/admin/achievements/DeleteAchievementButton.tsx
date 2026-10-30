"use client";

import { useRouter } from "next/navigation";
import { clientApi } from "@/lib/api.client";
import { ConfirmButton } from "@/components/ConfirmDialog";
import { appToast } from "@/lib/toast";

/**
 * Deleting an achievement people have already earned takes their badge with
 * it, so the count is in the confirmation rather than only in the table.
 */
export default function DeleteAchievementButton({
  id,
  name,
  unlockedBy,
}: {
  id: string;
  name: string;
  unlockedBy: number;
}) {
  const router = useRouter();
  return (
    <ConfirmButton
      title={`Delete “${name}”?`}
      description={
        unlockedBy === 0
          ? "Nobody has earned this one, so nothing is lost."
          : `${unlockedBy} ${unlockedBy === 1 ? "person has" : "people have"} earned this. Deleting it removes their badge and its points.`
      }
      pendingLabel="Deleting…"
      errorFallback="Could not delete that achievement"
      onConfirm={async () => {
        await clientApi(`/api/Achievements/${id}`, { method: "DELETE" });
        appToast.success(`Deleted "${name}"`);
        router.refresh();
      }}
    >
      Delete
    </ConfirmButton>
  );
}
