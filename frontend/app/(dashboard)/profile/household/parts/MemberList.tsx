"use client";

import { useRouter } from "next/navigation";
import { useTransition } from "react";
import { AnimatedIcon } from "@/components/ui/animated-icon";
import {
	removeHouseholdMember,
	respondToInvitation,
	type HouseholdMemberDto,
	type PendingInviteAdminDto,
} from "@/data/household";
import { appToast } from "@/lib/toast";

export function MemberList({
	householdId,
	members,
	pendingInvites,
	canManage,
}: {
	householdId: string;
	members: HouseholdMemberDto[];
	pendingInvites: PendingInviteAdminDto[];
	canManage: boolean;
}) {
	const router = useRouter();
	const [pending, startTransition] = useTransition();

	const handleRemove = (userId: string, name: string | null | undefined) => {
		if (!window.confirm(`Remove ${name ?? "this member"} from the household?`)) return;
		startTransition(async () => {
			const result = await removeHouseholdMember(householdId, userId);
			if (result.success) {
				appToast.success("Member removed.");
				router.refresh();
			} else {
				appToast.error(result.message ?? "Could not remove member.");
			}
		});
	};

	const handleRevoke = (invitationId: string) => {
		startTransition(async () => {
			const result = await respondToInvitation(invitationId, "revoke");
			if (result.success) {
				appToast.success("Invitation revoked.");
				router.refresh();
			} else {
				appToast.error(result.message ?? "Could not revoke invitation.");
			}
		});
	};

	return (
		<div className="space-y-4">
			<ul className="divide-y divide-charcoal-blue-100 dark:divide-white/10">
				{members.map((m) => (
					<li key={m.userId} className="flex items-center justify-between gap-3 py-3">
						<div className="min-w-0 flex-1">
							<p className="truncate text-sm font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">
								{m.name ?? m.email ?? "Unknown"}
							</p>
							<p className="truncate text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
								{m.email} · {m.role ?? "member"}
							</p>
						</div>
						{canManage && m.role !== "owner" && (
							<button
								type="button"
								className="btn-ghost h-8 text-xs text-red-600 hover:bg-red-50 dark:hover:bg-red-950/30"
								onClick={() => handleRemove(m.userId, m.name)}
								disabled={pending}
							>
								<AnimatedIcon name="logout" size={14} aria-hidden="true" />
								Remove
							</button>
						)}
					</li>
				))}
			</ul>

			{pendingInvites.length > 0 && (
				<div>
					<h3 className="mb-2 text-xs font-semibold uppercase tracking-[0.14em] text-charcoal-blue-500 dark:text-charcoal-blue-400">
						Pending invitations
					</h3>
					<ul className="space-y-2">
						{pendingInvites.map((inv) => (
							<li
								key={inv.id}
								className="flex items-center justify-between gap-3 rounded-2xl border border-dashed border-charcoal-blue-200 bg-charcoal-blue-50/40 px-3 py-2 text-sm dark:border-white/10 dark:bg-charcoal-blue-900/40"
							>
								<div>
									<p className="font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">
										{inv.invitedName ?? inv.invitedEmail}
									</p>
									<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
										{inv.invitedEmail} · {inv.role} · expires {new Date(inv.expiresAt).toLocaleDateString()}
									</p>
								</div>
								{canManage && (
									<button
										type="button"
										className="btn-ghost h-8 text-xs"
										onClick={() => handleRevoke(inv.id)}
										disabled={pending}
									>
										Revoke
									</button>
								)}
							</li>
						))}
					</ul>
				</div>
			)}
		</div>
	);
}
