'use client';

import User from "@/types/user";
import { useEffect, useState } from "react";

export default function getUser() {
	const [user, setUser] = useState<User | null>(null);
	const [loading, setLoading] = useState(true);

	useEffect(() => {
		fetch("/api/auth/me")
			.then((res) => res.json())
			.then((data) => data.user)
			.then((user) => setUser(user))
			.catch((error) => console.error(error))
			.finally(() => {
				setLoading(false);
			});
	}, []);

	return { user, loading };
}
