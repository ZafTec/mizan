"use server";

import { serverApi } from "@/lib/api.server";

export interface HardConstraint {
	title: string;
	detail: string;
	enforcedBy: string;
}

export interface AiPromptSummary {
	key: string;
	description: string;
	publishedVersion: number | null;
	publishedAt: string | null;
	draftCount: number;
	versionCount: number;
}

export type AiPromptStatus = 0 | 1 | 2;

export interface AiPromptVersion {
	id: string;
	version: number;
	body: string;
	softPolicy: string;
	status: AiPromptStatus;
	notes: string | null;
	authorName: string | null;
	createdAt: string;
	publishedAt: string | null;
}

export interface AiPromptDetail {
	key: string;
	description: string;
	defaultBody: string;
	preamble: string;
	hardConstraints: HardConstraint[];
	versions: AiPromptVersion[];
}

export interface AiEvalCase {
	id: string;
	name: string;
	isAdversarial: boolean;
	input: string;
	context: string | null;
	assertions: string;
}

export type AiEvalOutcome = 0 | 1 | 2;

export interface AiEvalRun {
	caseId: string;
	outcome: AiEvalOutcome;
	schemaValid: boolean;
	output: string | null;
	failureReason: string | null;
	tokens: number;
	costMicros: number;
	latencyMs: number;
}

export interface AiEvalMatrix {
	versionId: string;
	cases: AiEvalCase[];
	runs: AiEvalRun[];
	publishable: boolean;
	blockedReason: string | null;
	costMicros: number;
	publishedCostMicros: number | null;
}

export interface GlobalAiUsage {
	tokensToday: number;
	tokenCeiling: number;
	costMicrosToday: number;
	costCeilingMicros: number;
	requestsToday: number;
	failuresToday: number;
	activeUsersToday: number;
	byFeature: { feature: string; requests: number; tokens: number }[];
}

export async function listAiPrompts(): Promise<AiPromptSummary[]> {
	return serverApi<AiPromptSummary[]>("/api/Admin/Ai/Prompts");
}

export async function getAiPrompt(key: string): Promise<AiPromptDetail> {
	return serverApi<AiPromptDetail>(
		`/api/Admin/Ai/Prompts/${encodeURIComponent(key)}`,
	);
}

export async function getAiEvalMatrix(versionId: string): Promise<AiEvalMatrix> {
	return serverApi<AiEvalMatrix>(
		`/api/Admin/Ai/Prompts/versions/${versionId}/evals`,
	);
}

export async function getGlobalAiUsage(): Promise<GlobalAiUsage> {
	return serverApi<GlobalAiUsage>("/api/Ai/usage/global");
}
