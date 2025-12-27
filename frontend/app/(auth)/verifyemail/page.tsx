"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useSearchParams, useRouter } from "next/navigation";

export default function Page() {
	const [token, setToken] = useState("");
	const [verified, setVerified] = useState(false);
	const [error, setError] = useState(false);
	const [errorMessage, setErrorMessage] = useState("");
	const [loading, setLoading] = useState(true);
	const router = useRouter();

	const [countdown, setCountdown] = useState(5);

	const searchParams = useSearchParams();

	async function verifyUserEmail() {
		try {
			const response = await fetch("/api/auth/verify-email", {
				method: "POST",
				headers: {
					"Content-Type": "application/json",
				},
				body: JSON.stringify({ token }),
			});

			if (!response.ok) {
				const errorData = await response.json();
				throw new Error(errorData.message || "Verification failed");
			}

			setVerified(true);
		} catch (err: any) {
			setError(true);
			setErrorMessage(err.message || "Failed to verify email. The link may be invalid or expired.");
			console.error(err);
		} finally {
			setLoading(false);
		}
	}

	useEffect(() => {
		const tokenParam = searchParams.get("token");
		if (tokenParam) {
			setToken(tokenParam);
		} else {
			setError(true);
			setErrorMessage("No verification token provided");
			setLoading(false);
		}
	}, [searchParams]);

	useEffect(() => {
		if (token.length > 0) {
			verifyUserEmail();
		}
	}, [token]);

	useEffect(() => {
		if (verified) {
			const timer: NodeJS.Timeout = setInterval(() => {
				setCountdown((prev) => {
					if (prev <= 1) {
						clearInterval(timer);
						router.push("/login");
						return 0;
					}
					return prev - 1;
				});
			}, 1000);

			return () => clearInterval(timer);
		}
	}, [verified, router]);

	return (
		<div className="min-h-[70vh] flex items-center justify-center">
			<div className="w-full max-w-md">
				<div className="card p-6 sm:p-8">
					{loading ? (
						<div className="text-center space-y-4">
							<div className="w-16 h-16 rounded-full bg-brand-100 flex items-center justify-center mx-auto">
								<svg className="animate-spin h-8 w-8 text-brand-600" fill="none" viewBox="0 0 24 24">
									<circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
									<path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
								</svg>
							</div>
							<div>
								<h3 className="text-lg font-semibold text-slate-900 mb-2">
									Verifying your email
								</h3>
								<p className="text-slate-500 text-sm">
									Please wait while we verify your email address...
								</p>
							</div>
						</div>
					) : verified ? (
						<div className="text-center space-y-4">
							<div className="w-16 h-16 rounded-full bg-green-100 flex items-center justify-center mx-auto">
								<i className="ri-checkbox-circle-line text-3xl text-green-600" />
							</div>
							<div>
								<h3 className="text-lg font-semibold text-slate-900 mb-2">
									Email verified successfully!
								</h3>
								<p className="text-slate-500 text-sm">
									Your email has been verified. You can now sign in to your account.
								</p>
							</div>
							<div className="pt-4">
								<div className="text-sm text-slate-500 mb-3">
									Redirecting to login in {countdown} seconds...
								</div>
								<Link href="/login" className="btn-primary w-full py-3">
									Continue to login
								</Link>
							</div>
						</div>
					) : (
						<div className="text-center space-y-4">
							<div className="w-16 h-16 rounded-full bg-red-100 flex items-center justify-center mx-auto">
								<i className="ri-error-warning-line text-3xl text-red-600" />
							</div>
							<div>
								<h3 className="text-lg font-semibold text-slate-900 mb-2">
									Verification failed
								</h3>
								<p className="text-slate-500 text-sm">
									{errorMessage}
								</p>
							</div>
							<div className="space-y-3 pt-4">
								<Link href="/register" className="btn-primary w-full py-3">
									Create new account
								</Link>
								<Link href="/login" className="btn-secondary w-full py-3">
									<i className="ri-arrow-left-line" />
									Back to login
								</Link>
							</div>
						</div>
					)}
				</div>
			</div>
		</div>
	);
}
