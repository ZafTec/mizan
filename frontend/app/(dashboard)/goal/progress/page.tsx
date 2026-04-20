"use client";

import { useState } from "react";
import Loading from "@/components/Loading";
import { clientApi } from "@/lib/api.client";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { appToast } from "@/lib/toast";
import { AppFeatureIllustration } from "@/components/illustrations/AppFeatureIllustration";

export default function LogProgress() {
  const router = useRouter();
  const [loading, setLoading] = useState(false);
  const [formData, setFormData] = useState({
    actualCalories: "",
    actualProteinGrams: "",
    actualCarbsGrams: "",
    actualFatGrams: "",
    actualWeight: "",
    notes: "",
  });

  function handleChange(e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) {
    setFormData({
      ...formData,
      [e.target.name]: e.target.value,
    });
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setLoading(true);

    try {
		await clientApi("/api/Goals/progress", {
			method: "POST",
        body: {
          actualCalories: parseInt(formData.actualCalories),
          actualProteinGrams: parseFloat(formData.actualProteinGrams),
          actualCarbsGrams: parseFloat(formData.actualCarbsGrams),
          actualFatGrams: parseFloat(formData.actualFatGrams),
          actualWeight: formData.actualWeight ? parseFloat(formData.actualWeight) : null,
          notes: formData.notes || null,
        },
		});

		appToast.success("Progress saved");
		router.push("/goal/dashboard");
	} catch (error) {
		console.error("Failed to log progress:", error);
		appToast.error(error, "Failed to log progress. Please try again.");
	} finally {
		setLoading(false);
	}
  }

  return (
    <div className="max-w-3xl mx-auto space-y-6">
      {/* Header */}
      <header className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <div className="hidden w-20 sm:block opacity-95">
            <AppFeatureIllustration variant="progress" className="h-auto w-full" />
          </div>
          <div className="space-y-2">
            <p className="eyebrow">Log</p>
            <h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
              Log today's progress
            </h1>
            <p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
              Record your daily nutrition intake.
            </p>
          </div>
        </div>
        <Link href="/goal/dashboard" className="btn-secondary">
          <i className="ri-arrow-left-line" />
          Back to Dashboard
        </Link>
      </header>

      {/* Quick Tips */}
		<div className="card p-6 bg-white/70 dark:bg-charcoal-blue-950/60">
        <div className="flex items-start gap-4">
          <div className="w-12 h-12 rounded-xl bg-white dark:bg-slate-900 flex items-center justify-center shrink-0">
            <i className="ri-lightbulb-line text-xl text-brand-600 dark:text-brand-400" />
          </div>
          <div>
            <h3 className="font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100 mb-1">Pro Tips</h3>
            <ul className="text-sm text-slate-600 dark:text-slate-400 space-y-1">
              <li>• Track your meals throughout the day for accuracy</li>
              <li>• Use the meal diary to calculate totals automatically</li>
              <li>• Add notes about how you felt or what worked well</li>
            </ul>
          </div>
        </div>
      </div>

      <form onSubmit={handleSubmit} className="space-y-6">
        {/* Macros Card */}
        <div className="card p-6 space-y-5">
          <h2 className="font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100 flex items-center gap-2">
            <i className="ri-pie-chart-2-line text-brand-500" />
            Daily Totals
          </h2>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label htmlFor="actualCalories" className="label">
                <span className="flex items-center gap-1.5">
                  <span className="w-2 h-2 rounded-xl bg-burnt-peach-500" />
                  Calories (kcal) *
                </span>
              </label>
              <input
                type="number"
                id="actualCalories"
                name="actualCalories"
                value={formData.actualCalories}
                onChange={handleChange}
                min={0}
                required
                placeholder="2000"
                className="input"
              />
            </div>

            <div>
              <label htmlFor="actualProteinGrams" className="label">
                <span className="flex items-center gap-1.5">
                  <span className="w-2 h-2 rounded-xl bg-verdigris-500" />
                  Protein (g) *
                </span>
              </label>
              <input
                type="number"
                id="actualProteinGrams"
                name="actualProteinGrams"
                value={formData.actualProteinGrams}
                onChange={handleChange}
                min={0}
                step="0.1"
                required
                placeholder="150"
                className="input"
              />
            </div>

            <div>
              <label htmlFor="actualCarbsGrams" className="label">
                <span className="flex items-center gap-1.5">
                  <span className="w-2 h-2 rounded-xl bg-tuscan-sun-500" />
                  Carbs (g) *
                </span>
              </label>
              <input
                type="number"
                id="actualCarbsGrams"
                name="actualCarbsGrams"
                value={formData.actualCarbsGrams}
                onChange={handleChange}
                min={0}
                step="0.1"
                required
                placeholder="200"
                className="input"
              />
            </div>

            <div>
              <label htmlFor="actualFatGrams" className="label">
                <span className="flex items-center gap-1.5">
                  <span className="w-2 h-2 rounded-xl bg-sandy-brown-500" />
                  Fat (g) *
                </span>
              </label>
              <input
                type="number"
                id="actualFatGrams"
                name="actualFatGrams"
                value={formData.actualFatGrams}
                onChange={handleChange}
                min={0}
                step="0.1"
                required
                placeholder="65"
                className="input"
              />
            </div>
          </div>
        </div>

        {/* Optional Fields Card */}
        <div className="card p-6 space-y-5">
          <h2 className="font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100 flex items-center gap-2">
            <i className="ri-more-line text-brand-500" />
            Additional Info
          </h2>

          <div>
            <label htmlFor="actualWeight" className="label">
              Weight (kg) - Optional
            </label>
            <input
              type="number"
              id="actualWeight"
              name="actualWeight"
              value={formData.actualWeight}
              onChange={handleChange}
              min={0}
              step="0.1"
              placeholder="70.5"
              className="input"
            />
            <p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400 mt-1">Track your weight to see correlations with your nutrition</p>
          </div>

          <div>
            <label htmlFor="notes" className="label">
              Notes - Optional
            </label>
            <textarea
              id="notes"
              name="notes"
              value={formData.notes}
              onChange={handleChange}
              rows={3}
              placeholder="How did you feel today? Any challenges or wins?"
              className="input resize-none"
            />
          </div>
        </div>

        {/* Submit Button */}
        <button
          type="submit"
          disabled={loading}
          className="btn-primary w-full py-3"
        >
          {loading ? (
            <>
              <Loading size="sm" />
              Saving Progress...
            </>
          ) : (
            <>
              <i className="ri-save-line text-xl" />
              Save Progress
            </>
          )}
        </button>
      </form>
    </div>
  );
}
