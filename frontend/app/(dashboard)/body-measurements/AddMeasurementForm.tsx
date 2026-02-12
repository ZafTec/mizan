"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { clientApi } from "@/lib/api.client";

export default function AddMeasurementForm() {
    const router = useRouter();
    const [isOpen, setIsOpen] = useState(false);
    const [isSubmitting, setIsSubmitting] = useState(false);

    const handleSubmit = async (e: React.FormEvent<HTMLFormElement>) => {
        e.preventDefault();
        setIsSubmitting(true);

        const formData = new FormData(e.currentTarget);
        const data = {
            date: formData.get("date") as string,
            weightKg: formData.get("weightKg") ? Number(formData.get("weightKg")) : null,
            bodyFatPercentage: formData.get("bodyFatPercentage") ? Number(formData.get("bodyFatPercentage")) : null,
            muscleMassKg: formData.get("muscleMassKg") ? Number(formData.get("muscleMassKg")) : null,
            waistCm: formData.get("waistCm") ? Number(formData.get("waistCm")) : null,
            hipsCm: formData.get("hipsCm") ? Number(formData.get("hipsCm")) : null,
            chestCm: formData.get("chestCm") ? Number(formData.get("chestCm")) : null,
            armsCm: formData.get("armsCm") ? Number(formData.get("armsCm")) : null,
            thighsCm: formData.get("thighsCm") ? Number(formData.get("thighsCm")) : null,
            notes: formData.get("notes") as string || null,
        };

        try {
            await clientApi("/api/BodyMeasurements", {
                method: "POST",
                body: data,
            });
            setIsOpen(false);
            router.refresh();
        } catch (error) {
            console.error("Failed to add measurement:", error);
            alert("Failed to add measurement");
        } finally {
            setIsSubmitting(false);
        }
    };

    return (
        <>
            <button onClick={() => setIsOpen(true)} className="btn-primary w-full sm:w-auto">
                <i className="ri-add-line" />
                Add Measurement
            </button>

            {isOpen && (
                <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
                    <div className="card max-w-2xl w-full p-6 max-h-[90vh] overflow-y-auto">
                        <div className="flex items-center justify-between mb-6">
                            <h3 className="text-lg font-semibold text-slate-900">Add Measurement</h3>
                            <button
                                onClick={() => setIsOpen(false)}
                                className="text-slate-400 hover:text-slate-600"
                            >
                                <i className="ri-close-line text-xl" />
                            </button>
                        </div>

                        <form onSubmit={handleSubmit} className="space-y-4">
                            <div>
                                <label className="label">Date</label>
                                <input
                                    type="date"
                                    name="date"
                                    required
                                    defaultValue={new Date().toISOString().split("T")[0]}
                                    className="input"
                                />
                            </div>

                            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                                <div>
                                    <label className="label">Weight (kg)</label>
                                    <input type="number" step="0.1" name="weightKg" className="input" />
                                </div>
                                <div>
                                    <label className="label">Body Fat (%)</label>
                                    <input type="number" step="0.1" name="bodyFatPercentage" className="input" />
                                </div>
                                <div>
                                    <label className="label">Muscle Mass (kg)</label>
                                    <input type="number" step="0.1" name="muscleMassKg" className="input" />
                                </div>
                                <div>
                                    <label className="label">Waist (cm)</label>
                                    <input type="number" step="0.1" name="waistCm" className="input" />
                                </div>
                                <div>
                                    <label className="label">Hips (cm)</label>
                                    <input type="number" step="0.1" name="hipsCm" className="input" />
                                </div>
                                <div>
                                    <label className="label">Chest (cm)</label>
                                    <input type="number" step="0.1" name="chestCm" className="input" />
                                </div>
                                <div>
                                    <label className="label">Arms (cm)</label>
                                    <input type="number" step="0.1" name="armsCm" className="input" />
                                </div>
                                <div>
                                    <label className="label">Thighs (cm)</label>
                                    <input type="number" step="0.1" name="thighsCm" className="input" />
                                </div>
                            </div>

                            <div>
                                <label className="label">Notes</label>
                                <textarea name="notes" rows={3} className="input" />
                            </div>

                            <div className="flex gap-3">
                                <button
                                    type="button"
                                    onClick={() => setIsOpen(false)}
                                    className="btn-secondary flex-1"
                                    disabled={isSubmitting}
                                >
                                    Cancel
                                </button>
                                <button
                                    type="submit"
                                    className="btn-primary flex-1"
                                    disabled={isSubmitting}
                                >
                                    {isSubmitting ? "Adding..." : "Add Measurement"}
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            )}
        </>
    );
}
