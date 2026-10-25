"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { acceptPlan, declinePlan } from "@/lib/api/mealPlans";
import { appToast } from "@/lib/toast";

/**
 * A client's answer to a coach's draft. Accepting makes it the active plan
 * and archives the current one; declining archives the draft.
 */
export default function DraftActions({
  planId,
  planName,
  afterAccept = "refresh",
}: {
  planId: string;
  planName: string;
  afterAccept?: "refresh" | "list";
}) {
  const router = useRouter();
  const [busy, setBusy] = useState<"accept" | "decline" | null>(null);

  const run = async (action: "accept" | "decline") => {
    setBusy(action);
    try {
      if (action === "accept") {
        await acceptPlan(planId);
        appToast.success(`${planName} is now your active plan`);
      } else {
        await declinePlan(planId);
        appToast.success(`${planName} declined`);
      }
      if (afterAccept === "list") router.push("/meal-plan");
      router.refresh();
    } catch (error) {
      appToast.error(error, "Could not update this plan");
    } finally {
      setBusy(null);
    }
  };

  return (
    <div className="flex shrink-0 items-center gap-2">
      <button
        type="button"
        className="btn-primary btn-sm min-h-9"
        disabled={busy !== null}
        aria-label={`Accept ${planName}`}
        onClick={() => run("accept")}
      >
        Accept
      </button>
      <button
        type="button"
        className="btn-ghost btn-sm min-h-9"
        disabled={busy !== null}
        aria-label={`Decline ${planName}`}
        onClick={() => run("decline")}
      >
        Decline
      </button>
    </div>
  );
}
