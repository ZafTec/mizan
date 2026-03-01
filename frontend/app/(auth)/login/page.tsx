"use client";

import Link from "next/link";
import { useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { UserInput } from "@/types/user";
import Loading from "@/components/Loading";
import { PasswordInput } from "@/components/PasswordInput";
import { authClient } from "@/lib/auth-client";

export default function Page() {
	const router = useRouter();
	const searchParam = useSearchParams();
	const [user, setUser] = useState<UserInput>({
		email: "",
		password: "",
	});
	const [loading, setLoading] = useState(false);
	const [socialLoading, setSocialLoading] = useState<string | null>(null);
	const [error, setError] = useState("");

	const lastMethod = authClient.getLastUsedLoginMethod();

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

	async function handleSocialSignIn(provider: "google" | "github") {
		setSocialLoading(provider);
		setError("");
		const callbackUrl = searchParam.get("callbackUrl") || "/";
		await authClient.signIn.social({
			provider,
			callbackURL: callbackUrl,
		});
		setSocialLoading(null);
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
					{/* Social Sign In */}
					<div className="space-y-3 mb-6">
						<button
							type="button"
							onClick={() => handleSocialSignIn("google")}
							disabled={!!socialLoading}
							className="relative w-full flex items-center justify-center gap-3 py-2.5 px-4 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 hover:bg-slate-50 dark:hover:bg-slate-700 text-slate-700 dark:text-slate-200 text-sm font-medium transition-colors disabled:opacity-60"
						>
							{socialLoading === "google" ? (
								<Loading size="sm" />
							) : (
								<i className="ri-google-fill text-lg text-red-500" />
							)}
							Continue with Google
							{lastMethod === "google" && (
								<span className="absolute right-3 inline-flex items-center px-1.5 py-0.5 rounded-md text-xs font-medium bg-brand-100 dark:bg-brand-900 text-brand-700 dark:text-brand-300">
									Last used
								</span>
							)}
						</button>

						<button
							type="button"
							onClick={() => handleSocialSignIn("github")}
							disabled={!!socialLoading}
							className="relative w-full flex items-center justify-center gap-3 py-2.5 px-4 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 hover:bg-slate-50 dark:hover:bg-slate-700 text-slate-700 dark:text-slate-200 text-sm font-medium transition-colors disabled:opacity-60"
						>
							{socialLoading === "github" ? (
								<Loading size="sm" />
							) : (
								<i className="ri-github-fill text-lg" />
							)}
							Continue with GitHub
							{lastMethod === "github" && (
								<span className="absolute right-3 inline-flex items-center px-1.5 py-0.5 rounded-md text-xs font-medium bg-brand-100 dark:bg-brand-900 text-brand-700 dark:text-brand-300">
									Last used
								</span>
							)}
						</button>
					</div>

					<div className="relative mb-6">
						<div className="absolute inset-0 flex items-center">
							<div className="w-full border-t border-slate-200 dark:border-slate-700" />
						</div>
						<div className="relative flex justify-center text-xs text-slate-400 dark:text-slate-500">
							<span className="bg-white dark:bg-slate-900 px-3">or continue with email</span>
						</div>
					</div>

					<form data-testid="login-form" className="space-y-5" onSubmit={handleSubmit}>
						<div>
							<label htmlFor="email" className="label">
								Email address
							</label>
							<input
								required
								type="email"
								id="email"
								name="email"
								data-testid="login-email"
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
							<PasswordInput
								required
								id="password"
								name="password"
								data-testid="login-password"
								className="input pr-10"
								placeholder="••••••••"
								onChange={handleChange}
							/>
						</div>

						{lastMethod === "email" && (
							<p className="text-xs text-slate-400 dark:text-slate-500 flex items-center gap-1.5">
								<span className="inline-flex items-center px-1.5 py-0.5 rounded-md text-xs font-medium bg-brand-100 dark:bg-brand-900 text-brand-700 dark:text-brand-300">
									Last used
								</span>
								You last signed in with email
							</p>
						)}

						{error && (
							<div data-testid="error-message" className="flex items-center gap-2 p-3 rounded-xl bg-red-50 dark:bg-red-950 text-red-600 dark:text-red-400 text-sm">
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
							data-testid="login-submit"
							className="btn-primary w-full py-3"
						>
							{loading ? (
								<>
									<Loading size="sm" />
									Signing in...
								</>
							) : (
								<>
									Sign in
									<i className="ri-arrow-right-line" />
								</>
							)}
						</button>
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
