"use client";

import { FieldError } from "@/components/FieldError";
import { EMPTY_FORM_STATE } from "@/helper/FormErrorHandler";
import { addUser } from "@/data/user";
import { useActionState, useEffect, useState } from "react";
import Image from "next/image";
import Link from "next/link";
import { useRouter } from "next/navigation";
import Loading from "@/components/Loading";
import { PasswordInput } from "@/components/PasswordInput";
import { Icon } from "@/components/ui/icon";

export default function Page() {
	const router = useRouter();
	const [formState, action, isPending] = useActionState(addUser, EMPTY_FORM_STATE);
	const [password, setPassword] = useState("");
	const [email, setEmail] = useState("");

	useEffect(() => {
		if (formState.status === "success") {
			const plan = new URLSearchParams(window.location.search).get("plan");
			const dest = plan
				? `/login?callbackUrl=${encodeURIComponent(`/billing?checkout=${plan}`)}`
				: `/verify?email=${encodeURIComponent(email)}`;
			router.push(dest);
		}
	}, [formState.status, router, email]);

	return (
		<div className="min-h-[70vh] flex items-center justify-center py-8">
			<div className="w-full max-w-md">
				<div className="text-center mb-8">
					<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
						Create your account
					</h1>
					<p className="text-charcoal-blue-500 dark:text-charcoal-blue-400 mt-1">
						Start your nutrition journey with Mizan
					</p>
				</div>

				<div className="card p-6 sm:p-8">
					<form data-testid="register-form" action={action} className="space-y-5">
						<div>
							<label htmlFor="email" className="label">
								Email address
							</label>
							<input
								required
								type="email"
								id="email"
								name="email"
								data-testid="register-email"
								className="input"
								placeholder="you@example.com"
								value={email}
								onChange={(e) => setEmail(e.target.value)}
							/>
							<FieldError formState={formState} name="email" />
						</div>

						<div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
							<div>
								<label htmlFor="password" className="label">
									Password
								</label>
								<PasswordInput
									required
									id="password"
									name="password"
									data-testid="register-password"
									className="input pr-10"
									placeholder="••••••••"
									showStrength
									value={password}
									onChange={(e) => setPassword(e.target.value)}
								/>
								<FieldError formState={formState} name="password" />
							</div>
							<div>
								<label htmlFor="confirmPassword" className="label">
									Confirm Password
								</label>
								<PasswordInput
									required
									id="confirmPassword"
									name="confirmPassword"
									data-testid="register-confirm-password"
									className="input pr-10"
									placeholder="••••••••"
								/>
								<FieldError formState={formState} name="confirmPassword" />
							</div>
						</div>

						<label className="flex items-start gap-2 text-sm text-charcoal-blue-600 dark:text-charcoal-blue-400 cursor-pointer">
							<input
								type="checkbox"
								required
								className="mt-0.5 rounded border-charcoal-blue-300 dark:border-charcoal-blue-700 text-brand-600 focus:ring-brand-500"
							/>
							<span>
								I agree to the{" "}
								<a
									href="https://zaftech.co/terms"
									target="_blank"
									rel="noopener noreferrer"
									className="text-brand-600 dark:text-brand-400 font-medium underline-offset-2 hover:underline"
								>
									Terms of Service
								</a>{" "}
								and{" "}
								<a
									href="https://zaftech.co/privacy"
									target="_blank"
									rel="noopener noreferrer"
									className="text-brand-600 dark:text-brand-400 font-medium underline-offset-2 hover:underline"
								>
									Privacy Policy
								</a>
							</span>
						</label>

						{formState.status === "success" && (
							<div className="flex items-center gap-2 p-3 rounded-xl bg-green-50 dark:bg-green-950 text-green-600 dark:text-green-400 text-sm">
								<Icon name="circleCheck" size={18} aria-hidden="true" />
								<span>Account created! Redirecting...</span>
							</div>
						)}

						{formState.status === "error" && formState.message && (
							<div
								data-testid="error-message"
								className="flex items-center gap-2 p-3 rounded-xl bg-red-50 dark:bg-red-950 text-red-600 dark:text-red-400 text-sm"
							>
								<Icon name="badgeAlert" size={18} className="shrink-0" aria-hidden="true" />
								<span>
									{formState.message}
									{formState.message.includes("already exists") && (
										<>
											{" "}
											<Link href="/login" className="font-medium underline hover:text-red-700">
												Sign in instead
											</Link>
										</>
									)}
								</span>
							</div>
						)}

						<button
							type="submit"
							disabled={isPending}
							data-testid="register-submit"
							className="btn-primary w-full py-3"
						>
							{isPending ? (
								<>
									<Loading size="sm" />
									Creating account...
								</>
							) : (
								<>
									Create Account
									<Icon name="arrowRight" size={18} aria-hidden="true" />
								</>
							)}
						</button>
					</form>
				</div>

				<p className="text-center text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400 mt-6">
					Already have an account?{" "}
					<Link
						href="/login"
						className="text-brand-600 dark:text-brand-400 font-medium hover:text-brand-700 dark:hover:text-brand-400"
					>
						Sign in
					</Link>
				</p>
			</div>
		</div>
	);
}
