"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useTransition } from "react";
import { AnimatedIcon } from "@/components/ui/animated-icon";
import { adminDeleteHousehold } from "@/data/admin/household";
import { appToast } from "@/lib/toast";

export default function HouseholdRowActions({ id, name }: { id: string; name: string }) {
	const router = useRouter();
	const [pending, startTransition] = useTransition();

	const handleDelete = () => {
		if (!window.confirm(`Delete "${name}"? This cascades members and cannot be undone.`)) return;
		startTransition(async () => {
			const result = await adminDeleteHousehold(id);
			if (result.success) {
				appToast.success("Household deleted.");
				router.refresh();
			} else {
				appToast.error(result.message ?? "Delete failed.");
			}
		});
	};

	return (
		<div className="flex items-center justify-end gap-2">
			<Link href={`/admin/households/${id}`} className="btn-ghost h-8 text-xs">
				<AnimatedIcon name="search" size={14} aria-hidden="true" />
				Inspect
			</Link>
			<button
				type="button"
				onClick={handleDelete}
				disabled={pending}
				className="btn-ghost h-8 text-xs text-red-600 hover:bg-red-50 dark:hover:bg-red-950/30"
			>
				<AnimatedIcon name="logout" size={14} aria-hidden="true" />
				Delete
			</button>
		</div>
	);
}
