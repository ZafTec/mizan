import { useState, useEffect } from "react";

export function useCsrfToken() {
	const [csrfToken, setCsrfToken] = useState<string | null>(null);
	const [loading, setLoading] = useState(true);
	const [error, setError] = useState<Error | null>(null);

	useEffect(() => {
		async function fetchToken() {
			try {
				setLoading(true);
				const response = await fetch("/api/csrf");
				if (!response.ok) {
					throw new Error("Failed to fetch CSRF token");
				}
				const data = await response.json();
				setCsrfToken(data.token);
				setError(null);
			} catch (err) {
				setError(err instanceof Error ? err : new Error("Unknown error"));
			} finally {
				setLoading(false);
			}
		}

		fetchToken();
	}, []);

	return { csrfToken, loading, error };
}
