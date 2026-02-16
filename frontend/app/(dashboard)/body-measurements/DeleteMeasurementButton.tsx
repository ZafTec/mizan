"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { clientApi } from "@/lib/api.client";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";

export default function DeleteMeasurementButton({ id }: { id: string }) {
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);

  const handleDelete = async () => {
    setIsDeleting(true);
    try {
      await clientApi(`/api/BodyMeasurements/${id}`, { method: "DELETE" });
      setOpen(false);
      router.refresh();
    } catch {
      alert("Failed to delete measurement");
    } finally {
      setIsDeleting(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <button
          className="text-slate-400 dark:text-slate-500 hover:text-red-500 transition-colors disabled:opacity-50"
          title="Delete measurement"
        >
          <i className="ri-delete-bin-line" />
        </button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Delete Measurement</DialogTitle>
          <DialogDescription>
            Are you sure you want to delete this measurement? This action cannot be undone.
          </DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button variant="outline" onClick={() => setOpen(false)} disabled={isDeleting}>
            Cancel
          </Button>
          <Button variant="destructive" onClick={handleDelete} disabled={isDeleting}>
            {isDeleting ? (
              <>
                <i className="ri-loader-4-line animate-spin mr-2" />
                Deleting...
              </>
            ) : (
              "Delete"
            )}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
