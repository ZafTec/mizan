/**
 * Converts PascalCase/camelCase string to snake_case
 */
function toSnakeCase(str: string): string {
	return str.replace(/[A-Z]/g, (letter) => `_${letter.toLowerCase()}`);
}

/**
 * Converts snake_case string to camelCase
 */
function toCamelCase(str: string): string {
	return str.replace(/_([a-z])/g, (_, letter) => letter.toUpperCase());
}

/**
 * Converts PascalCase string to camelCase
 */
function pascalToCamelCase(str: string): string {
	return str.charAt(0).toLowerCase() + str.slice(1);
}

/**
 * Recursively converts all keys in an object from PascalCase to camelCase
 */
export function convertKeysToCamelCase<T = any>(obj: any): T {
	if (obj === null || obj === undefined) {
		return obj;
	}

	if (Array.isArray(obj)) {
		return obj.map((item) => convertKeysToCamelCase(item)) as T;
	}

	if (typeof obj === "object" && obj.constructor === Object) {
		const converted: any = {};
		for (const key in obj) {
			if (Object.prototype.hasOwnProperty.call(obj, key)) {
				const camelKey = pascalToCamelCase(key);
				converted[camelKey] = convertKeysToCamelCase(obj[key]);
			}
		}
		return converted as T;
	}

	return obj;
}

/**
 * Recursively converts all keys in an object from camelCase to PascalCase
 */
export function convertKeysToPascalCase<T = any>(obj: any): T {
	if (obj === null || obj === undefined) {
		return obj;
	}

	if (Array.isArray(obj)) {
		return obj.map((item) => convertKeysToPascalCase(item)) as T;
	}

	if (typeof obj === "object" && obj.constructor === Object) {
		const converted: any = {};
		for (const key in obj) {
			if (Object.prototype.hasOwnProperty.call(obj, key)) {
				const pascalKey = key.charAt(0).toUpperCase() + key.slice(1);
				converted[pascalKey] = convertKeysToPascalCase(obj[key]);
			}
		}
		return converted as T;
	}

	return obj;
}
