"use client";

import { FieldError } from "@/components/FieldError";
import { EMPTY_FORM_STATE } from "@/helper/FormErrorHandler";
import { addUser } from "@/data/user";
import { useActionState, useEffect, useState } from "react";
import { CldUploadWidget } from "next-cloudinary";
import Image from "next/image";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Spinner } from "@/components/ui/spinner";
import { PasswordInput } from "@/components/PasswordInput";

export default function Page() {
	const router = useRouter();
	const [formState, action, isPending] = useActionState(addUser, EMPTY_FORM_STATE);
	const [image, setImage] = useState<string>("");
	const [password, setPassword] = useState("");

	useEffect(() => {
		if (formState.status === "success") {
			router.push("/login");
		}
	}, [formState.status, router]);

	return (
		<div className="min-h-[70vh] flex items-center justify-center py-8">
			<div className="w-full max-w-md">
				{/* Header */}
				<div className="text-center mb-8">
					<div className="inline-flex items-center justify-center w-16 h-16 rounded-2xl bg-linear-to-br from-brand-400 to-brand-600 shadow-lg shadow-brand-500/30 dark:shadow-brand-500/15 mb-4">
						<i className="ri-user-add-line text-3xl text-white" />
					</div>
					<h1 className="text-2xl font-bold text-slate-900 dark:text-slate-100">Create your account</h1>
					<p className="text-slate-500 dark:text-slate-400 mt-1">Start your nutrition journey with Mizan</p>
				</div>

				{/* Form Card */}
				<div className="card p-6 sm:p-8">
					<form data-testid="register-form" action={action} className="space-y-5">
						{/* Email */}
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
								defaultValue=""
							/>
							<FieldError formState={formState} name="email" />
						</div>

						{/* Password Fields */}
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

						{/* Profile Image Upload */}
						<div>
							<label className="label">
								Profile Image <span className="text-slate-400 dark:text-slate-500 font-normal">(optional)</span>
							</label>
							<CldUploadWidget
								onSuccess={(result) => {
									if (result?.info && result.info instanceof Object) {
										setImage(result.info.secure_url);
									}
								}}
								signatureEndpoint="/api/sign-cloudinary-params"
							>
								{({ open }) => (
									<div className="flex items-center gap-4">
										{image ? (
											<div className="relative">
												<Image
													src={image}
													alt="Profile"
													width={80}
													height={80}
													className="w-20 h-20 rounded-2xl object-cover border-2 border-slate-200 dark:border-slate-700"
												/>
												<button
													type="button"
													onClick={() => setImage("")}
													className="absolute -top-2 -right-2 w-6 h-6 bg-red-500 text-white rounded-full flex items-center justify-center hover:bg-red-600 transition-colors"
												>
													<i className="ri-close-line text-sm" />
												</button>
											</div>
										) : (
											<button
												type="button"
												onClick={(e) => {
													e.preventDefault();
													open();
												}}
												className="w-20 h-20 rounded-2xl border-2 border-dashed border-slate-300 dark:border-slate-600 hover:border-brand-400 bg-slate-50 dark:bg-slate-800 hover:bg-brand-50 dark:hover:bg-brand-950 flex flex-col items-center justify-center transition-colors group"
											>
												<i className="ri-camera-line text-2xl text-slate-400 dark:text-slate-500 group-hover:text-brand-500" />
												<span className="text-xs text-slate-400 dark:text-slate-500 group-hover:text-brand-500 mt-1">Upload</span>
											</button>
										)}
										<div className="text-sm text-slate-500 dark:text-slate-400">
											<p>Add a profile photo</p>
											<p className="text-xs text-slate-400 dark:text-slate-500">JPG, PNG up to 5MB</p>
										</div>
									</div>
								)}
							</CldUploadWidget>
							<input type="hidden" name="userImage" value={image} />
						</div>

						{/* Terms checkbox */}
						<label className="flex items-start gap-2 text-sm text-slate-600 dark:text-slate-400 cursor-pointer">
							<input type="checkbox" required className="mt-0.5 rounded border-slate-300 dark:border-slate-600 text-brand-600 focus:ring-brand-500" />
							<span>
								I agree to the{" "}
								<span className="text-brand-600 dark:text-brand-400 font-medium">Terms of Service</span>
								{" "}and{" "}
								<span className="text-brand-600 dark:text-brand-400 font-medium">Privacy Policy</span>
							</span>
						</label>

						{/* Success Message */}
						{formState.status === "success" && (
							<div className="flex items-center gap-2 p-3 rounded-xl bg-green-50 dark:bg-green-950 text-green-600 dark:text-green-400 text-sm">
								<i className="ri-checkbox-circle-line text-lg" />
								<span>Account created! Redirecting to login...</span>
							</div>
						)}

						{/* Error Message */}
						{formState.status === "error" && formState.message && (
							<div data-testid="error-message" className="flex items-center gap-2 p-3 rounded-xl bg-red-50 dark:bg-red-950 text-red-600 dark:text-red-400 text-sm">
								<i className="ri-error-warning-line text-lg shrink-0" />
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

						{/* Submit Button */}
						<button
							type="submit"
							disabled={isPending}
							data-testid="register-submit"
							className="btn-primary w-full py-3"
						>
							{isPending ? (
								<>
									<Spinner />
									Creating account...
								</>
							) : (
								<>
									Create Account
									<i className="ri-arrow-right-line" />
								</>
							)}
						</button>
					</form>
				</div>

				{/* Footer */}
				<p className="text-center text-sm text-slate-500 dark:text-slate-400 mt-6">
					Already have an account?{" "}
					<Link href="/login" className="text-brand-600 dark:text-brand-400 font-medium hover:text-brand-700 dark:hover:text-brand-400">
						Sign in
					</Link>
				</p>
			</div>
		</div>
	);
}
