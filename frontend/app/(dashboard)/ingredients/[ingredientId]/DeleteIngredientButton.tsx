"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { clientApi } from "@/lib/api.client";

export default function DeleteIngredientButton({ id }: { id: string }) {
    const router = useRouter();
    const [isDeleting, setIsDeleting] = useState(false);

    const handleDelete = async () => {
        if (!confirm("Are you sure you want to delete this ingredient? This cannot be undone.")) return;

        setIsDeleting(true);
        try {
            await clientApi(`/api/Foods/${id}`, { method: "DELETE" });
            router.push("/ingredients");
            router.refresh();
        } catch {
            alert("Failed to delete ingredient");
        } finally {
            setIsDeleting(false);
        }
    };

    return (
        <button
            onClick={handleDelete}
            disabled={isDeleting}
            className="btn-secondary text-sm px-3 py-1.5 text-red-600 hover:bg-red-50 disabled:opacity-50"
        >
            <i className={isDeleting ? "ri-loader-4-line animate-spin" : "ri-delete-bin-line"} />
            Delete
        </button>
    );
}
