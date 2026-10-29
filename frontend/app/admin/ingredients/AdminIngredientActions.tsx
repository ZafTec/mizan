"use client";

import Link from "next/link";
import { Pencil, Trash2 } from "lucide-react";
import { useState } from "react";
import { deleteIngredient } from "@/data/ingredient";
import ConfirmationModal from "@/components/ConfirmationModal";
import { useRouter } from "next/navigation";
import { appToast } from "@/lib/toast";

interface AdminIngredientActionsProps {
  id: string;
  name: string;
}

export default function AdminIngredientActions({
  id,
  name,
}: AdminIngredientActionsProps) {
  const [isDeleteModalOpen, setIsDeleteModalOpen] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);
  const router = useRouter();

  const handleDelete = async () => {
    setIsDeleting(true);
    try {
      const result = await deleteIngredient(id);
      if (result.success) {
        appToast.success("Ingredient deleted");
        router.refresh();
        setIsDeleteModalOpen(false);
      } else {
        appToast.error(result.message || "Failed to delete ingredient");
      }
    } catch (error) {
      console.error("Delete error:", error);
      appToast.error(error, "An error occurred while deleting");
    } finally {
      setIsDeleting(false);
    }
  };

  return (
    <div className="flex items-center justify-end gap-2">
      <Link
        href={`/admin/ingredients/${id}/edit`}
        className="icon-button"
        aria-label="Edit ingredient"
      >
        <Pencil size={15} aria-hidden="true" />
      </Link>
      <button
        type="button"
        onClick={() => setIsDeleteModalOpen(true)}
        className="icon-button-destructive"
        aria-label="Delete ingredient"
      >
        <Trash2 size={15} aria-hidden="true" />
      </button>

      <ConfirmationModal
        isOpen={isDeleteModalOpen}
        onClose={() => setIsDeleteModalOpen(false)}
        onConfirm={handleDelete}
        title="Delete Ingredient"
        message={`Are you sure you want to delete "${name}"? This action cannot be undone.`}
        confirmText="Delete Ingredient"
        isDanger
        isLoading={isDeleting}
      />
    </div>
  );
}
