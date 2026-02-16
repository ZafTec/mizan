"use client";

import Link from "next/link";
import { useState, useEffect } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { authClient } from "@/lib/auth-client";

export default function ResetPasswordPage() {
	const router = useRouter();
	const searchParams = useSearchParams();
	const [password, setPassword] = useState("");
	const [confirmPassword, setConfirmPassword] = useState("");
	const [loading, setLoading] = useState(false);
	const [error, setError] = useState("");
	const [success, setSuccess] = useState(false);
	const [tokenError, setTokenError] = useState("");

	const token = searchParams.get("token");

	useEffect(() => {
		const errorParam = searchParams.get("error");
		if (errorParam === "INVALID_TOKEN") {
			setTokenError("This password reset link is invalid or has expired.");
		} else if (!token && !errorParam) {
			setTokenError("No reset token provided. Please request a new password reset.");
		}
	}, [token, searchParams]);

	async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
		e.preventDefault();
		setLoading(true);
		setError("");

		// Validate passwords match
		if (password !== confirmPassword) {
			setError("Passwords do not match");
			setLoading(false);
			return;
		}

		// Validate password length
		if (password.length < 8) {
			setError("Password must be at least 8 characters long");
			setLoading(false);
			return;
		}

		if (!token) {
			setError("No reset token provided");
			setLoading(false);
			return;
		}

		try {
			const { data, error: resetError } = await authClient.resetPassword({
				newPassword: password,
				token,
			});

			if (resetError) {
				const errorMessage = resetError.message || "Failed to reset password. Please try again.";
				setError(errorMessage);
			} else {
				setSuccess(true);
				// Redirect to login after 2 seconds
				setTimeout(() => {
					router.push("/login");
				}, 2000);
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
					<div className="inline-flex items-center justify-center w-16 h-16 rounded-2xl bg-linear-to-br from-brand-400 to-brand-600 shadow-lg shadow-brand-500/30 mb-4">
						<i className="ri-lock-unlock-line text-3xl text-white" />
					</div>
					<h1 className="text-2xl font-bold text-slate-900 dark:text-slate-100">Set new password</h1>
					<p className="text-slate-500 dark:text-slate-400 mt-1">
						Your new password must be different from previous passwords
					</p>
				</div>

				{/* Form Card */}
				<div className="card p-6 sm:p-8">
					{tokenError ? (
						<div className="text-center space-y-4">
							<div className="w-16 h-16 rounded-full bg-red-100 dark:bg-red-900/30 flex items-center justify-center mx-auto">
								<i className="ri-error-warning-line text-3xl text-red-600 dark:text-red-400" />
							</div>
							<div>
								<h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100 mb-2">
									Invalid or expired link
								</h3>
								<p className="text-slate-500 dark:text-slate-400 text-sm">
									{tokenError}
								</p>
							</div>
							<div className="space-y-3 pt-4">
								<Link href="/forgot-password" className="btn-primary w-full py-3">
									Request new reset link
								</Link>
								<Link href="/login" className="btn-secondary w-full py-3">
									<i className="ri-arrow-left-line" />
									Back to login
								</Link>
							</div>
						</div>
					) : success ? (
						<div className="text-center space-y-4">
							<div className="w-16 h-16 rounded-full bg-green-100 dark:bg-green-900/30 flex items-center justify-center mx-auto">
								<i className="ri-checkbox-circle-line text-3xl text-green-600 dark:text-green-400" />
							</div>
							<div>
								<h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100 mb-2">
									Password reset successful!
								</h3>
								<p className="text-slate-500 dark:text-slate-400 text-sm">
									Your password has been updated. Redirecting to login...
								</p>
							</div>
							<div className="pt-4">
								<Link href="/login" className="btn-primary w-full py-3">
									Continue to login
								</Link>
							</div>
						</div>
					) : (
						<form onSubmit={handleSubmit} className="space-y-5">
							<div>
								<label htmlFor="password" className="label">
									New Password
								</label>
								<input
									required
									type="password"
									id="password"
									name="password"
									className="input"
									placeholder="••••••••"
									value={password}
									onChange={(e) => setPassword(e.target.value)}
									minLength={8}
								/>
								<p className="text-xs text-slate-500 dark:text-slate-400 mt-1.5">
									Must be at least 8 characters
								</p>
							</div>

							<div>
								<label htmlFor="confirmPassword" className="label">
									Confirm Password
								</label>
								<input
									required
									type="password"
									id="confirmPassword"
									name="confirmPassword"
									className="input"
									placeholder="••••••••"
									value={confirmPassword}
									onChange={(e) => setConfirmPassword(e.target.value)}
									minLength={8}
								/>
							</div>

							{error && (
								<div className="flex items-center gap-2 p-3 rounded-xl bg-red-50 dark:bg-red-950 text-red-600 dark:text-red-400 text-sm">
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
										Resetting password...
									</>
								) : (
									<>
										Reset password
										<i className="ri-check-line" />
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
					)}
				</div>
			</div>
		</div>
	);
}
