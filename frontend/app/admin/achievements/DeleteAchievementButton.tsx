"use client";

import { useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import { clientApi } from "@/lib/api.client";
import { ModalShell } from "@/components/ModalShell";
import { appToast } from "@/lib/toast";

/**
 * Deleting an achievement people have already earned takes their badge with
 * it, so the count is in the confirmation rather than only in the table.
 */
export default function DeleteAchievementButton({
	id,
	name,
	unlockedBy,
}: {
	id: string;
	name: string;
	unlockedBy: number;
}) {
	const router = useRouter();
	const [open, setOpen] = useState(false);
	const [pending, startTransition] = useTransition();

	function remove() {
		startTransition(async () => {
			try {
				await clientApi(`/api/Achievements/${id}`, { method: "DELETE" });
				appToast.success(`Deleted "${name}"`);
				setOpen(false);
				router.refresh();
			} catch (error) {
				appToast.error(error, "Could not delete that achievement");
			}
		});
	}

	return (
		<>
			<button
				type="button"
				onClick={() => setOpen(true)}
				className="text-xs text-red-600 hover:underline dark:text-red-400"
			>
				Delete
			</button>

			<ModalShell open={open} onClose={() => setOpen(false)}>
				<div className="surface-panel w-full max-w-md space-y-4 p-5">
					<div className="space-y-1">
						<h2 className="text-base font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
							Delete &ldquo;{name}&rdquo;?
						</h2>
						<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
							{unlockedBy === 0
								? "Nobody has earned this one, so nothing is lost."
								: `${unlockedBy} ${unlockedBy === 1 ? "person has" : "people have"} earned this. Deleting it removes their badge and its points.`}
						</p>
					</div>

					<div className="flex gap-2">
						<button
							type="button"
							onClick={() => setOpen(false)}
							disabled={pending}
							className="btn-ghost flex-1"
						>
							Cancel
						</button>
						<button type="button" onClick={remove} disabled={pending} className="btn-danger flex-1">
							{pending ? "Deleting…" : "Delete"}
						</button>
					</div>
				</div>
			</ModalShell>
		</>
	);
}
