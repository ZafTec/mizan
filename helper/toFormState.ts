import { FormState } from "./FormErrorHandler";

export function toFormState(
	status: FormState['status'],
	message: string,
    fieldValues?: Record<string, any>
): FormState {
	return {
		status,
		message,
		fieldErrors: {},
		timestamp: Date.now(),
        fieldValues,
	}
}
