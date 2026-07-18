"use client";

import type { WorkoutSummaryDto, WorkoutTemplateDto } from "@/types/workout";

export default function StartWorkoutPanel({ templates, lastWorkout, onTemplate, onRepeat, onEmpty }: {
  templates: WorkoutTemplateDto[];
  lastWorkout?: WorkoutSummaryDto;
  onTemplate: (id: string) => void;
  onRepeat: (workout: WorkoutSummaryDto) => void;
  onEmpty: () => void;
}) {
  return <div className="space-y-5"><div className="grid gap-4 md:grid-cols-3"><button className="card-hover press-feedback p-6 text-left" onClick={onEmpty}><span className="icon-chip h-12 w-12"><i className="ri-add-line text-xl" /></span><h2 className="mt-4 font-semibold">Empty workout</h2><p className="mt-1 text-sm text-charcoal-blue-500">Build a session as you train.</p></button><button className="card-hover press-feedback p-6 text-left disabled:opacity-50" disabled={!lastWorkout} onClick={() => lastWorkout && onRepeat(lastWorkout)}><span className="icon-chip h-12 w-12"><i className="ri-repeat-line text-xl" /></span><h2 className="mt-4 font-semibold">Repeat last</h2><p className="mt-1 text-sm text-charcoal-blue-500">{lastWorkout?.name || "No previous workout"}</p></button><button className="card-hover press-feedback p-6 text-left" onClick={() => document.getElementById("programs")?.scrollIntoView({ behavior: "smooth" })}><span className="icon-chip h-12 w-12"><i className="ri-layout-grid-line text-xl" /></span><h2 className="mt-4 font-semibold">From template</h2><p className="mt-1 text-sm text-charcoal-blue-500">Use progression and planned sets.</p></button></div><section id="programs"><h2 className="section-title mb-4">Programs and templates</h2><div className="grid gap-3 md:grid-cols-2">{templates.map((template) => <button key={template.id} className="card-hover press-feedback flex items-center gap-4 p-4 text-left" onClick={() => onTemplate(template.id)}><svg className="size-8 text-brand-700"><use href="/illustrations/icons-workout.svg#wi-template" /></svg><span><strong className="block">{template.name}</strong><span className="text-sm text-charcoal-blue-500">{template.programName || "Custom"}</span></span></button>)}</div></section></div>;
}
