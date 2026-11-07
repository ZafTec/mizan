"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { clientApi } from "@/lib/api.client";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { appToast } from "@/lib/toast";

export default function DeleteIngredientButton({ id }: { id: string }) {
  const router = useRouter();
  const [confirmOpen, setConfirmOpen] = useState(false);

  const handleDelete = async () => {
    await clientApi(`/api/Foods/${id}`, { method: "DELETE" });
    appToast.success("Ingredient deleted");
    router.push("/ingredients");
    router.refresh();
  };

  return (
    <>
      <button
        onClick={() => setConfirmOpen(true)}
        type="button"
        className="btn-secondary-destructive"
      >
        Delete
      </button>

      <ConfirmDialog
        open={confirmOpen}
        onClose={() => setConfirmOpen(false)}
        onConfirm={handleDelete}
        title="Delete this ingredient?"
        description="This ingredient is removed from your foods. This cannot be undone."
        confirmLabel="Delete ingredient"
        pendingLabel="Deleting…"
        errorFallback="Failed to delete ingredient"
      />
    </>
  );
}
