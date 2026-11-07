"use client";

import { useState } from "react";
import { Trash2 } from "lucide-react";
import { useRouter } from "next/navigation";
import { clientApi } from "@/lib/api.client";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { appToast } from "@/lib/toast";

export default function DeleteMeasurementButton({ id }: { id: string }) {
  const router = useRouter();
  const [open, setOpen] = useState(false);

  return (
    <>
      <button
        type="button"
        className="icon-button-destructive"
        aria-label="Delete measurement"
        onClick={() => setOpen(true)}
      >
        <Trash2 size={15} aria-hidden="true" />
      </button>
      <ConfirmDialog
        open={open}
        onClose={() => setOpen(false)}
        title="Delete this measurement?"
        description="Every value recorded in this measurement is removed. This cannot be undone."
        pendingLabel="Deleting…"
        errorFallback="Failed to delete measurement"
        onConfirm={async () => {
          await clientApi(`/api/BodyMeasurements/${id}`, { method: "DELETE" });
          appToast.success("Measurement deleted");
          router.refresh();
        }}
      />
    </>
  );
}
