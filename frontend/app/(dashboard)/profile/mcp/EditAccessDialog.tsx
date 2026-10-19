"use client";

import { useEffect, useMemo, useState } from "react";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { HouseholdPicker, PermissionPicker } from "@/components/mcp/PermissionPicker";
import { fetchMyHouseholds, type HouseholdChoice } from "@/lib/api/mcp";
import { levelsFromScopes, scopesFromLevels, type AccessLevels, type HouseholdMode } from "@/lib/mcp-permissions";
import type { McpConnection, ScopeGroup } from "@/types/mcp";

/**
 * Takes access away from a connected app. It cannot add any: the controls stop
 * at what the app already holds, and the server refuses anything more. To give
 * an app more, it connects again and the person sees the consent screen.
 */
export function EditAccessDialog({
	connection,
	groups,
	onClose,
	onSave,
}: {
	connection: McpConnection;
	groups: ScopeGroup[];
	onClose: () => void;
	onSave: (change: { scopes: string[] } & HouseholdChoice) => Promise<void>;
}) {
	const held = useMemo(() => levelsFromScopes(groups, connection.scopes ?? []), [groups, connection.scopes]);
	const currentMode = (connection.householdMode ?? "none") as HouseholdMode;
	const [levels, setLevels] = useState<AccessLevels>(held);
	const [mode, setMode] = useState<HouseholdMode>(currentMode);
	const [selected, setSelected] = useState<string[]>((connection.households ?? []).map((h) => h.id!));
	const [all, setAll] = useState<{ id: string; name: string }[] | null>(null);
	const [saving, setSaving] = useState(false);

	// An app that sees every household can be narrowed to some of them, so those need names.
	useEffect(() => {
		if (currentMode === "all") void fetchMyHouseholds().then(setAll).catch(() => setAll([]));
	}, [currentMode]);

	const households =
		currentMode === "all"
			? (all ?? [])
			: (connection.households ?? []).map((h) => ({ id: h.id!, name: h.name! }));

	const scopes = scopesFromLevels(groups, levels);

	return (
		<Dialog open onOpenChange={(open) => !open && onClose()}>
			<DialogContent className="max-h-[90vh] max-w-2xl overflow-y-auto">
				<DialogHeader>
					<DialogTitle>Edit access for {connection.clientName}</DialogTitle>
					<DialogDescription>
						You can remove access here. To give the app more, connect it again from the app.
					</DialogDescription>
				</DialogHeader>

				<PermissionPicker groups={groups} levels={levels} onChange={setLevels} ceiling={held} />

				{currentMode !== "none" && (
					<HouseholdPicker
						households={households}
						mode={mode}
						selected={selected}
						onChange={(nextMode, ids) => {
							setMode(nextMode);
							setSelected(ids);
						}}
						ceilingMode={currentMode}
					/>
				)}

				<DialogFooter className="flex justify-end gap-2">
					<button type="button" className="btn-secondary" onClick={onClose}>
						Cancel
					</button>
					<button
						type="button"
						className="btn-primary"
						disabled={saving || scopes.length === 0}
						onClick={async () => {
							setSaving(true);
							await onSave({ scopes, householdMode: mode, householdIds: selected });
							setSaving(false);
						}}
					>
						{scopes.length === 0 ? "Keep at least one permission" : "Save"}
					</button>
				</DialogFooter>
			</DialogContent>
		</Dialog>
	);
}
