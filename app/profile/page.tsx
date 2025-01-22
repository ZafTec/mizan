"use client";

import Loading from "@/components/Loading";
import { UserOutput } from "@/types/user";
import { useEffect, useState } from "react";


export default function Page() {

	const [user, setUser] = useState<UserOutput | null>(null);
	const [loading, setLoading] = useState(true);
	
	useEffect(() => {
		setLoading(true);
		fetch("/api/auth/me")
		.then((res) => res.json())
		.then((data) =>  data.user)
		.then((user) => setUser(user))
		.catch((error) => console.error(error))
		.finally(() => {
			setTimeout(() =>
				setLoading(false)
			, 10000)
		})
	
	}, []);

	if (loading) {
		return <Loading	/>
	}

	if (!user) {
		return (
			<div>
				<h1>User not found</h1>
			</div>
		)
	}
	
	return (
		<div>
			<h1>Profile</h1>
			<p>{user.email}</p>
		</div>
	)
}
