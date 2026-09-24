"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { Icon } from "@/components/ui/icon";
import {
	Dialog,
	DialogContent,
	DialogDescription,
	DialogFooter,
	DialogHeader,
	DialogTitle,
	DialogTrigger,
} from "@/components/ui/dialog";
import {
	deleteHousehold,
	getHouseholdDeletionPreview,
	type HouseholdDeletionResult,
} from "@/data/household";
import { appToast } from "@/lib/toast";

function plural(count: number, one: string, many: string) {
	return `${count} ${count === 1 ? one : many}`;
}

/**
 * Owner-only. Every count shown here comes from the server, and the delete
 * sends back the preview's version: if members, lists or plans changed since,
 * the server refuses and the dialog shows the new state before asking again.
 */
export function DeleteHouseholdButton({ householdId }: { householdId: string }) {
	const router = useRouter();
	const [open, setOpen] = useState(false);
	const [state, setState] = useState<HouseholdDeletionResult | null>(null);
	const [notice, setNotice] = useState("");
	const [pending, startTransition] = useTransition();

	const openDialog = () => {
		setNotice("");
		setState(null);
		setOpen(true);
		startTransition(async () => setState(await getHouseholdDeletionPreview(householdId)));
	};

	const preview = state?.preview ?? null;
	const hasPlans = !!preview && (preview.shoppingListCount > 0 || preview.mealPlanCount > 0);
	const blocked = state?.status === "HasOtherMembers";
	const unavailable = state !== null && !preview;

	const confirm = () => {
		if (!preview || pending) return;
		startTransition(async () => {
			const result = await deleteHousehold(householdId, preview.version, hasPlans);
			if (result.status === "Deleted" || result.status === "NotFound") {
				setOpen(false);
				appToast.success(result.status === "Deleted" ? `Deleted ${preview.householdName}.` : "This household no longer exists.");
				router.push("/profile/household");
				router.refresh();
				return;
			}
			setState(result);
			setNotice(result.message ?? "Nothing was changed. Review the household and try again.");
		});
	};

	return (
		<Dialog
			open={open}
			onOpenChange={(next) => {
				if (next) openDialog();
				else if (!pending) setOpen(false);
			}}
		>
			<DialogTrigger asChild>
				<button type="button" className="btn-ghost h-9 text-sm text-destructive">
					<Icon name="trash" size={14} aria-hidden="true" />
					Delete
				</button>
			</DialogTrigger>
			<DialogContent className="max-w-md">
				<DialogHeader>
					<DialogTitle>{preview ? `Delete ${preview.householdName}?` : "Delete household"}</DialogTitle>
					<DialogDescription>
						{state === null
							? "Checking what this would remove…"
							: unavailable
								? state.message ?? "This household cannot be deleted."
								: blocked
									? `${plural(preview!.otherMemberCount, "other member", "other members")} still ${preview!.otherMemberCount === 1 ? "belongs" : "belong"} to this household. Remove ${preview!.otherMemberCount === 1 ? "them" : "every other member"} first, then delete it.`
									: "This cannot be undone. Recipes stay with the people who own them."}
					</DialogDescription>
				</DialogHeader>

				{preview && !blocked && hasPlans && (
					<div className="space-y-2 text-sm text-foreground">
						<p>These shared plans are deleted with the household:</p>
						<ul className="list-disc space-y-1 pl-5">
							{preview.shoppingListCount > 0 && (
								<li>
									{plural(preview.shoppingListCount, "shopping list", "shopping lists")} with{" "}
									{plural(preview.shoppingListItemCount, "item", "items")}
								</li>
							)}
							{preview.mealPlanCount > 0 && (
								<li>
									{plural(preview.mealPlanCount, "meal plan", "meal plans")} with{" "}
									{plural(preview.mealPlanRecipeCount, "planned meal", "planned meals")}
								</li>
							)}
						</ul>
						<p className="text-muted-foreground">Personal lists and plans are not affected.</p>
					</div>
				)}
				{preview && !blocked && preview.pendingInvitationCount > 0 && (
					<p className="text-sm text-muted-foreground">
						{plural(preview.pendingInvitationCount, "pending invitation is", "pending invitations are")} cancelled.
					</p>
				)}

				{notice && (
					<p role="alert" className="text-sm text-destructive">
						{notice}
					</p>
				)}

				<DialogFooter className="gap-2">
					<button type="button" onClick={() => setOpen(false)} disabled={pending} className="btn-ghost">
						{blocked || unavailable ? "Close" : "Cancel"}
					</button>
					{preview && !blocked && (
						<button type="button" onClick={confirm} disabled={pending} className="btn-danger">
							{pending ? "Deleting…" : hasPlans ? "Delete plans and household" : "Delete household"}
						</button>
					)}
				</DialogFooter>
			</DialogContent>
		</Dialog>
	);
}
