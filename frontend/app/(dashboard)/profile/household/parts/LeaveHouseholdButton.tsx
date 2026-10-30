"use client";

import { useRouter } from "next/navigation";
import { leaveHousehold } from "@/data/household";
import { ConfirmButton } from "@/components/ConfirmDialog";
import { appToast } from "@/lib/toast";

export function LeaveHouseholdButton({
  householdId,
  disabled,
}: {
  householdId: string;
  disabled?: boolean;
}) {
  const router = useRouter();

  return (
    <ConfirmButton
      className="btn-ghost h-9"
      disabled={disabled}
      title="Leave this household?"
      description="You lose access to its shared recipes, meal plans and shopping lists. Your own log is not affected."
      confirmLabel="Leave household"
      pendingLabel="Leaving…"
      errorFallback="Could not leave household."
      onConfirm={async () => {
        const result = await leaveHousehold(householdId);
        if (!result.success)
          throw new Error(result.message ?? "Could not leave household.");
        appToast.success("Left household.");
        router.push("/profile/household");
        router.refresh();
      }}
    >
      Leave
    </ConfirmButton>
  );
}
