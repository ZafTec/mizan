"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { AnimatedIcon } from "@/components/ui/animated-icon";
import { inviteToHousehold } from "@/data/household";
import { appToast } from "@/lib/toast";

export function InviteForm({ householdId }: { householdId: string }) {
	const router = useRouter();
	const [email, setEmail] = useState("");
	const [role, setRole] = useState<"admin" | "member">("member");
	const [pending, startTransition] = useTransition();

	const handleSubmit = (e: React.FormEvent<HTMLFormElement>) => {
		e.preventDefault();
		const trimmed = email.trim();
		if (!trimmed) return;
		startTransition(async () => {
			const result = await inviteToHousehold(householdId, trimmed, role);
			if (result.success) {
				appToast.success("Invitation sent.");
				setEmail("");
				router.refresh();
			} else {
				appToast.error(result.message ?? "Could not send invitation.");
			}
		});
	};

	return (
		<form onSubmit={handleSubmit} className="flex flex-col gap-3 sm:flex-row sm:items-center">
			<input
				type="email"
				required
				maxLength={255}
				value={email}
				onChange={(e) => setEmail(e.target.value)}
				placeholder="friend@example.com"
				className="input h-10 flex-1"
				aria-label="Invitee email"
				disabled={pending}
			/>
			<select
				className="input h-10 sm:w-40"
				value={role}
				onChange={(e) => setRole(e.target.value as "admin" | "member")}
				aria-label="Role"
				disabled={pending}
			>
				<option value="member">Member</option>
				<option value="admin">Admin</option>
			</select>
			<button type="submit" className="btn-primary h-10" disabled={pending || email.trim().length === 0}>
				<AnimatedIcon name="sparkles" size={16} aria-hidden="true" />
				{pending ? "Sending..." : "Invite"}
			</button>
		</form>
	);
}
