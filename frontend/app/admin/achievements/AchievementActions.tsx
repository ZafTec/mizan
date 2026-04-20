"use client";

import { useState, useTransition } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import { deleteAchievementAction } from "@/data/admin/achievement";

export default function AchievementActions({ id, name }: { id: string; name: string }) {
    const [pending, startTransition] = useTransition();
    const [confirming, setConfirming] = useState(false);
    const router = useRouter();

    function handleDelete() {
        startTransition(async () => {
            const res = await deleteAchievementAction(id);
            if (res.success) {
                toast.success(`Deleted "${name}"`);
                router.refresh();
            } else {
                toast.error(res.message ?? "Delete failed");
            }
            setConfirming(false);
        });
    }

    return (
        <div className="flex items-center justify-end gap-2">
            <Link
                href={`/admin/achievements/${id}/edit`}
                className="text-sm text-brand-700 hover:underline dark:text-brand-200"
            >
                Edit
            </Link>
            {confirming ? (
                <div className="flex items-center gap-2">
                    <button
                        type="button"
                        onClick={handleDelete}
                        disabled={pending}
                        className="text-sm font-semibold text-red-600 hover:underline dark:text-red-300 disabled:opacity-60"
                    >
                        {pending ? "Deleting..." : "Confirm"}
                    </button>
                    <button
                        type="button"
                        onClick={() => setConfirming(false)}
                        disabled={pending}
                        className="text-sm text-charcoal-blue-500 hover:underline dark:text-charcoal-blue-400"
                    >
                        Cancel
                    </button>
                </div>
            ) : (
                <button
                    type="button"
                    onClick={() => setConfirming(true)}
                    className="text-sm text-red-600 hover:underline dark:text-red-300"
                >
                    Delete
                </button>
            )}
        </div>
    );
}
