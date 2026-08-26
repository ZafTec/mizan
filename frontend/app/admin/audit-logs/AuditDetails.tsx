"use client";

import { useState } from "react";
import { ModalShell } from "@/components/ModalShell";

/**
 * The details blob, on demand.
 *
 * It is a JSON payload of whatever the command carried, which is far too wide
 * for a table cell and occasionally long enough to be a page of its own.
 */
export default function AuditDetails({ details }: { details: string }) {
	const [open, setOpen] = useState(false);

	let pretty = details;
	try {
		pretty = JSON.stringify(JSON.parse(details), null, 2);
	} catch {
		// Not JSON. Show it as it was written rather than not at all.
	}

	return (
		<>
			<button
				type="button"
				onClick={() => setOpen(true)}
				className="whitespace-nowrap text-xs text-verdigris-700 hover:underline dark:text-verdigris-300"
			>
				Details
			</button>

			<ModalShell open={open} onClose={() => setOpen(false)}>
				<div className="surface-panel w-full max-w-2xl space-y-3 p-5">
					<h2 className="text-base font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
						Audit entry
					</h2>
					<pre className="max-h-[60vh] overflow-auto whitespace-pre-wrap rounded-2xl bg-charcoal-blue-50 p-4 font-mono text-xs leading-relaxed text-charcoal-blue-800 dark:bg-charcoal-blue-950 dark:text-charcoal-blue-200">
						{pretty}
					</pre>
				</div>
			</ModalShell>
		</>
	);
}
