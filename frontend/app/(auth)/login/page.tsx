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
		fetch("/api/auth/sign-in/email", {
			method: "POST",
			headers: {
				"Content-Type": "application/json",
			},
			body: JSON.stringify({
				email: user.email,
				password: user.password,
				callbackURL: callbackUrl,
			}),
		})
		.then(async (res) => {
			if (!res.ok) {
				const text = await res.text();
				try {
					const json = JSON.parse(text);
					// Map Better Auth error codes to user-friendly messages
					const errorCode = json.code;
					let errorMessage = "Sign in failed. Please try again.";

					if (errorCode === "INVALID_EMAIL_OR_PASSWORD") {
						errorMessage = "Invalid email or password";
					} else if (errorCode === "USER_NOT_VERIFIED") {
						errorMessage = "Please verify your email address before signing in";
					} else if (json.message) {
						errorMessage = json.message;
					}

					throw new Error(errorMessage);
				} catch (e) {
					if (e instanceof Error && e.message !== text) {
						throw e;
					}
					throw new Error("Sign in failed. Please try again.");
				}
			}
			return res.json();
		})
		.then((data) => {
			if (data.user || data.session) {
				router.push(callbackUrl);
				router.refresh();
			} else {
				setError("Sign in failed. Please try again.");
			}
		})
		.catch((error) => {
			setError(error.message || "An error occurred during sign in");
		})
		.finally(() => {
			setLoading(false);
		});
	}

	return (
		<div className="min-h-[70vh] flex items-center justify-center">
			<div className="w-full max-w-md">
				{/* Header */}
				<div className="text-center mb-8">
					<div className="inline-flex items-center justify-center w-16 h-16 rounded-2xl bg-linear-to-br from-brand-400 to-brand-600 shadow-lg shadow-brand-500/30 dark:shadow-brand-500/15 mb-4">
						<i className="ri-user-line text-3xl text-white" />
					</div>
					<h1 className="text-2xl font-bold text-slate-900 dark:text-slate-100">Welcome back</h1>
					<p className="text-slate-500 dark:text-slate-400 mt-1">Sign in to continue to Mizan</p>
				</div>

				{/* Form Card */}
				<div className="card p-6 sm:p-8">
					<form className="space-y-5" onSubmit={handleSubmit}>
						<div>
							<label htmlFor="email" className="label">
								Email address
							</label>
							<input
								required
								type="email"
								id="email"
								name="email"
								className="input"
								placeholder="you@example.com"
								onChange={handleChange}
							/>
						</div>

						<div>
							<div className="flex items-center justify-between mb-1.5">
								<label htmlFor="password" className="label mb-0">
									Password
								</label>
								<Link href="/forgot-password" className="text-sm text-brand-600 dark:text-brand-400 hover:text-brand-700 dark:hover:text-brand-400">
									Forgot password?
								</Link>
							</div>
							<input
								required
								type="password"
								id="password"
								name="password"
								className="input"
								placeholder="••••••••"
								onChange={handleChange}
							/>
						</div>

						{error && (
							<div className="flex items-center gap-2 p-3 rounded-xl bg-red-50 dark:bg-red-950 text-red-600 dark:text-red-400 text-sm">
								<i className="ri-error-warning-line text-lg" />
								<span>{error}</span>
								{error === "User is not verified" && user.email && (
									<Link className="ml-auto text-brand-600 dark:text-brand-400 hover:underline" href={`/verify?email=${user.email}`}>
										Resend verification
									</Link>
								)}
							</div>
						)}

						<button
							type="submit"
							disabled={loading}
							className="btn-primary w-full py-3"
						>
							{loading ? (
								<>
									<svg className="animate-spin h-5 w-5" fill="none" viewBox="0 0 24 24">
										<circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
										<path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
									</svg>
									Signing in...
								</>
							) : (
								<>
									Sign in
									<i className="ri-arrow-right-line" />
								</>
							)}
						</button>

						{/* Divider */}
						<div className="relative my-6">
							<div className="absolute inset-0 flex items-center">
								<div className="w-full border-t border-slate-200 dark:border-slate-700" />
							</div>
							<div className="relative flex justify-center text-sm">
								<span className="px-2 bg-white dark:bg-slate-900 text-slate-500 dark:text-slate-400">Or continue with</span>
							</div>
						</div>

						{/* Social Login */}
						<div className="grid grid-cols-2 gap-3">
							<button type="button" className="btn-secondary py-2.5">
								<i className="ri-google-fill text-lg" />
								Google
							</button>
							<button type="button" className="btn-secondary py-2.5">
								<i className="ri-github-fill text-lg" />
								GitHub
							</button>
						</div>
					</form>
				</div>

				{/* Footer */}
				<p className="text-center text-sm text-slate-500 dark:text-slate-400 mt-6">
					Don&apos;t have an account?{" "}
					<Link href="/register" className="text-brand-600 dark:text-brand-400 font-medium hover:text-brand-700 dark:hover:text-brand-400">
						Create one
					</Link>
				</p>
			</div>
		</div>
	);
}
