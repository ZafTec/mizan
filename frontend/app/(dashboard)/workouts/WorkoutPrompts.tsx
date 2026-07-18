"use client";

import Image from "next/image";
import { useState } from "react";
import { clientApi } from "@/lib/api.client";
import { appToast } from "@/lib/toast";

export function ResumePrompt({
  onResume,
  onDiscard,
}: {
  onResume: () => void;
  onDiscard: () => void;
}) {
  return (
    <div className="card flex flex-col items-center gap-4 p-5 sm:flex-row">
      <Image
        src="/illustrations/resume-workout.svg"
        alt=""
        width={128}
        height={104}
        className="h-auto w-32"
      />
      <div className="flex-1">
        <h2 className="text-lg font-semibold">Resume workout?</h2>
        <p className="text-sm text-charcoal-blue-500">
          Your most recent in-progress sets were saved.
        </p>
      </div>
      <div className="flex gap-2">
        <button className="btn-primary" onClick={onResume}>
          Resume
        </button>
        <button className="btn-ghost" onClick={onDiscard}>
          Discard
        </button>
      </div>
    </div>
  );
}

type PersonalRecord = {
  exerciseId: string;
  exerciseName: string;
  weightKg: number;
  previousBestKg?: number | null;
};

export function PostWorkout({
  summary,
  defaultPublish,
  onClose,
}: {
  summary: {
    id: string;
    exercises: number;
    sets: number;
    personalRecords: PersonalRecord[];
  };
  defaultPublish: boolean;
  onClose: () => void;
}) {
  const [publish, setPublish] = useState(defaultPublish);
  const [caption, setCaption] = useState("");
  const [saving, setSaving] = useState(false);

  async function finish() {
    if (!publish) {
      onClose();
      return;
    }

    setSaving(true);
    try {
      await clientApi("/api/Social/feed", {
        method: "POST",
        body: {
          type: "WorkoutCompleted",
          workoutId: summary.id,
          templateId: null,
          achievementId: null,
          caption: caption.trim() || null,
        },
      });
      appToast.success("Workout shared");
      onClose();
    } catch (error) {
      appToast.error(error, "Create a social profile before sharing");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="mx-auto max-w-2xl card p-6 text-center sm:p-8">
      <Image
        src="/illustrations/success-check.svg"
        alt=""
        width={144}
        height={144}
        className="mx-auto h-auto w-36"
        priority
      />
      <p className="eyebrow mt-5">Workout complete</p>
      <h1 className="mt-3 text-3xl font-semibold">Strong work</h1>
      <p className="mt-2 text-charcoal-blue-500">
        {summary.exercises} exercises · {summary.sets} sets recorded
      </p>

      {summary.personalRecords.length > 0 && (
        <section className="mt-6 rounded-3xl border border-tuscan-sun-300 bg-tuscan-sun-50 p-5 text-left dark:border-tuscan-sun-500/30 dark:bg-tuscan-sun-950/30">
          <div className="flex items-center gap-3">
            <Image
              src="/illustrations/celebration-pr.svg"
              alt=""
              width={72}
              height={72}
              className="size-18"
            />
            <div>
              <p className="eyebrow">
                New personal{" "}
                {summary.personalRecords.length === 1 ? "record" : "records"}
              </p>
              <h2 className="mt-1 text-lg font-semibold">
                You moved the ceiling
              </h2>
            </div>
          </div>
          <ul className="mt-4 space-y-2">
            {summary.personalRecords.map((record) => (
              <li
                key={record.exerciseId}
                className="flex items-center justify-between rounded-2xl bg-white/70 px-4 py-3 text-sm dark:bg-charcoal-blue-950/60"
              >
                <strong>{record.exerciseName}</strong>
                <span>
                  {record.weightKg} kg
                  {record.previousBestKg
                    ? ` · +${record.weightKg - record.previousBestKg} kg`
                    : " · first record"}
                </span>
              </li>
            ))}
          </ul>
        </section>
      )}

      <section className="mt-6 space-y-3 rounded-3xl border border-charcoal-blue-200 p-5 text-left dark:border-white/10">
        <label className="flex items-center justify-between gap-4">
          <span>
            <strong className="block">Share to your training circle</strong>
            <small className="text-charcoal-blue-500">
              Only approved followers can see it.
            </small>
          </span>
          <input
            className="toggle"
            type="checkbox"
            checked={publish}
            onChange={(event) => setPublish(event.target.checked)}
          />
        </label>
        {publish && (
          <label className="block">
            <span className="label">Caption</span>
            <textarea
              className="input min-h-24"
              value={caption}
              onChange={(event) => setCaption(event.target.value)}
              placeholder="How did the session feel?"
              maxLength={280}
            />
          </label>
        )}
      </section>

      <div className="mt-7 flex flex-wrap justify-center gap-3">
        <button className="btn-primary" disabled={saving} onClick={finish}>
          {saving ? "Saving…" : publish ? "Share and continue" : "Continue"}
        </button>
        {publish && (
          <button className="btn-secondary" disabled={saving} onClick={onClose}>
            Skip sharing
          </button>
        )}
      </div>
    </div>
  );
}
