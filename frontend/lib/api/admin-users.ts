import { clientApi } from "@/lib/api.client";

export interface UpdateAdminUserInput {
	role?: string;
	banned?: boolean;
	banReason?: string | null;
	banExpires?: string | null;
	emailVerified?: boolean;
	newPassword?: string;
}

export function updateAdminUser(userId: string, input: UpdateAdminUserInput) {
	return clientApi<void>(`/api/admin/users/${userId}`, { method: "PATCH", body: input });
}

/** Opens a one-hour session as the user. The page must then load afresh, so the server renders as them. */
export function impersonateAdminUser(userId: string) {
	return clientApi<{ id: string }>(`/api/admin/users/${userId}/impersonate`, { method: "POST" });
}

export function deleteAdminUser(userId: string) {
	return clientApi<void>(`/api/admin/users/${userId}`, { method: "DELETE" });
}

export function revokeAdminUserSessions(userId: string) {
	return clientApi<void>(`/api/admin/users/${userId}/sessions`, { method: "DELETE" });
}

export function revokeAdminSession(sessionId: string) {
	return clientApi<void>(`/api/admin/sessions/${sessionId}`, { method: "DELETE" });
}

export function createAdminUser(input: {
	email: string;
	password: string;
	name?: string | null;
	role: string;
	emailVerified: boolean;
}) {
	return clientApi<{ id: string }>("/api/admin/users", { method: "POST", body: input });
}
