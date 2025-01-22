
"use client";

import { FieldError } from "@/components/FieldError";
import { EMPTY_FORM_STATE } from "@/helper/FormErrorHandler";
import { addUser } from "@/data/user";
import { useActionState, useEffect, useState } from "react";
import SubmitButton from "@/components/AddIngredient/button";
import { CldUploadWidget } from "next-cloudinary";
import Image from "next/image";
import Link from "next/link";
import { useRouter } from "next/navigation";


const MAX_IMAGES_TO_PREVIEW = 1;


export default function Page() {
	const router = useRouter();
	const [formState, action] = useActionState(addUser, EMPTY_FORM_STATE);
	const [images, setImages] = useState<string[]>([]);
	

	useEffect(() => {
		if (formState.status === "SUCCESS") {
			router.push("/login");
		}
	}, [formState.status]);

	return (
		<div className="flex flex-col gap-4 w-full md:mx-auto md:max-w-fit">
			<h1 className="text-4xl font-bold">Register</h1>

			<form action={action} className="flex flex-col gap-4">
				<div className="flex flex-row gap-2">
					<div className="flex flex-1 flex-col gap-2">
						<label htmlFor="email">Email <span className="text-xs text-red-500">*required</span></label>
						<input
							required
							type="email"
							id="email"
							name="email"
							className="border-2 border-gray-300 rounded-lg p-2"
							placeholder="Email"
							defaultValue={formState.fieldValues?.email}
						/>
						<FieldError formState={formState} name="email" />
					</div>
				</div>

				<div className="flex flex-col md:flex-row gap-2">
					<div className="flex flex-col gap-2">
						<label htmlFor="password">Password <span className="text-xs text-red-500">*required</span></label>
						<input required type="password" id="password" name="password" className="border-2 border-gray-300 rounded-lg p-2" placeholder="Password" />
						<FieldError formState={formState} name="password" />
					</div>
					<div className="flex flex-col gap-2">
						<label htmlFor="confirmPassword">Confirm Password <span className="text-xs text-red-500">*required</span></label>
						<input required type="password" id="confirmPassword" name="confirmPassword" className="border-2 border-gray-300 rounded-lg p-2" placeholder="Confirm Password" />
						<FieldError formState={formState} name="confirmPassword" />
					</div>
				</div>

				<div className="flex flex-row gap-2">
					<div>
						<label className="block text-sm font-medium mb-1" htmlFor="userImage">Profile Image <span className="text-xs text-blue-500">[optional]</span></label> 
						<CldUploadWidget
							onSuccess={(result) => {
								if (result?.info && result.info instanceof Object) {
									// setImages([...images, result.info.secure_url]);
									setImages((prevImages) => {
										if (result?.info && result.info instanceof Object) {
											// return [...prevImages, result.info.secure_url]
											return [result.info.secure_url];
										}
										return prevImages;
									});
								}
								// widget.close();
							}}
							signatureEndpoint="/api/sign-cloudinary-params"
						>
							{({ open }) => {
								return (
									<div className="flex flex-row gap-2 items-center">
										<button
											type="button"
											onClick={(e: React.MouseEvent<HTMLButtonElement>) => {
												e.stopPropagation();
												e.preventDefault();
												open()
											}}
											className="bg-emerald-50 border-emerald-700 border-4 text-emerald-700 w-24 h-24 items-center rounded-md hover:bg-emerald-100 hover:border-emerald-600"
										>
											<i className="ri-upload-cloud-2-line ri-3x"></i>
											<span className="sr-only">Upload Image</span>
										</button>
										{images.map((image, index) => {

											if (typeof image !== 'string' || index >= MAX_IMAGES_TO_PREVIEW) {
												return null;
											}
											
											return (
												<div key={index} className="relative w-24 h-24" draggable="false">
													<Image draggable="false" key={index} src={image} alt="Recipe image thumbnail" fill className="w-24 h-24 object-cover rounded-md m-1" />
													<button
														type="button"
														onClick={() => setImages(images.filter((_, i) => i !== index))}
														className="absolute items-center -top-3 -right-3 text-sm w-6 h-6 bg-red-600 opacity-70 text-white hover:opacity-85 rounded-full"
													>
														<i className="ri-close-line"></i>
													</button>
												</div>
											);
										})}

										{images.length > MAX_IMAGES_TO_PREVIEW && 
											<div className="w-24 h-24 bg-gray-200 flex items-center justify-center rounded-md">
												<p className="text-gray-500">+{images.length - MAX_IMAGES_TO_PREVIEW}</p>
											</div>
										}
									</div>
								);
							}}
						</CldUploadWidget>
						<input type="hidden" name="userImage" value={images[0]} />
					</div>
				</div>
				<SubmitButton label="Submit" loading={<div>Loading...</div>} />
				<Link href="/login" className="text-blue-500">
					Already have an account? Login
				</Link>

				<div>
					{formState.status === "SUCCESS" && (
						<div className="text-emerald-700">User created successfully!</div>
					)}
				</div>
			</form>
		</div>
	)
}
