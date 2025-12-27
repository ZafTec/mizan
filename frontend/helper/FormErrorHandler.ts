export interface FieldError {
    field: string;
    message: string;
}

export interface FormState {
    status: "idle" | "error" | "success";
    message?: string;
    fieldErrors?: FieldError[];
}

export const EMPTY_FORM_STATE: FormState = {
    status: "idle",
};

/**
 * Helper to extract field-specific error from form state
 */
export function getFieldError(formState: FormState, field: string): string | undefined {
    return formState.fieldErrors?.find((e) => e.field === field)?.message;
}

/**
 * Helper to create an error form state
 */
export function createErrorState(message: string, fieldErrors?: FieldError[]): FormState {
    return {
        status: "error",
        message,
        fieldErrors,
    };
}

/**
 * Helper to create a success form state
 */
export function createSuccessState(message?: string): FormState {
    return {
        status: "success",
        message,
    };
}
