"use client";

import { useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import { clientApi } from "@/lib/api.client";
import { ModalShell } from "@/components/ModalShell";
import { appToast } from "@/lib/toast";

/**
 * Ending someone else's coaching relationship is not a click you want to make
 * by accident, so it asks - and it says whose access is about to stop.
 */
export default function EndRelationshipButton({
	id,
	trainer,
	client,
}: {
	id: string;
	trainer: string;
	client: string;
}) {
	const router = useRouter();
	const [open, setOpen] = useState(false);
	const [reason, setReason] = useState("");
	const [pending, startTransition] = useTransition();

	function end() {
		startTransition(async () => {
			try {
				await clientApi(`/api/Admin/Relationships/${id}/end`, {
					method: "POST",
					body: { reason: reason.trim() || null },
				});
				appToast.success("Relationship ended");
				setOpen(false);
				router.refresh();
			} catch (error) {
				appToast.error(error, "Could not end the relationship");
			}
		});
	}

	return (
		<>
			<button
				type="button"
				onClick={() => setOpen(true)}
				className="whitespace-nowrap text-xs text-red-600 hover:underline dark:text-red-400"
			>
				End
			</button>

			<ModalShell open={open} onClose={() => setOpen(false)}>
				<div className="surface-panel w-full max-w-md space-y-4 p-5">
					<div className="space-y-1">
						<h2 className="text-base font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
							End this relationship?
						</h2>
						<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
							{trainer} loses access to {client}&apos;s log on their next request.
							The client&apos;s sharing choices are kept, so re-accepting restores
							what they had.
						</p>
					</div>

					<label className="block space-y-1">
						<span className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
							Reason (recorded in the audit log)
						</span>
						<input
							value={reason}
							onChange={(e) => setReason(e.target.value)}
							placeholder="Client asked by email"
							className="input w-full !py-2 text-sm"
						/>
					</label>

					<div className="flex gap-2">
						<button
							type="button"
							onClick={() => setOpen(false)}
							disabled={pending}
							className="btn-ghost flex-1"
						>
							Cancel
						</button>
						<button type="button" onClick={end} disabled={pending} className="btn-danger flex-1">
							{pending ? "Ending…" : "End relationship"}
						</button>
					</div>
				</div>
			</ModalShell>
		</>
	);
}
