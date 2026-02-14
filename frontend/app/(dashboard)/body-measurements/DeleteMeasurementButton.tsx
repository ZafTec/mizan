"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { clientApi } from "@/lib/api.client";

export default function DeleteMeasurementButton({ id }: { id: string }) {
    const router = useRouter();
    const [isDeleting, setIsDeleting] = useState(false);

    const handleDelete = async () => {
        if (!confirm("Are you sure you want to delete this measurement?")) return;

        setIsDeleting(true);
        try {
            await clientApi(`/api/BodyMeasurements/${id}`, { method: "DELETE" });
            router.refresh();
        } catch {
            alert("Failed to delete measurement");
        } finally {
            setIsDeleting(false);
        }
    };

    return (
        <button
            onClick={handleDelete}
            disabled={isDeleting}
            className="text-slate-400 hover:text-red-500 transition-colors disabled:opacity-50"
            title="Delete measurement"
        >
            <i className={isDeleting ? "ri-loader-4-line animate-spin" : "ri-delete-bin-line"} />
        </button>
    );
}
