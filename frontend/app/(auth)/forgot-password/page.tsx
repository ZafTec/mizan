"use client";

import Link from "next/link";
import { useState } from "react";
import { authClient } from "@/lib/auth-client";

export default function ForgotPasswordPage() {
	const [email, setEmail] = useState("");
	const [loading, setLoading] = useState(false);
	const [error, setError] = useState("");
	const [success, setSuccess] = useState(false);

	async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
		e.preventDefault();
		setLoading(true);
		setError("");
		setSuccess(false);

		try {
			const { data, error: resetError } = await authClient.requestPasswordReset({
				email,
				redirectTo: `${window.location.origin}/reset-password`,
			});

			if (resetError) {
				setError(resetError.message || "Failed to send reset email. Please try again.");
			} else {
				setSuccess(true);
			}
		} catch (err) {
			setError("An error occurred. Please try again.");
		} finally {
			setLoading(false);
		}
	}

	return (
		<div className="min-h-[70vh] flex items-center justify-center">
			<div className="w-full max-w-md">
				{/* Header */}
				<div className="text-center mb-8">
					<div className="inline-flex items-center justify-center w-16 h-16 rounded-2xl bg-gradient-to-br from-brand-400 to-brand-600 shadow-lg shadow-brand-500/30 mb-4">
						<i className="ri-lock-password-line text-3xl text-white" />
					</div>
					<h1 className="text-2xl font-bold text-slate-900">Forgot password?</h1>
					<p className="text-slate-500 mt-1">
						No worries, we&apos;ll send you reset instructions
					</p>
				</div>

				{/* Form Card */}
				<div className="card p-6 sm:p-8">
					{success ? (
						<div className="text-center space-y-4">
							<div className="w-16 h-16 rounded-full bg-green-100 flex items-center justify-center mx-auto">
								<i className="ri-mail-send-line text-3xl text-green-600" />
							</div>
							<div>
								<h3 className="text-lg font-semibold text-slate-900 mb-2">
									Check your email
								</h3>
								<p className="text-slate-500 text-sm">
									We sent a password reset link to <span className="font-medium text-slate-700">{email}</span>
								</p>
							</div>
							<div className="pt-4">
								<Link href="/login" className="btn-primary w-full py-3">
									<i className="ri-arrow-left-line" />
									Back to login
								</Link>
							</div>
							<p className="text-sm text-slate-500">
								Didn&apos;t receive the email?{" "}
								<button
									onClick={() => {
										setSuccess(false);
										setEmail("");
									}}
									className="text-brand-600 hover:text-brand-700 font-medium"
								>
									Try again
								</button>
							</p>
						</div>
					) : (
						<form onSubmit={handleSubmit} className="space-y-5">
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
								<p className="text-xs text-slate-500 mt-1.5">
									Enter the email address associated with your account
								</p>
							</div>

							{error && (
								<div className="flex items-center gap-2 p-3 rounded-xl bg-red-50 text-red-600 text-sm">
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
										<svg className="animate-spin h-5 w-5" fill="none" viewBox="0 0 24 24">
											<circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
											<path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
										</svg>
										Sending reset link...
									</>
								) : (
									<>
										Send reset link
										<i className="ri-arrow-right-line" />
									</>
								)}
							</button>

							<div className="text-center">
								<Link
									href="/login"
									className="text-sm text-slate-600 hover:text-slate-900 inline-flex items-center gap-1"
								>
									<i className="ri-arrow-left-line" />
									Back to login
								</Link>
							</div>
						</form>
					)}
				</div>
			</div>
		</div>
	);
}
