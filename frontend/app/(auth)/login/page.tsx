"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { UserInput } from "@/types/user";
import Loading from "@/components/Loading";
import { PasswordInput } from "@/components/PasswordInput";
import { AnimatedIcon } from "@/components/ui/animated-icon";
import { resendVerification, signIn, startExternalSignIn, type AuthError } from "@/lib/auth-client";

const LAST_METHOD_KEY = "mizan:last-login-method";

function rememberMethod(method: string) {
	try {
		localStorage.setItem(LAST_METHOD_KEY, method);
	} catch {
		// Nothing to remember with. The hint is a nicety.
	}
}

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
	const [unverified, setUnverified] = useState(false);
	const [resent, setResent] = useState(false);
	const [lastMethod, setLastMethod] = useState<string | null>(null);

	// BetterAuth's lastLoginMethod plugin went with the rest of it; the hint is
	// worth one localStorage key. Read after mount so the markup matches SSR.
	useEffect(() => {
		try {
			setLastMethod(localStorage.getItem(LAST_METHOD_KEY));
		} catch {
			// Private mode or blocked storage: no hint, no problem.
		}
	}, []);

	function handleChange(e: React.ChangeEvent<HTMLInputElement>) {
		setUser({
			...user,
			[e.target.name]: e.target.value,
		});
	}

	async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
		e.preventDefault();
		setLoading(true);
		setError("");
		setUnverified(false);

		try {
			await signIn(user.email, user.password);
			rememberMethod("email");
			router.push(searchParam.get("callbackUrl") || "/dashboard");
			router.refresh();
		} catch (caught) {
			const authError = caught as AuthError;
			// The backend distinguishes an unconfirmed address from a bad
			// password, so the screen can offer the fix instead of blaming the
			// password.
			if (authError.code === "email_not_verified") setUnverified(true);
			setError(authError.message || "Sign in failed. Please try again.");
		} finally {
			setLoading(false);
		}
	}

	async function handleResend() {
		try {
			await resendVerification(user.email);
			setResent(true);
		} catch {
			setError("Could not send the confirmation email. Try again shortly.");
		}
	}

	function handleSocialSignIn(provider: "google" | "github") {
		setSocialLoading(provider);
		setError("");
		rememberMethod(provider);
		startExternalSignIn(provider, searchParam.get("callbackUrl") || "/dashboard");
	}

	return (
		<div className="min-h-[70vh] flex items-center justify-center">
			<div className="w-full max-w-md">
				<div className="text-center mb-8">
					<div className="mx-auto mb-4 flex h-16 w-16 items-center justify-center rounded-3xl bg-brand-600 text-white shadow-lg shadow-brand-500/25 dark:bg-brand-500">
						<AnimatedIcon name="lock" size={26} aria-hidden="true" />
					</div>
					<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">Welcome back</h1>
					<p className="text-charcoal-blue-500 dark:text-charcoal-blue-400 mt-1">Sign in to continue to Mizan</p>
				</div>

				<div className="card p-6 sm:p-8">
					<div className="space-y-3 mb-6">
						<button
							type="button"
							onClick={() => handleSocialSignIn("google")}
							disabled={!!socialLoading}
							className="relative w-full flex items-center justify-center gap-3 py-2.5 px-4 rounded-xl border border-charcoal-blue-200 dark:border-charcoal-blue-800 bg-white dark:bg-charcoal-blue-900 hover:bg-charcoal-blue-50 dark:hover:bg-charcoal-blue-700 text-charcoal-blue-700 dark:text-charcoal-blue-200 text-sm font-medium transition-colors disabled:opacity-60"
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
							className="relative w-full flex items-center justify-center gap-3 py-2.5 px-4 rounded-xl border border-charcoal-blue-200 dark:border-charcoal-blue-800 bg-white dark:bg-charcoal-blue-900 hover:bg-charcoal-blue-50 dark:hover:bg-charcoal-blue-700 text-charcoal-blue-700 dark:text-charcoal-blue-200 text-sm font-medium transition-colors disabled:opacity-60"
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
							<div className="w-full border-t border-charcoal-blue-200 dark:border-charcoal-blue-800" />
						</div>
						<div className="relative flex justify-center text-xs text-charcoal-blue-400 dark:text-charcoal-blue-500">
							<span className="bg-white dark:bg-charcoal-blue-900 px-3">or continue with email</span>
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
							<p className="text-xs text-charcoal-blue-400 dark:text-charcoal-blue-500 flex items-center gap-1.5">
								<span className="inline-flex items-center px-1.5 py-0.5 rounded-md text-xs font-medium bg-brand-100 dark:bg-brand-900 text-brand-700 dark:text-brand-300">
									Last used
								</span>
								You last signed in with email
							</p>
						)}

					{error && (
						<div data-testid="error-message" className="flex items-center gap-2 p-3 rounded-xl bg-red-50 dark:bg-red-950 text-red-600 dark:text-red-400 text-sm">
							<AnimatedIcon name="badgeAlert" size={18} aria-hidden="true" />
							<span>{error}</span>
								{unverified && user.email && !resent && (
									<button
										type="button"
										onClick={handleResend}
										className="ml-auto shrink-0 text-brand-600 dark:text-brand-400 hover:underline"
									>
										Resend link
									</button>
								)}
								{resent && (
									<span className="ml-auto shrink-0 text-xs text-charcoal-blue-500">Link sent</span>
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
									<AnimatedIcon name="arrowRight" size={18} aria-hidden="true" />
								</>
							)}
						</button>
					</form>
				</div>

				<p className="text-center text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400 mt-6">
					Don&apos;t have an account?{" "}
					<Link href="/register" className="text-brand-600 dark:text-brand-400 font-medium hover:text-brand-700 dark:hover:text-brand-400">
						Create one
					</Link>
				</p>
			</div>
		</div>
	);
}
