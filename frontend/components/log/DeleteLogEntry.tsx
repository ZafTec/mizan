"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Trash2 } from "lucide-react";
import { clientApi } from "@/lib/api.client";
import { changeList } from "@/components/motion/changeList";
import {
  Dialog,
  DialogContent,
  DialogTitle,
  DialogDescription,
} from "@/components/ui/dialog";

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
  const [pending, setPending] = useState(false);
  const [error, setError] = useState("");
  const router = useRouter();
  const group = kind === "MealGroup";
  return (
    <Dialog
      open={open}
      onOpenChange={(value) => {
        if (!pending) {
          setOpen(value);
          setError("");
        }
      }}
    >
      {group ? (
        <button
          className="btn-ghost-destructive btn-sm"
          onClick={() => setOpen(true)}
        >
          Delete whole recipe
        </button>
      ) : (
        <button
          className="icon-button-destructive"
          onClick={() => setOpen(true)}
          aria-label={`Delete ${name}`}
        >
          <Trash2 size={15} />
        </button>
      )}
      <DialogContent>
        <DialogTitle>
          {group ? "Delete this recipe from your log?" : "Delete this entry?"}
        </DialogTitle>
        <DialogDescription>
          {group
            ? `${name} and every ingredient row logged with it will be removed. This cannot be undone.`
            : `${name} will be removed from your log. This cannot be undone.`}
        </DialogDescription>
        {error && (
          <p role="alert" className="text-destructive text-sm">
            {error}
          </p>
        )}
        <div className="flex justify-end gap-3 mt-2">
          <button
            className="btn-secondary"
            onClick={() => setOpen(false)}
            disabled={pending}
          >
            Keep entry
          </button>
          <button
            className="btn-danger"
            disabled={pending}
            onClick={async () => {
              setPending(true);
              setError("");
              try {
                await deleteEntry[kind](id);
                setOpen(false);
                // The row fades out and the rows below it move up.
                changeList(() => router.refresh());
              } catch {
                setError("Could not delete this entry. Please try again.");
              } finally {
                setPending(false);
              }
            }}
          >
            {pending ? "Deleting…" : group ? "Delete recipe" : "Delete entry"}
          </button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
