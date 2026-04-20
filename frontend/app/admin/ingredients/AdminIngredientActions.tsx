'use client';

import Link from "next/link";
import { useState } from "react";
import { deleteIngredient } from "@/data/ingredient";
import ConfirmationModal from "@/components/ConfirmationModal";
import { useRouter } from "next/navigation";
import { appToast } from "@/lib/toast";

interface AdminIngredientActionsProps {
    id: string;
    name: string;
}

export default function AdminIngredientActions({ id, name }: AdminIngredientActionsProps) {
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
	                className="flex h-8 w-8 items-center justify-center rounded-lg bg-charcoal-blue-100 transition-colors hover:bg-brand-50 hover:text-brand-600 dark:bg-charcoal-blue-900/60 dark:text-charcoal-blue-300 dark:hover:bg-brand-950/50 dark:hover:text-brand-300"
                title="Edit Ingredient"
            >
                <i className="ri-edit-line" />
            </Link>
            <button
                onClick={() => setIsDeleteModalOpen(true)}
	                className="flex h-8 w-8 items-center justify-center rounded-lg bg-charcoal-blue-100 text-charcoal-blue-500 transition-colors hover:bg-red-50 hover:text-red-600 dark:bg-charcoal-blue-900/60 dark:text-charcoal-blue-300 dark:hover:bg-red-500/10 dark:hover:text-red-300"
                title="Delete Ingredient"
            >
                <i className="ri-delete-bin-line" />
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
