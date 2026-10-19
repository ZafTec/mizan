import { resolvePublicApiOrigin } from "@/lib/api-base";
import { clientApi } from "@/lib/api.client";

export interface ExerciseDetails {
	name: string;
	category: string;
	muscleGroup?: string | null;
	equipment?: string | null;
	description?: string | null;
	videoUrl?: string | null;
	imageUrl?: string | null;
}

/** An exercise made by an administrator is a system one at once: created, then promoted. */
export async function createSystemExercise(details: ExerciseDetails) {
	const created = await clientApi<{ id: string }>("/api/Exercises", { method: "POST", body: details });
	await clientApi<void>(`/api/Exercises/${created.id}/promote`, { method: "POST" });
	return created.id;
}

export function updateExercise(id: string, details: ExerciseDetails) {
	return clientApi<void>(`/api/Exercises/${id}`, { method: "PUT", body: { id, ...details } });
}

export function setExerciseModel(id: string, modelUrl: string | null) {
	return clientApi<void>(`/api/Exercises/${id}/model`, { method: "PUT", body: { modelUrl } });
}

/** The glTF binary upload door. The server checks the file's bytes, not its name, and refuses anything else. */
export async function uploadModel(file: File): Promise<string> {
	const body = new FormData();
	body.append("file", file);
	const response = await fetch(`${resolvePublicApiOrigin()}/api/Uploads/model`, {
		method: "POST",
		credentials: "include",
		body,
	});

	if (!response.ok) {
		const payload = await response.json().catch(() => null);
		throw new Error(payload?.error ?? "The upload failed. Try again.");
	}

	return ((await response.json()) as { url: string }).url;
}
