"use client";

import { useRouter } from "next/navigation";
import { useTransition } from "react";
import { AnimatedIcon } from "@/components/ui/animated-icon";
import { adminRemoveHouseholdMember } from "@/data/admin/household";
import type { HouseholdMemberDto } from "@/data/household";
import { appToast } from "@/lib/toast";

export default function AdminMemberRow({
	householdId,
	member,
}: {
	householdId: string;
	member: HouseholdMemberDto;
}) {
	const router = useRouter();
	const [pending, startTransition] = useTransition();

	const handleRemove = () => {
		if (!window.confirm(`Remove ${member.name ?? member.email} from this household?`)) return;
		startTransition(async () => {
			const result = await adminRemoveHouseholdMember(householdId, member.userId);
			if (result.success) {
				appToast.success("Member removed.");
				router.refresh();
			} else {
				appToast.error(result.message ?? "Could not remove member.");
			}
		});
	};

	return (
		<li className="flex items-center justify-between gap-3 py-3">
			<div className="min-w-0 flex-1">
				<p className="truncate text-sm font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">
					{member.name ?? "(no name)"}
				</p>
				<p className="truncate text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
					{member.email} · {member.role ?? "member"} · joined {new Date(member.joinedAt).toLocaleDateString()}
				</p>
			</div>
			<button
				type="button"
				onClick={handleRemove}
				disabled={pending}
				className="btn-ghost h-8 text-xs text-red-600 hover:bg-red-50 dark:hover:bg-red-950/30"
			>
				<AnimatedIcon name="logout" size={14} aria-hidden="true" />
				Remove
			</button>
		</li>
	);
}
