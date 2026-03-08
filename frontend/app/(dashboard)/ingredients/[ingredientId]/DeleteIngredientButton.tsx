"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { clientApi } from "@/lib/api.client";
import ConfirmationModal from "@/components/ConfirmationModal";
import { appToast } from "@/lib/toast";

export default function DeleteIngredientButton({ id }: { id: string }) {
    const router = useRouter();
    const [isDeleting, setIsDeleting] = useState(false);
    const [confirmOpen, setConfirmOpen] = useState(false);

    const handleDelete = async () => {
        setIsDeleting(true);
        try {
            await clientApi(`/api/Foods/${id}`, { method: "DELETE" });
            appToast.success("Ingredient deleted");
            router.push("/ingredients");
            router.refresh();
	        } catch (error) {
	            appToast.error(error, "Failed to delete ingredient");
        } finally {
            setIsDeleting(false);
	            setConfirmOpen(false);
        }
    };

    return (
	        <>
	            <button
	                onClick={() => setConfirmOpen(true)}
	                disabled={isDeleting}
	                className="btn-secondary text-sm px-3 py-1.5 text-red-600 hover:bg-red-50 disabled:opacity-50"
	            >
	                <i className={isDeleting ? "ri-loader-4-line animate-spin" : "ri-delete-bin-line"} />
	                Delete
	            </button>

	            <ConfirmationModal
	                isOpen={confirmOpen}
	                onClose={() => setConfirmOpen(false)}
	                onConfirm={handleDelete}
	                title="Delete Ingredient"
	                message="Are you sure you want to delete this ingredient? This cannot be undone."
	                confirmText="Delete Ingredient"
	                isDanger
	                isLoading={isDeleting}
	            />
	        </>
    );
}
