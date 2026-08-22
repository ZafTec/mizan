"use client";

import Link from "next/link";
import { useState } from "react";
import { forgotPassword } from "@/lib/auth-client";
import Loading from "@/components/Loading";

type State = "idle" | "success";

export default function ForgotPasswordPage() {
	const [email, setEmail] = useState("");
	const [loading, setLoading] = useState(false);
	const [error, setError] = useState("");
	const [state, setState] = useState<State>("idle");

	async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
		e.preventDefault();
		setLoading(true);
		setError("");

		try {
			await forgotPassword(email);
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
							<div className="w-16 h-16 rounded-2xl bg-green-100 dark:bg-green-950 flex items-center justify-center mx-auto">
								<i className="ri-mail-send-line text-3xl text-green-600" />
							</div>
							<div>
								<h3 className="text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100 mb-2">
									Check your email
								</h3>
								<p className="text-charcoal-blue-500 dark:text-charcoal-blue-400 text-sm">
									If <span className="font-medium text-charcoal-blue-700 dark:text-charcoal-blue-300">{email}</span> has an account, a reset link is on its way.
								</p>
							</div>
							<div className="pt-4">
								<Link href="/login" className="btn-primary w-full py-3">
									<i className="ri-arrow-left-line" />
									Back to login
								</Link>
							</div>
							<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
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
					<div className="inline-flex items-center justify-center w-16 h-16 rounded-2xl bg-brand-600 shadow-lg shadow-brand-500/30 dark:bg-brand-500 dark:shadow-brand-500/15 mb-4">
						<i className="ri-lock-password-line text-3xl text-white" />
					</div>
					<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">Forgot password?</h1>
					<p className="text-charcoal-blue-500 dark:text-charcoal-blue-400 mt-1">
						We&apos;ll email you a link to set a new one
					</p>
				</div>

				{/* Form Card */}
				<div className="card p-6 sm:p-8 space-y-5">
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
									Sending reset link...
								</>
							) : (
								<>
									<i className="ri-mail-send-line" /> Send reset link
									<i className="ri-arrow-right-line" />
								</>
							)}
						</button>

						<div className="text-center">
							<Link
								href="/login"
								className="text-sm text-charcoal-blue-600 dark:text-charcoal-blue-400 hover:text-charcoal-blue-900 dark:hover:text-charcoal-blue-100 inline-flex items-center gap-1"
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
