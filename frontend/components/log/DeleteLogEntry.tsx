"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Trash2 } from "lucide-react";
import { clientApi } from "@/lib/api.client";
import { changeList } from "@/components/motion/changeList";
import { ConfirmDialog } from "@/components/ConfirmDialog";

type EntryKind = "Meals" | "MealGroup" | "Workouts" | "BodyMeasurements";

const deleteEntry: Record<EntryKind, (id: string) => Promise<unknown>> = {
  Meals: (id) => clientApi(`/api/Meals/${id}`, { method: "DELETE" }),
  MealGroup: (id) => clientApi(`/api/Meals/groups/${id}`, { method: "DELETE" }),
  Workouts: (id) => clientApi(`/api/Workouts/${id}`, { method: "DELETE" }),
  BodyMeasurements: (id) =>
    clientApi(`/api/BodyMeasurements/${id}`, { method: "DELETE" }),
};

export default function DeleteLogEntry({
  id,
  name,
  kind = "Meals",
}: {
  id: string;
  name: string;
  kind?: EntryKind;
}) {
  const [open, setOpen] = useState(false);
  const router = useRouter();
  const group = kind === "MealGroup";
  return (
    <>
      {group ? (
        <button
          type="button"
          className="btn-ghost-destructive btn-sm"
          onClick={() => setOpen(true)}
        >
          Delete whole recipe
        </button>
      ) : (
        <button
          type="button"
          className="icon-button-destructive"
          onClick={() => setOpen(true)}
          aria-label={`Delete ${name}`}
        >
          <Trash2 size={15} />
        </button>
      )}
      <ConfirmDialog
        open={open}
        onClose={() => setOpen(false)}
        title={
          group ? "Delete this recipe from your log?" : "Delete this entry?"
        }
        description={
          group
            ? `${name} and every ingredient row logged with it will be removed. This cannot be undone.`
            : `${name} will be removed from your log. This cannot be undone.`
        }
        confirmLabel={group ? "Delete recipe" : "Delete entry"}
        pendingLabel="Deleting…"
        cancelLabel="Keep entry"
        errorFallback="Could not delete this entry. Please try again."
        onConfirm={async () => {
          await deleteEntry[kind](id);
          // The row fades out and the rows below it move up.
          changeList(() => router.refresh());
        }}
      />
    </>
  );
}
