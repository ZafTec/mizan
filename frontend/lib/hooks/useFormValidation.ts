import { useState, useCallback } from "react";
import { z, ZodSchema, ZodError } from "zod";

export interface ValidationErrors {
	[key: string]: string | undefined;
}

export interface UseFormValidationReturn<T> {
	errors: ValidationErrors;
	validate: (data: unknown) => T | null;
	validateField: (fieldName: keyof T, value: unknown) => boolean;
	clearError: (fieldName: keyof T) => void;
	clearErrors: () => void;
	hasErrors: boolean;
}

/**
 * Custom hook for form validation using Zod schemas
 *
 * @example
 * const { errors, validate, validateField, clearError } = useFormValidation(RecipeCreateSchema);
 *
 * const handleSubmit = () => {
 *   const validData = validate(formData);
 *   if (validData) {
 *     // Submit valid data
 *   }
 * };
 *
 * const handleFieldBlur = (field: string, value: any) => {
 *   validateField(field, value);
 * };
 */
export function useFormValidation<T extends z.ZodRawShape>(
	schema: ZodSchema<T>
): UseFormValidationReturn<T> {
	const [errors, setErrors] = useState<ValidationErrors>({});

	const validate = useCallback(
		(data: unknown): T | null => {
			try {
				const validData = schema.parse(data);
				setErrors({});
				return validData as T;
			} catch (error) {
				if (error instanceof ZodError) {
					const fieldErrors: ValidationErrors = {};
					error.issues.forEach((err) => {
						const fieldName = err.path[0];
						if (fieldName) {
							fieldErrors[fieldName as string] = err.message;
						}
					});
					setErrors(fieldErrors);
				}
				return null;
			}
		},
		[schema]
	);

	const validateField = useCallback(
		(fieldName: keyof T, value: unknown): boolean => {
			try {
				// Get the field schema from the parent schema
				const fieldSchema = (schema as any).shape[fieldName];
				if (fieldSchema) {
					fieldSchema.parse(value);
					setErrors((prev) => {
						const next = { ...prev };
						delete next[fieldName as string];
						return next;
					});
					return true;
				}
				return false;
			} catch (error) {
				if (error instanceof ZodError) {
					setErrors((prev) => ({
						...prev,
						[fieldName as string]: error.issues[0]?.message || "Invalid value",
					}));
				}
				return false;
			}
		},
		[schema]
	);

	const clearError = useCallback((fieldName: keyof T) => {
		setErrors((prev) => {
			const next = { ...prev };
			delete next[fieldName as string];
			return next;
		});
	}, []);

	const clearErrors = useCallback(() => {
		setErrors({});
	}, []);

	const hasErrors = Object.keys(errors).length > 0;

	return {
		errors,
		validate,
		validateField,
		clearError,
		clearErrors,
		hasErrors,
	};
}

/**
 * Helper function to get error message for a specific field
 */
export function getFieldError(
	errors: ValidationErrors,
	fieldName: string
): string | undefined {
	return errors[fieldName];
}

/**
 * Helper function to check if a field has an error
 */
export function hasFieldError(
	errors: ValidationErrors,
	fieldName: string
): boolean {
	return !!errors[fieldName];
}
