"use client";

import { useRef, useState, type FormEvent } from "react";
import Image from "next/image";
import { useRouter } from "next/navigation";
import { ModalShell } from "@/components/ModalShell";
import { useImageUpload } from "@/components/ImageUpload";
import {
	createSystemExercise,
	setExerciseModel,
	updateExercise,
	uploadModel,
	type ExerciseDetails,
} from "@/lib/api/admin-exercises";
import { appToast } from "@/lib/toast";

const CATEGORIES = ["Strength", "Cardio", "Flexibility", "Balance"];
const MAX_MODEL_MB = 30;

export interface EditableExercise extends ExerciseDetails {
	id: string;
	modelUrl?: string | null;
}

/**
 * Create or edit a system exercise, including its picture and its rigged 3D figure. The figure is a .glb file made
 * in Blender: it uploads on its own, and is attached to the exercise when the form is saved.
 */
export default function ExerciseEditor({
	exercise,
	open,
	onClose,
}: {
	exercise?: EditableExercise;
	open: boolean;
	onClose: () => void;
}) {
	const router = useRouter();
	const creating = !exercise;
	const [form, setForm] = useState<ExerciseDetails>(() => ({
		name: exercise?.name ?? "",
		category: exercise?.category ?? "Strength",
		muscleGroup: exercise?.muscleGroup ?? "",
		equipment: exercise?.equipment ?? "",
		description: exercise?.description ?? "",
		videoUrl: exercise?.videoUrl ?? "",
		imageUrl: exercise?.imageUrl ?? "",
	}));
	const [modelUrl, setModelUrl] = useState<string | null>(exercise?.modelUrl ?? null);
	const [modelName, setModelName] = useState<string | null>(null);
	const [modelBusy, setModelBusy] = useState(false);
	const [saving, setSaving] = useState(false);
	const [error, setError] = useState("");
	const modelInput = useRef<HTMLInputElement>(null);

	const image = useImageUpload({
		folder: "exercises",
		onUploaded: (url) => setForm((current) => ({ ...current, imageUrl: url })),
	});

	const set = (field: keyof ExerciseDetails) => (value: string) =>
		setForm((current) => ({ ...current, [field]: value }));

	async function pickModel(file: File | undefined) {
		if (!file) return;
		if (!file.name.toLowerCase().endsWith(".glb")) {
			setError("Choose a .glb file exported from Blender.");
			return;
		}
		if (file.size > MAX_MODEL_MB * 1024 * 1024) {
			setError(`The model must be ${MAX_MODEL_MB} MB or smaller.`);
			return;
		}

		setError("");
		setModelBusy(true);
		try {
			setModelUrl(await uploadModel(file));
			setModelName(file.name);
		} catch (cause) {
			setError(cause instanceof Error ? cause.message : "The upload failed. Try again.");
		} finally {
			setModelBusy(false);
			if (modelInput.current) modelInput.current.value = "";
		}
	}

	async function submit(event: FormEvent) {
		event.preventDefault();
		if (saving || !form.name.trim()) return;
		setSaving(true);
		setError("");

		// Empty fields are sent as nothing, so clearing a field clears it on the server too.
		const details: ExerciseDetails = Object.fromEntries(
			Object.entries(form).map(([key, value]) => [key, typeof value === "string" ? value.trim() || null : value]),
		) as unknown as ExerciseDetails;
		details.name = form.name.trim();

		try {
			let id = exercise?.id;
			if (id) await updateExercise(id, details);
			else id = await createSystemExercise(details);

			if (modelUrl !== (exercise?.modelUrl ?? null)) await setExerciseModel(id, modelUrl);

			appToast.success(creating ? "Exercise created" : "Exercise saved");
			router.refresh();
			onClose();
		} catch (cause) {
			setError(cause instanceof Error ? cause.message : "Could not save the exercise.");
		} finally {
			setSaving(false);
		}
	}

	const busy = saving || modelBusy || image.uploading;

	return (
		<ModalShell open={open} onClose={busy ? () => {} : onClose} closeOnOverlayClick={!busy}>
			<form onSubmit={submit} className="surface-panel max-h-[90dvh] w-full space-y-4 overflow-y-auto p-5" aria-label={creating ? "New exercise" : `Edit ${exercise.name}`}>
				<h3 className="text-base font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
					{creating ? "New exercise" : "Edit exercise"}
				</h3>

				<div>
					<label className="label" htmlFor="ex-name">Name</label>
					<input id="ex-name" className="input" required maxLength={100} value={form.name} onChange={(e) => set("name")(e.target.value)} />
				</div>

				<div className="grid grid-cols-2 gap-3">
					<div>
						<label className="label" htmlFor="ex-category">Category</label>
						<select id="ex-category" className="input" value={form.category} onChange={(e) => set("category")(e.target.value)}>
							{CATEGORIES.map((c) => <option key={c}>{c}</option>)}
						</select>
					</div>
					<div>
						<label className="label" htmlFor="ex-muscle">Muscle group</label>
						<input id="ex-muscle" className="input" maxLength={100} value={form.muscleGroup ?? ""} onChange={(e) => set("muscleGroup")(e.target.value)} />
					</div>
				</div>

				<div>
					<label className="label" htmlFor="ex-equipment">Equipment</label>
					<input id="ex-equipment" className="input" maxLength={100} value={form.equipment ?? ""} onChange={(e) => set("equipment")(e.target.value)} />
				</div>

				<div>
					<label className="label" htmlFor="ex-description">Description</label>
					<textarea id="ex-description" className="input min-h-20" maxLength={2000} value={form.description ?? ""} onChange={(e) => set("description")(e.target.value)} />
				</div>

				<div>
					<label className="label" htmlFor="ex-video">Video link</label>
					<input id="ex-video" type="url" className="input" placeholder="https://" value={form.videoUrl ?? ""} onChange={(e) => set("videoUrl")(e.target.value)} />
				</div>

				<fieldset className="space-y-2">
					<legend className="label">Picture</legend>
					<div className="flex items-center gap-3">
						<div className="relative h-16 w-16 shrink-0 overflow-hidden rounded-xs border border-border bg-muted">
							{form.imageUrl ? (
								<Image src={form.imageUrl} alt="Current exercise picture" fill sizes="64px" className="object-cover" />
							) : (
								<span className="flex h-full items-center justify-center text-xs text-charcoal-blue-500">None</span>
							)}
						</div>
						<div className="flex flex-wrap gap-2">
							<button type="button" className="btn-secondary btn-sm" onClick={image.open} disabled={busy}>
								{image.uploading ? "Uploading…" : form.imageUrl ? "Replace picture" : "Upload picture"}
							</button>
							{form.imageUrl && (
								<button type="button" className="btn-ghost btn-sm" disabled={busy} onClick={() => set("imageUrl")("")}>
									Remove
								</button>
							)}
						</div>
						{image.input}
					</div>
				</fieldset>

				<fieldset className="space-y-2">
					<legend className="label">3D figure</legend>
					<p className="text-xs text-charcoal-blue-600 dark:text-charcoal-blue-300">
						A rigged model exported from Blender as .glb, up to {MAX_MODEL_MB} MB.
					</p>
					<div className="flex flex-wrap items-center gap-2">
						<span className="text-sm" data-testid="model-state">
							{modelBusy ? "Uploading…" : modelUrl ? modelName ?? "A figure is attached" : "No figure"}
						</span>
						{modelUrl && !modelBusy && (
							<a href={modelUrl} target="_blank" rel="noreferrer" className="text-sm underline underline-offset-4">
								Open file
							</a>
						)}
						<button type="button" className="btn-secondary btn-sm" onClick={() => modelInput.current?.click()} disabled={busy}>
							{modelUrl ? "Replace figure" : "Upload figure"}
						</button>
						{modelUrl && (
							<button type="button" className="btn-ghost btn-sm" disabled={busy} onClick={() => { setModelUrl(null); setModelName(null); }}>
								Remove
							</button>
						)}
						<input
							ref={modelInput}
							type="file"
							accept=".glb,model/gltf-binary"
							className="hidden"
							aria-label="3D figure file"
							onChange={(e) => pickModel(e.target.files?.[0])}
						/>
					</div>
				</fieldset>

				{error && <p role="alert" className="text-sm text-burnt-peach-700 dark:text-burnt-peach-300">{error}</p>}

				<div className="flex gap-2">
					<button type="button" onClick={onClose} disabled={busy} className="btn-ghost flex-1">Cancel</button>
					<button type="submit" disabled={busy || !form.name.trim()} className="btn-primary flex-1">
						{saving ? "Saving…" : creating ? "Create exercise" : "Save"}
					</button>
				</div>
			</form>
		</ModalShell>
	);
}
