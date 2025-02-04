
"use client";

import Link from "next/link";
import { useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { UserInput } from "@/types/user";

export default function Page() {
	const router = useRouter();
	const searchParam = useSearchParams();
	const [user, setUser] = useState<UserInput>({
		email: "",
		password: "",
	});
	const [loading, setLoading] = useState(false);
	const [error, setError] = useState("");

	function handleChange(e: React.ChangeEvent<HTMLInputElement>) {
		setUser({
			...user,
			[e.target.name]: e.target.value,
		});
	}

	function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
		e.preventDefault();
		setLoading(true);
		const callbackUrl = searchParam.get("callbackUrl") || "/";
		fetch("/api/auth/login?callbackUrl=" + callbackUrl, {
			method: "POST",
			headers: {
				"Content-Type": "application/json",
			},
			body: JSON.stringify(user),
		})
		.then((res) => {
			return res.json();
		})
		.then((data) => {
			if (data.success) {
				router.push(data.callbackUrl);
				router.refresh();
			}
			setError(data.error);
		})
		.catch((error) => {
			console.error(error);
		})
		.finally(() => {
			setLoading(false);
		});
	}


	return (
		<div className="flex flex-col items-center justify-center py-2">
			<div className="w-3/5 md:w-1/2 lg:w-1/3 max-w-4xl min-w-96 p-2 md:p-6 mb-8 bg-white rounded-lg shadow-lg">
				<h2 className="text-2xl font-bold mb-4 text-gray-800">Login</h2>
				<form className="flex flex-col gap-4" onSubmit={handleSubmit}>
					<div className="flex flex-row gap-2">
						<div className="flex flex-1 flex-col gap-2">
							<label htmlFor="email">Email <span className="text-xs text-red-500">*required</span></label>
							<input
								required
								type="email"
								id="email"
								name="email"
								className="border-2 border-gray-300 rounded-lg p-2"
								placeholder="Email"
								onChange={handleChange}
							/>
						</div>
					</div>

					<div className="flex flex-row gap-2">
						<div className="flex flex-1 flex-col gap-2">
							<label htmlFor="password">Password <span className="text-xs text-red-500">*required</span></label>
							<input
								required
								type="password"
								id="password"
								name="password"
								className="border-2 border-gray-300 rounded-lg p-2"
								placeholder="Password"
								onChange={handleChange}
							/>
						</div>
					</div>
					{error && 
						<div className="text-red-500 text-sm">
							{error}
							{error === "User is not verified" && user.email && (
								<Link className="text-cyan-600 mx-3 hover:text-cyan-400" href={`/verify?email=${user.email}`}>Resend?</Link>
							)}
						</div>
					}
					<div className="flex flex-row gap-8 justify-end">
						<Link href="/register" className="text-blue-500 text-right">
							Don&apos;t have an account? <br />Register
						</Link>
						<button
							type="submit"
							disabled={loading}
							className="bg-blue-500 text-white rounded-lg py-1 px-6 flex items-center"
						>
							{loading && (
								<svg
									className="w-4 h-4 mr-2 animate-spin"
									fill="none"
									viewBox="0 0 24 24"
								>
									<circle
										className="opacity-25"
										cx="12"
										cy="12"
										r="10"
										stroke="currentColor"
										strokeWidth="4"
									></circle>
									<path
										className="opacity-75"
										fill="currentColor"
										d="M4 12a8 8 0 018-8v8H4z"
									></path>
								</svg>
							)}
							{loading ? "loading..." : "Login"}
						</button>
					</div>
				</form>
			</div>
		</div>
	)
}
