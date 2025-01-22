"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useSearchParams, useRouter } from "next/navigation";
import Loading from "@/components/Loading";

export default function Page() {
	const [token, setToken] = useState("");
	const [verified, setVerified] = useState(false);
	const [error, setError] = useState(false);
    const router = useRouter();

	const [countdown, setCountdown] = useState(8); // Countdown from 8 seconds

	const searchParams = useSearchParams();

	async function verifyUserEmail() {
		try {
			await fetch("/api/auth/verifyemail", {
				method: "POST",
				headers: {
					"Content-Type": "application/json",
				},
				body: JSON.stringify({ token }),
			});
			setVerified(true);
		} catch (error: any) {
			setError(true);
			console.error(error.response.data);
		}
	}

	useEffect(() => {
		const token = searchParams.get("token");
		setToken(token || "");
	}, []);

	useEffect(() => {
		if (token.length > 0) {
			verifyUserEmail();
		}
	}, [token]);

	// Start the countdown when verified is true
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

			// Cleanup the interval on component unmount
			return () => clearInterval(timer);
		}
	}, [verified]);

	// Calculate the progress for the circular countdown
	const radius = 45;
	const circumference = 2 * Math.PI * radius;
	const progress = ((8 - countdown) / 8) * 100;
	const offset = circumference - (progress / 100) * circumference;

	return (
		<div className="flex flex-col items-center justify-center mt-8 py-2">
			
			{!verified && !error && (
				<div className="flex flex-col items-center justify-center m-2 gap-4">
					<h1 className="text-4xl">Verifing Email ...</h1>
					<Loading />
				</div>
			)}
			

			{/* Verified State with Circular Countdown */}
			{verified && (
				<div className="flex flex-col items-center">
					<h2 className="text-2xl mb-4">Email Verified</h2>
					<div className="relative">
						<svg width="100" height="100" className="-rotate-90">
							<circle
								cx="50"
								cy="50"
								r={radius}
								stroke="lightgrey"
								strokeWidth="10"
								fill="none"
							/>
							<circle
								cx="50"
								cy="50"
								r={radius}
								stroke="#f03355"
								strokeWidth="10"
								fill="none"
								strokeDasharray={circumference}
								strokeDashoffset={offset}
							/>
						</svg>
						<div className="absolute top-0 left-0 w-full h-full flex items-center justify-center">
							<span className="text-xl font-semibold">{countdown}</span>
						</div>
					</div>
					<p className="mt-4">Redirecting to login in {countdown} seconds...</p>
					<Link href="/login" className="text-blue-500 mt-2">
						Go to Login
					</Link>
				</div>
			)}

			{error && (
				<div>
					<h2 className="text-2xl bg-red-500">Error</h2>
				</div>
			)}
		</div>
	)
}
