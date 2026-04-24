"use server";

import { serverApi } from "@/lib/api.server";
import { logger } from "@/lib/logger";
import { revalidatePath } from "next/cache";

const householdLogger = logger.createModuleLogger("household");

export interface HouseholdSummary {
	id: string;
	name: string;
	myRole: string;
	memberCount: number;
	joinedAt: string;
	isActive: boolean;
}

export interface HouseholdInvitationSummary {
	id: string;
	householdId: string;
	householdName: string;
	role: string;
	invitedByName: string;
	createdAt: string;
	expiresAt: string;
}

export interface MyHouseholds {
	households: HouseholdSummary[];
	pendingInvitations: HouseholdInvitationSummary[];
	activeHouseholdId?: string | null;
}

export interface HouseholdMemberDto {
	userId: string;
	name?: string | null;
	email?: string | null;
	role?: string | null;
	joinedAt: string;
}

export interface HouseholdDetail {
	id: string;
	name: string;
	createdBy?: string | null;
	createdAt: string;
	members: HouseholdMemberDto[];
}

export interface PendingInviteAdminDto {
	id: string;
	householdId: string;
	invitedEmail: string;
	invitedName?: string | null;
	role: string;
	status: string;
	createdAt: string;
	expiresAt: string;
}

export async function getMyHouseholds(): Promise<MyHouseholds> {
	try {
		return await serverApi<MyHouseholds>("/api/Households/mine");
	} catch (error) {
		householdLogger.error("Failed to load households", { error });
		return { households: [], pendingInvitations: [], activeHouseholdId: null };
	}
}

export async function getHousehold(id: string): Promise<HouseholdDetail | null> {
	try {
		return await serverApi<HouseholdDetail>(`/api/Households/${id}`);
	} catch (error) {
		householdLogger.error("Failed to load household", { error, id });
		return null;
	}
}

export async function getHouseholdPendingInvites(id: string): Promise<PendingInviteAdminDto[]> {
	try {
		return await serverApi<PendingInviteAdminDto[]>(`/api/Households/${id}/invitations`);
	} catch (error) {
		householdLogger.error("Failed to load pending invites", { error, id });
		return [];
	}
}

export async function createHousehold(name: string): Promise<{ id: string } | null> {
	try {
		const id = await serverApi<string>("/api/Households", {
			method: "POST",
			body: { name },
		});
		revalidatePath("/settings/household");
		return { id };
	} catch (error) {
		householdLogger.error("Create household failed", { error });
		return null;
	}
}

export async function inviteToHousehold(householdId: string, email: string, role: "admin" | "member" = "member") {
	try {
		const result = await serverApi<{ success: boolean; invitationId?: string; message?: string }>(
			`/api/Households/${householdId}/invitations`,
			{ method: "POST", body: { email, role } },
		);
		revalidatePath("/settings/household");
		return result;
	} catch (error) {
		householdLogger.error("Invite failed", { error, householdId });
		return { success: false, message: "Could not send invitation." };
	}
}

export async function respondToInvitation(invitationId: string, action: "accept" | "decline" | "revoke") {
	try {
		const result = await serverApi<{ success: boolean; message?: string; householdId?: string }>(
			`/api/Households/invitations/${invitationId}/respond`,
			{ method: "POST", body: { action } },
		);
		revalidatePath("/settings/household");
		revalidatePath("/");
		return result;
	} catch (error) {
		householdLogger.error("Respond to invitation failed", { error, invitationId });
		return { success: false, message: "Could not respond to invitation." };
	}
}

export async function setActiveHousehold(householdId: string | null) {
	try {
		const result = await serverApi<{ success: boolean; activeHouseholdId?: string | null; message?: string }>(
			"/api/Households/active",
			{ method: "PUT", body: { householdId } },
		);
		revalidatePath("/");
		return result;
	} catch (error) {
		householdLogger.error("Set active household failed", { error, householdId });
		return { success: false, message: "Could not switch household." };
	}
}

export async function leaveHousehold(householdId: string) {
	try {
		const result = await serverApi<{ success: boolean; message?: string }>(
			`/api/Households/${householdId}/leave`,
			{ method: "POST" },
		);
		revalidatePath("/settings/household");
		return result;
	} catch (error) {
		householdLogger.error("Leave household failed", { error, householdId });
		return { success: false, message: "Could not leave household." };
	}
}

export async function removeHouseholdMember(householdId: string, userId: string) {
	try {
		const result = await serverApi<{ success: boolean; message?: string }>(
			`/api/Households/${householdId}/members/${userId}`,
			{ method: "DELETE" },
		);
		revalidatePath(`/settings/household`);
		return result;
	} catch (error) {
		householdLogger.error("Remove member failed", { error, householdId, userId });
		return { success: false, message: "Could not remove member." };
	}
}
