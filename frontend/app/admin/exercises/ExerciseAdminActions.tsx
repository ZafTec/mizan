"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { clientApi } from "@/lib/api.client";
import { appToast } from "@/lib/toast";
import ExerciseEditor, { type EditableExercise } from "./ExerciseEditor";

export default function ExerciseAdminActions({ exercise }: { exercise: EditableExercise & { isCustom: boolean } }) {
	const router = useRouter();
	const [editing, setEditing] = useState(false);

	async function run(path: string, method: "POST" | "DELETE") {
		try {
			await clientApi(path, { method });
			router.refresh();
		} catch (error) {
			appToast.error(error, "Could not update exercise");
		}
	}

	return (
		<div className="flex justify-end gap-2">
			<button className="btn-secondary btn-sm" onClick={() => setEditing(true)} aria-label={`Edit ${exercise.name}`}>
				Edit
			</button>
			{exercise.isCustom && (
				<button className="btn-primary btn-sm" onClick={() => run(`/api/Exercises/${exercise.id}/promote`, "POST")}>
					Promote
				</button>
			)}
			<button className="btn-ghost btn-sm text-red-600" onClick={() => run(`/api/Exercises/${exercise.id}`, "DELETE")}>
				Delete
			</button>
			{editing && <ExerciseEditor exercise={exercise} open onClose={() => setEditing(false)} />}
		</div>
	);
}
