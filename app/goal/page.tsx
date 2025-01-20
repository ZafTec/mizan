
"use client";

import { FormState } from "@/helper/FormErrorHandler";
import { FieldError } from "@/components/FieldError";
import { EMPTY_FORM_STATE } from "@/helper/FormErrorHandler";
import Goal from "@/types/goal";
import { useActionState, useEffect } from "react";
import SubmitButton from "@/components/AddIngredient/button";
import { createGoal } from "@/data/goal";

interface GoalFormProps {
	// currentGoal: Goal;
}

export default function GoalForm({  }: GoalFormProps) {
	const [formState, action] = useActionState(createGoal, EMPTY_FORM_STATE);

	useEffect(() => {
		if (formState.status === "SUCCESS") {
			// Optionally reset form or display a success message
		}
	}, [formState.status]);

	return (
		<form action={action} className="flex flex-col gap-4">
			<div className="flex flex-row gap-2">
				<div className="flex flex-1 flex-col gap-2">
					<label htmlFor="name">Goal Name</label>
					<input
						type="text"
						id="name"
						name="name"
						className="border-2 border-gray-300 rounded-lg p-2"
					/>
					<FieldError formState={formState} name="name" />
				</div>
			</div>
			<div className="flex flex-col xl:flex-row gap-2">
				<div className="flex flex-col md:flex-row gap-2">
					<div className="flex flex-1 flex-col gap-2">
						<label htmlFor="calories">Calories</label>
						<input
							type="number"
							id="calories"
							name="calories"
							min={0}
							className="border-2 border-gray-300 rounded-lg p-2"
						/>
						<FieldError formState={formState} name="calories" />
					</div>
					<div className="flex flex-1 flex-col gap-2">
						<label htmlFor="protein">Protein</label>
						<input
							type="number"
							id="protein"
							name="protein"
							min={0}
							className="border-2 border-gray-300 rounded-lg p-2"
						/>
						<FieldError formState={formState} name="protein" />
					</div>
					<div className="flex flex-1 flex-col gap-2">
						<label htmlFor="fat">Fat</label>
						<input
							type="number"
							id="fat"
							name="fat"
							min={0}
							className="border-2 border-gray-300 rounded-lg p-2"
						/>
						<FieldError formState={formState} name="fat" />
					</div>
					</div>
					<div className="flex flex-col md:flex-row gap-2">
						<div className="flex flex-1 flex-col gap-2">
							<label htmlFor="carbs">Carbs</label>
							<input
								type="number"
								id="carbs"
								name="carbs"
								min={0}
								className="border-2 border-gray-300 rounded-lg p-2"
							/>
							<FieldError formState={formState} name="carbs" />
						</div>
						<div className="flex flex-1 flex-col gap-2">
							<label htmlFor="fiber">Fiber</label>
							<input
								type="number"
								id="fiber"
								name="fiber"
								min={0}
								className="border-2 border-gray-300 rounded-lg p-2"
							/>
							<FieldError formState={formState} name="fiber" />
						</div>
				</div>
			</div>

			<div>
				<SubmitButton label="Update Goal" loading={<>Loading...</>} />
			</div>

			{formState.message && (
				<div>
					<p>{formState.message}</p>
				</div>
			)}
		</form>
	)
}