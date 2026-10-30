"use client";
import { useRouter } from "next/navigation";
import { clientApi } from "@/lib/api.client";
import { appToast } from "@/lib/toast";
import { ConfirmButton } from "@/components/ConfirmDialog";
export default function ModerationActions({ id }: { id: string }) {
  const router = useRouter();
  async function send(action: "dismiss" | "delete") {
    await clientApi(`/api/admin/social/reports/${id}/resolve`, {
      method: "POST",
      body: { action },
    });
    router.refresh();
  }
  async function resolve(action: "dismiss" | "delete") {
    try {
      await send(action);
    } catch (error) {
      appToast.error(error, "Could not resolve report");
    }
  }
  return (
    <div className="flex gap-2">
      <button className="btn-ghost btn-sm" onClick={() => resolve("dismiss")}>
        Dismiss
      </button>
      <ConfirmButton
        className="btn-danger btn-sm"
        title="Remove the reported content?"
        description="The post or comment is deleted for everyone and the report is closed."
        confirmLabel="Remove content"
        pendingLabel="Removing…"
        errorFallback="Could not resolve report"
        onConfirm={() => send("delete")}
      >
        Remove content
      </ConfirmButton>
    </div>
  );
}
