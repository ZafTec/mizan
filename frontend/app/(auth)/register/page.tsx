"use client";

import { FieldError } from "@/components/FieldError";
import { EMPTY_FORM_STATE } from "@/helper/FormErrorHandler";
import { addUser } from "@/data/user";
import { useActionState, useEffect, useState } from "react";
import { CldUploadWidget } from "next-cloudinary";
import Image from "next/image";
import Link from "next/link";
import { useRouter } from "next/navigation";

export default function Page() {
	const router = useRouter();
	const [formState, action, isPending] = useActionState(addUser, EMPTY_FORM_STATE);
	const [image, setImage] = useState<string>("");

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
					<div className="inline-flex items-center justify-center w-16 h-16 rounded-2xl bg-gradient-to-br from-brand-400 to-brand-600 shadow-lg shadow-brand-500/30 mb-4">
						<i className="ri-user-add-line text-3xl text-white" />
					</div>
					<h1 className="text-2xl font-bold text-slate-900">Create your account</h1>
					<p className="text-slate-500 mt-1">Start your nutrition journey with Mizan</p>
				</div>

				{/* Form Card */}
				<div className="card p-6 sm:p-8">
					<form action={action} className="space-y-5">
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
								<input
									required
									type="password"
									id="password"
									name="password"
									className="input"
									placeholder="••••••••"
								/>
								<FieldError formState={formState} name="password" />
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
								/>
								<FieldError formState={formState} name="confirmPassword" />
							</div>
						</div>

						{/* Profile Image Upload */}
						<div>
							<label className="label">
								Profile Image <span className="text-slate-400 font-normal">(optional)</span>
							</label>
							<CldUploadWidget
								onSuccess={(result) => {
									if (result?.info && result.info instanceof Object) {
										setImage(result.info.secure_url);
									}
								}}
								signatureEndpoint="/api/sign-cloudinary-params"
								options={{ cloudName: (typeof process !== 'undefined' && process.env["NEXT_PUBLIC_CLOUDINARY_CLOUD_NAME"]) || "" }}
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
													className="w-20 h-20 rounded-2xl object-cover border-2 border-slate-200"
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
												className="w-20 h-20 rounded-2xl border-2 border-dashed border-slate-300 hover:border-brand-400 bg-slate-50 hover:bg-brand-50 flex flex-col items-center justify-center transition-colors group"
											>
												<i className="ri-camera-line text-2xl text-slate-400 group-hover:text-brand-500" />
												<span className="text-xs text-slate-400 group-hover:text-brand-500 mt-1">Upload</span>
											</button>
										)}
										<div className="text-sm text-slate-500">
											<p>Add a profile photo</p>
											<p className="text-xs text-slate-400">JPG, PNG up to 5MB</p>
										</div>
									</div>
								)}
							</CldUploadWidget>
							<input type="hidden" name="userImage" value={image} />
						</div>

						{/* Success Message */}
						{formState.status === "success" && (
							<div className="flex items-center gap-2 p-3 rounded-xl bg-green-50 text-green-600 text-sm">
								<i className="ri-checkbox-circle-line text-lg" />
								<span>Account created! Redirecting to login...</span>
							</div>
						)}

						{/* Error Message */}
						{formState.status === "error" && formState.message && (
							<div className="flex items-center gap-2 p-3 rounded-xl bg-red-50 text-red-600 text-sm">
								<i className="ri-error-warning-line text-lg" />
								<span>{formState.message}</span>
							</div>
						)}

						{/* Submit Button */}
						<button
							type="submit"
							disabled={isPending}
							className="btn-primary w-full py-3"
						>
							{isPending ? (
								<>
									<svg className="animate-spin h-5 w-5" fill="none" viewBox="0 0 24 24">
										<circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
										<path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
									</svg>
									Creating account...
								</>
							) : (
								<>
									Create Account
									<i className="ri-arrow-right-line" />
								</>
							)}
						</button>

						{/* Divider */}
						<div className="relative my-6">
							<div className="absolute inset-0 flex items-center">
								<div className="w-full border-t border-slate-200" />
							</div>
							<div className="relative flex justify-center text-sm">
								<span className="px-2 bg-white text-slate-500">Or sign up with</span>
							</div>
						</div>

						{/* Social Login */}
						<div className="grid grid-cols-2 gap-3">
							<button type="button" className="btn-secondary py-2.5">
								<i className="ri-google-fill text-lg" />
								Google
							</button>
							<button type="button" className="btn-secondary py-2.5">
								<i className="ri-github-fill text-lg" />
								GitHub
							</button>
						</div>
					</form>
				</div>

				{/* Footer */}
				<p className="text-center text-sm text-slate-500 mt-6">
					Already have an account?{" "}
					<Link href="/login" className="text-brand-600 font-medium hover:text-brand-700">
						Sign in
					</Link>
				</p>
			</div>
		</div>
	);
}
