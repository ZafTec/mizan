"use client";

import { useTransition } from "react";
import { useRouter } from "next/navigation";
import { clientApi } from "@/lib/api.client";
import { appToast } from "@/lib/toast";
import type { AdminJob } from "@/data/admin/jobs";

/**
 * Retry and discard. Both only make sense on a job that has stopped, so
 * neither appears on one that is still going - the alternative is an operator
 * discovering the rule from a 400.
 */
export default function JobActions({ job }: { job: AdminJob }) {
	const router = useRouter();
	const [pending, startTransition] = useTransition();

	const retryable = job.status === "DeadLettered" || job.status === "Failed";
	const deletable = job.status === "DeadLettered" || job.status === "Succeeded";

	function act(path: string, method: "POST" | "DELETE", done: string, failed: string) {
		startTransition(async () => {
			try {
				await clientApi(path, { method });
				appToast.success(done);
				router.refresh();
			} catch (error) {
				appToast.error(error, failed);
			}
		});
	}

	if (!retryable && !deletable) return null;

	return (
		<span className="inline-flex gap-3 whitespace-nowrap">
			{retryable && (
				<button
					type="button"
					disabled={pending}
					onClick={() =>
						act(
							`/api/Admin/Jobs/${job.id}/retry`,
							"POST",
							"Requeued",
							"Could not requeue the job",
						)
					}
					className="text-xs text-verdigris-700 hover:underline disabled:opacity-50 dark:text-verdigris-400"
				>
					Retry
				</button>
			)}
			{deletable && (
				<button
					type="button"
					disabled={pending}
					onClick={() =>
						act(
							`/api/Admin/Jobs/${job.id}`,
							"DELETE",
							"Job discarded",
							"Could not discard the job",
						)
					}
					className="text-xs text-red-600 hover:underline disabled:opacity-50 dark:text-red-400"
				>
					Discard
				</button>
			)}
		</span>
	);
}
