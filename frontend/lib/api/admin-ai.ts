import { clientApi } from "@/lib/api.client";
import type { AiEvalMatrix, AiPromptVersion } from "@/data/admin/ai";

export interface EvalSummary {
	versionId: string;
	total: number;
	passed: number;
	failed: number;
	errored: number;
	adversarialFailures: number;
	tokens: number;
	costMicros: number;
}

export const createDraft = (
	key: string,
	body: { body?: string; softPolicy?: string; notes?: string | null },
) =>
	clientApi<AiPromptVersion>(
		`/api/Admin/Ai/Prompts/${encodeURIComponent(key)}/drafts`,
		{ method: "POST", body },
	);

export const updateDraft = (
	versionId: string,
	body: { body: string; softPolicy: string; notes?: string | null },
) =>
	clientApi<AiPromptVersion>(`/api/Admin/Ai/Prompts/versions/${versionId}`, {
		method: "PUT",
		body,
	});

export const runEvals = (versionId: string) =>
	clientApi<EvalSummary>(`/api/Admin/Ai/Prompts/versions/${versionId}/evals`, {
		method: "POST",
	});

export const getEvalMatrix = (versionId: string) =>
	clientApi<AiEvalMatrix>(`/api/Admin/Ai/Prompts/versions/${versionId}/evals`);

export const publishVersion = (versionId: string) =>
	clientApi<AiPromptVersion>(
		`/api/Admin/Ai/Prompts/versions/${versionId}/publish`,
		{ method: "POST" },
	);
