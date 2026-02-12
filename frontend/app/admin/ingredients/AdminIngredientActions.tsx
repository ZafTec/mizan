'use client';

import Link from "next/link";
import { useState } from "react";
import { deleteIngredient } from "@/data/ingredient";
import ConfirmationModal from "@/components/ConfirmationModal";
import { useRouter } from "next/navigation";
import { toast } from "sonner";

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
                router.refresh();
                setIsDeleteModalOpen(false);
            } else {
                toast.error(result.message || "Failed to delete ingredient");
            }
        } catch (error) {
            console.error("Delete error:", error);
            toast.error("An error occurred while deleting");
        } finally {
            setIsDeleting(false);
        }
    };

    return (
        <div className="flex items-center justify-end gap-2">
            <Link
                href={`/admin/ingredients/${id}/edit`}
                className="w-8 h-8 rounded-lg bg-slate-100 hover:bg-brand-50 hover:text-brand-600 flex items-center justify-center transition-colors"
                title="Edit Ingredient"
            >
                <i className="ri-edit-line" />
            </Link>
            <button
                onClick={() => setIsDeleteModalOpen(true)}
                className="w-8 h-8 rounded-lg bg-slate-100 hover:bg-red-50 hover:text-red-600 flex items-center justify-center transition-colors text-slate-500"
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
