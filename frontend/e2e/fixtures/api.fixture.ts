import type { APIRequestContext } from "@playwright/test";

const API_URL = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000";

export class ApiHelper {
	constructor(private request: APIRequestContext) {}

	async createFood(token: string, data: Record<string, unknown>) {
		return this.request.post(`${API_URL}/api/Foods`, {
			headers: { Authorization: `Bearer ${token}` },
			data,
		});
	}

	async createRecipe(token: string, data: Record<string, unknown>) {
		return this.request.post(`${API_URL}/api/Recipes`, {
			headers: { Authorization: `Bearer ${token}` },
			data,
		});
	}
}
