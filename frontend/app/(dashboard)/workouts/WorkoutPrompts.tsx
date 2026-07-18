"use client";

import { clientApi } from "@/lib/api.client";
import { appToast } from "@/lib/toast";

export function ResumePrompt({ onResume, onDiscard }: { onResume: () => void; onDiscard: () => void }) {
  return <div className="card flex flex-col items-center gap-4 p-5 sm:flex-row"><img src="/illustrations/resume-workout.svg" alt="" className="w-32" /><div className="flex-1"><h2 className="text-lg font-semibold">Resume workout?</h2><p className="text-sm text-charcoal-blue-500">Your in-progress sets were saved.</p></div><div className="flex gap-2"><button className="btn-primary" onClick={onResume}>Resume</button><button className="btn-ghost" onClick={onDiscard}>Discard</button></div></div>;
}

export function PostWorkout({ summary, onClose }: { summary: { id: string; exercises: number; sets: number }; onClose: () => void }) {
  async function share() {
    try { await clientApi("/api/Social/feed", { method: "POST", body: { type: "WorkoutCompleted", workoutId: summary.id } }); appToast.success("Workout shared"); }
    catch (error) { appToast.error(error, "Create a social profile before sharing"); }
  }
  return <div className="mx-auto max-w-2xl card p-8 text-center"><img src="/illustrations/success-check.svg" alt="" className="mx-auto w-36" /><p className="eyebrow mt-5">Workout complete</p><h1 className="mt-3 text-3xl font-semibold">Strong work</h1><p className="mt-2 text-charcoal-blue-500">{summary.exercises} exercises · {summary.sets} sets recorded</p><div className="mt-7 flex flex-wrap justify-center gap-3"><button className="btn-primary" onClick={share}>Share to feed</button><button className="btn-secondary" onClick={onClose}>Back to history</button></div></div>;
}
