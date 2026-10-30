"use client";

import Link from "next/link";
import { Pencil, Trash2 } from "lucide-react";
import { useRouter } from "next/navigation";
import { deleteIngredient } from "@/data/ingredient";
import { ConfirmButton } from "@/components/ConfirmDialog";
import { appToast } from "@/lib/toast";

interface AdminIngredientActionsProps {
  id: string;
  name: string;
}

export default function AdminIngredientActions({
  id,
  name,
}: AdminIngredientActionsProps) {
  const router = useRouter();

  return (
    <div className="flex items-center justify-end gap-2">
      <Link
        href={`/admin/ingredients/${id}/edit`}
        className="icon-button"
        aria-label="Edit ingredient"
      >
        <Pencil size={15} aria-hidden="true" />
      </Link>
      <ConfirmButton
        className="icon-button-destructive"
        aria-label="Delete ingredient"
        title={`Delete “${name}”?`}
        description="The ingredient is removed from the catalogue. This cannot be undone."
        confirmLabel="Delete ingredient"
        pendingLabel="Deleting…"
        errorFallback="An error occurred while deleting"
        onConfirm={async () => {
          const result = await deleteIngredient(id);
          if (!result.success)
            throw new Error(result.message || "Failed to delete ingredient");
          appToast.success("Ingredient deleted");
          router.refresh();
        }}
      >
        <Trash2 size={15} aria-hidden="true" />
      </ConfirmButton>
    </div>
  );
}
