"use server";

import { serverApi } from "@/lib/api.server";
import { logger } from "@/lib/logger";
import { revalidatePath } from "next/cache";

const adminHouseholdLogger = logger.createModuleLogger("admin-household");

export interface AdminHouseholdSummary {
	id: string;
	name: string;
	createdBy: string;
	createdByName?: string | null;
	createdByEmail?: string | null;
	createdAt: string;
	memberCount: number;
	pendingInviteCount: number;
}

export interface AdminHouseholdList {
	items: AdminHouseholdSummary[];
	totalCount: number;
	totalPages: number;
	page: number;
	pageSize: number;
}

export interface AdminHouseholdListQuery {
	page?: number;
	pageSize?: number;
	searchTerm?: string;
	sortBy?: string;
	sortOrder?: "asc" | "desc";
}

export async function listHouseholdsAdmin(query: AdminHouseholdListQuery = {}): Promise<AdminHouseholdList> {
	try {
		const params = new URLSearchParams({
			Page: String(query.page ?? 1),
			PageSize: String(query.pageSize ?? 20),
		});
		if (query.searchTerm) params.set("SearchTerm", query.searchTerm);
		if (query.sortBy) params.set("SortBy", query.sortBy);
		if (query.sortOrder) params.set("SortOrder", query.sortOrder);

		const result = await serverApi<AdminHouseholdList>(`/api/Households/admin/all?${params}`);
		return {
			items: result.items ?? [],
			totalCount: result.totalCount ?? 0,
			totalPages: result.totalPages ?? 0,
			page: result.page ?? 1,
			pageSize: result.pageSize ?? 20,
		};
	} catch (error) {
		adminHouseholdLogger.error("Failed to list households", { error });
		return { items: [], totalCount: 0, totalPages: 0, page: 1, pageSize: 20 };
	}
}

export async function adminDeleteHousehold(id: string) {
	try {
		const result = await serverApi<{ success: boolean; message?: string }>(
			`/api/Households/admin/${id}`,
			{ method: "DELETE" },
		);
		revalidatePath("/admin/households");
		return result;
	} catch (error) {
		adminHouseholdLogger.error("Delete household failed", { error, id });
		return { success: false, message: "Could not delete household." };
	}
}

export async function adminRemoveHouseholdMember(householdId: string, userId: string) {
	try {
		const result = await serverApi<{ success: boolean; message?: string }>(
			`/api/Households/admin/${householdId}/members/${userId}`,
			{ method: "DELETE" },
		);
		revalidatePath(`/admin/households/${householdId}`);
		return result;
	} catch (error) {
		adminHouseholdLogger.error("Admin remove member failed", { error, householdId, userId });
		return { success: false, message: "Could not remove member." };
	}
}
