"use client";

import Link from "next/link";
import { useState } from "react";
import { authClient } from "@/lib/auth-client";
import Loading from "@/components/Loading";

type Mode = "reset" | "magic";
type State = "idle" | "success";

export default function ForgotPasswordPage() {
	const [email, setEmail] = useState("");
	const [loading, setLoading] = useState(false);
	const [error, setError] = useState("");
	const [state, setState] = useState<State>("idle");
	const [mode, setMode] = useState<Mode>("reset");

	async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
		e.preventDefault();
		setLoading(true);
		setError("");

		try {
			if (mode === "reset") {
				const { error: resetError } = await authClient.requestPasswordReset({
					email,
					redirectTo: `${window.location.origin}/reset-password`,
				});
				if (resetError) {
					setError(resetError.message || "Failed to send reset email. Please try again.");
					return;
				}
			} else {
				const { error: magicError } = await authClient.signIn.magicLink({
					email,
					callbackURL: "/dashboard",
				});
				if (magicError) {
					setError(magicError.message || "Failed to send sign-in link. Please try again.");
					return;
				}
			}
			setState("success");
		} catch {
			setError("An error occurred. Please try again.");
		} finally {
			setLoading(false);
		}
	}

	if (state === "success") {
		return (
			<div className="min-h-[70vh] flex items-center justify-center">
				<div className="w-full max-w-md">
					<div className="card p-6 sm:p-8">
						<div className="text-center space-y-4">
							<div className="w-16 h-16 rounded-full bg-green-100 dark:bg-green-950 flex items-center justify-center mx-auto">
								<i className="ri-mail-send-line text-3xl text-green-600" />
							</div>
							<div>
								<h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100 mb-2">
									Check your email
								</h3>
								<p className="text-slate-500 dark:text-slate-400 text-sm">
									{mode === "reset"
										? <>We sent a password reset link to <span className="font-medium text-slate-700 dark:text-slate-300">{email}</span></>
										: <>We sent a sign-in link to <span className="font-medium text-slate-700 dark:text-slate-300">{email}</span>. Click it to sign in instantly — no password needed.</>
									}
								</p>
							</div>
							<div className="pt-4">
								<Link href="/login" className="btn-primary w-full py-3">
									<i className="ri-arrow-left-line" />
									Back to login
								</Link>
							</div>
							<p className="text-sm text-slate-500 dark:text-slate-400">
								Didn&apos;t receive the email?{" "}
								<button
									onClick={() => { setState("idle"); setEmail(""); }}
									className="text-brand-600 dark:text-brand-400 hover:text-brand-700 font-medium"
								>
									Try again
								</button>
							</p>
						</div>
					</div>
				</div>
			</div>
		);
	}

	return (
		<div className="min-h-[70vh] flex items-center justify-center">
			<div className="w-full max-w-md">
				{/* Header */}
				<div className="text-center mb-8">
					<div className="inline-flex items-center justify-center w-16 h-16 rounded-2xl bg-linear-to-br from-brand-400 to-brand-600 shadow-lg shadow-brand-500/30 dark:shadow-brand-500/15 mb-4">
						<i className="ri-lock-password-line text-3xl text-white" />
					</div>
					<h1 className="text-2xl font-bold text-slate-900 dark:text-slate-100">Forgot password?</h1>
					<p className="text-slate-500 dark:text-slate-400 mt-1">
						No worries, choose how you want to recover access
					</p>
				</div>

				{/* Form Card */}
				<div className="card p-6 sm:p-8 space-y-5">
					{/* Mode toggle */}
					<div className="grid grid-cols-2 gap-1 p-1 bg-slate-100 dark:bg-slate-800 rounded-xl">
						<button
							type="button"
							onClick={() => { setMode("reset"); setError(""); }}
							className={`py-2 px-3 rounded-lg text-sm font-medium transition-colors ${
								mode === "reset"
									? "bg-white dark:bg-slate-700 text-slate-900 dark:text-slate-100 shadow-sm"
									: "text-slate-500 dark:text-slate-400 hover:text-slate-700 dark:hover:text-slate-200"
							}`}
						>
							<i className="ri-lock-2-line mr-1.5" />
							Reset Password
						</button>
						<button
							type="button"
							onClick={() => { setMode("magic"); setError(""); }}
							className={`py-2 px-3 rounded-lg text-sm font-medium transition-colors ${
								mode === "magic"
									? "bg-white dark:bg-slate-700 text-slate-900 dark:text-slate-100 shadow-sm"
									: "text-slate-500 dark:text-slate-400 hover:text-slate-700 dark:hover:text-slate-200"
							}`}
						>
							<i className="ri-magic-line mr-1.5" />
							Magic Link
						</button>
					</div>

					{/* Mode description */}
					<p className="text-xs text-slate-500 dark:text-slate-400 text-center">
						{mode === "reset"
							? "We'll email you a link to set a new password."
							: "We'll email you a one-click sign-in link — no password required."}
					</p>

					<form data-testid="forgot-password-form" onSubmit={handleSubmit} className="space-y-5">
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
								value={email}
								onChange={(e) => setEmail(e.target.value)}
							/>
						</div>

						{error && (
							<div data-testid="error-message" className="flex items-center gap-2 p-3 rounded-xl bg-red-50 dark:bg-red-950 text-red-600 dark:text-red-400 text-sm">
								<i className="ri-error-warning-line text-lg" />
								<span>{error}</span>
							</div>
						)}

						<button
							type="submit"
							disabled={loading}
							className="btn-primary w-full py-3"
						>
							{loading ? (
								<>
									<Loading size="sm" />
									{mode === "reset" ? "Sending reset link..." : "Sending sign-in link..."}
								</>
							) : (
								<>
									{mode === "reset" ? (
										<><i className="ri-mail-send-line" /> Send reset link</>
									) : (
										<><i className="ri-magic-line" /> Send magic link</>
									)}
									<i className="ri-arrow-right-line" />
								</>
							)}
						</button>

						<div className="text-center">
							<Link
								href="/login"
								className="text-sm text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-slate-100 inline-flex items-center gap-1"
							>
								<i className="ri-arrow-left-line" />
								Back to login
							</Link>
						</div>
					</form>
				</div>
			</div>
		</div>
	);
}
