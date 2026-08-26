"use client";

import { useCallback, useRef, useState } from "react";
import { resolvePublicApiOrigin } from "@/lib/api-base";
import { appToast } from "@/lib/toast";

export type UploadFolder = "avatars" | "recipes";

/** JPEG, PNG, WebP and GIF - the same set the API sniffs for. */
const ACCEPT = "image/jpeg,image/png,image/webp,image/gif";

/**
 * Uploads go to our API, which validates the bytes and stores them in S3 -
 * MinIO or R2, the browser cannot tell and never holds a storage credential.
 * Replaces the Cloudinary widget; see docs/REFOCUS.md §7.
 *
 * Headless on purpose: every call site already has its own button and preview
 * layout, and this only supplies the behaviour.
 */
export function useImageUpload({
	folder,
	onUploaded,
}: {
	folder: UploadFolder;
	onUploaded: (url: string) => void;
}) {
	const inputRef = useRef<HTMLInputElement>(null);
	const [uploading, setUploading] = useState(false);

	const open = useCallback(() => inputRef.current?.click(), []);

	const handleChange = useCallback(
		async (event: React.ChangeEvent<HTMLInputElement>) => {
			const file = event.target.files?.[0];
			// Reset immediately so picking the same file twice still fires.
			event.target.value = "";
			if (!file) return;

			setUploading(true);
			try {
				const body = new FormData();
				body.append("file", file);

				const response = await fetch(
					`${resolvePublicApiOrigin()}/api/Uploads/image?folder=${folder}`,
					{ method: "POST", credentials: "include", body },
				);

				if (!response.ok) {
					const payload = await response.json().catch(() => null);
					throw new Error(payload?.error ?? "The upload failed. Try again.");
				}

				const { url } = (await response.json()) as { key: string; url: string };
				onUploaded(url);
			} catch (error) {
				appToast.error(error, "The upload failed. Try again.");
			} finally {
				setUploading(false);
			}
		},
		[folder, onUploaded],
	);

	const input = (
		<input
			ref={inputRef}
			type="file"
			accept={ACCEPT}
			className="hidden"
			onChange={handleChange}
		/>
	);

	return { open, uploading, input };
}
