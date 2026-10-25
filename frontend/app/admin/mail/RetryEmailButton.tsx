"use client";

import { useTransition } from "react";
import { useRouter } from "next/navigation";
import { retryEmail } from "@/lib/api/admin-ops";
import { appToast } from "@/lib/toast";

/** Queues a message that did not go out once more; the log row turns Queued. */
export default function RetryEmailButton({ id }: { id: string }) {
  const router = useRouter();
  const [pending, startTransition] = useTransition();

  return (
    <button
      type="button"
      className="btn-primary btn-sm"
      disabled={pending}
      onClick={() =>
        startTransition(async () => {
          try {
            await retryEmail(id);
            appToast.success("Queued again");
            router.refresh();
          } catch (error) {
            appToast.error(error, "Could not queue the email again");
          }
        })
      }
    >
      {pending ? "Queueing…" : "Retry"}
    </button>
  );
}
