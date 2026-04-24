"use client";

import { useRouter } from "next/navigation";
import { useTransition } from "react";
import { AnimatedIcon } from "@/components/ui/animated-icon";
import { leaveHousehold } from "@/data/household";
import { appToast } from "@/lib/toast";

export function LeaveHouseholdButton({
	householdId,
	disabled,
}: {
	householdId: string;
	disabled?: boolean;
}) {
	const router = useRouter();
	const [pending, startTransition] = useTransition();

	const handleClick = () => {
		if (!window.confirm("Leave this household? You'll lose access to shared recipes and meal plans.")) return;
		startTransition(async () => {
			const result = await leaveHousehold(householdId);
			if (result.success) {
				appToast.success("Left household.");
				router.push("/profile/household");
				router.refresh();
			} else {
				appToast.error(result.message ?? "Could not leave household.");
			}
		});
	};

	return (
		<button
			type="button"
			onClick={handleClick}
			disabled={disabled || pending}
			className="btn-ghost h-9 text-sm"
		>
			<AnimatedIcon name="logout" size={14} aria-hidden="true" />
			Leave
		</button>
	);
}
