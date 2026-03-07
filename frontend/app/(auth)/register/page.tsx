"use client";

import { FieldError } from "@/components/FieldError";
import { EMPTY_FORM_STATE } from "@/helper/FormErrorHandler";
import { addUser } from "@/data/user";
import { useActionState, useEffect, useState } from "react";
import { CldUploadWidget } from "next-cloudinary";
import Image from "next/image";
import Link from "next/link";
import { useRouter } from "next/navigation";
import Loading from "@/components/Loading";
import { PasswordInput } from "@/components/PasswordInput";
import { AnimatedIcon } from "@/components/ui/animated-icon";

const hasCloudinary = !!process.env.NEXT_PUBLIC_CLOUDINARY_CLOUD_NAME;

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
				<div className="text-center mb-8">
					<div className="mx-auto mb-4 flex h-16 w-16 items-center justify-center rounded-3xl bg-brand-600 text-white shadow-lg shadow-brand-500/25 dark:bg-brand-500">
						<AnimatedIcon name="rocket" size={26} aria-hidden="true" />
					</div>
					<h1 className="text-2xl font-bold text-slate-900 dark:text-slate-100">Create your account</h1>
					<p className="text-slate-500 dark:text-slate-400 mt-1">Start your nutrition journey with Mizan</p>
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
								defaultValue=""
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

						<div>
							<label className="label">
								Profile Image <span className="text-slate-400 dark:text-slate-500 font-normal">(optional)</span>
							</label>
							{hasCloudinary ? (
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
													<AnimatedIcon name="upload" size={22} className="text-slate-400 dark:text-slate-500 group-hover:text-brand-500" aria-hidden="true" />
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
							) : null}
							<input type="hidden" name="userImage" value={image} />
						</div>

						<label className="flex items-start gap-2 text-sm text-slate-600 dark:text-slate-400 cursor-pointer">
							<input type="checkbox" required className="mt-0.5 rounded border-slate-300 dark:border-slate-600 text-brand-600 focus:ring-brand-500" />
							<span>
								I agree to the{" "}
								<span className="text-brand-600 dark:text-brand-400 font-medium">Terms of Service</span>
								{" "}and{" "}
								<span className="text-brand-600 dark:text-brand-400 font-medium">Privacy Policy</span>
							</span>
						</label>

						{formState.status === "success" && (
							<div className="flex items-center gap-2 p-3 rounded-xl bg-green-50 dark:bg-green-950 text-green-600 dark:text-green-400 text-sm">
								<AnimatedIcon name="circleCheck" size={18} aria-hidden="true" />
								<span>Account created! Redirecting to login...</span>
							</div>
						)}

						{formState.status === "error" && formState.message && (
							<div data-testid="error-message" className="flex items-center gap-2 p-3 rounded-xl bg-red-50 dark:bg-red-950 text-red-600 dark:text-red-400 text-sm">
								<AnimatedIcon name="badgeAlert" size={18} className="shrink-0" aria-hidden="true" />
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
									<AnimatedIcon name="arrowRight" size={18} aria-hidden="true" />
								</>
							)}
						</button>
					</form>
				</div>

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
