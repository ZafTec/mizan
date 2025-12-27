import { FormState, getFieldError } from "@/helper/FormErrorHandler";

type FieldErrorProps = {
	formState: FormState;
	name: string;
};

export function FieldError({ formState, name }: FieldErrorProps) {
	const error = getFieldError(formState, name);
	if (!error) return null;

	return (
		<span className="text-xs text-red-400">
			{error}
		</span>
	)
}

