"use client";

import { useState } from "react";
import ExerciseEditor from "./ExerciseEditor";

export default function NewExerciseButton() {
	const [open, setOpen] = useState(false);
	return (
		<>
			<button type="button" className="btn-primary !rounded-2xl" onClick={() => setOpen(true)}>
				New exercise
			</button>
			{open && <ExerciseEditor open onClose={() => setOpen(false)} />}
		</>
	);
}
