"use client";

import { Plus, Repeat2, ArrowRight } from "lucide-react";
import type { WorkoutSummaryDto, WorkoutTemplateDto } from "@/types/workout";

export default function StartWorkoutPanel({
  templates,
  lastWorkout,
  onTemplate,
  onRepeat,
  onEmpty,
}: {
  templates: WorkoutTemplateDto[];
  lastWorkout?: WorkoutSummaryDto;
  onTemplate: (id: string) => void;
  onRepeat: (workout: WorkoutSummaryDto) => void;
  onEmpty: () => void;
}) {
  return (
    <div className="space-y-8">
      <div className="border-y divide-y">
        <button className="workout-start-row" onClick={onEmpty}>
          <Plus size={22} />
          <span className="flex-1">
            <span className="block font-semibold">Empty workout</span>
            <span className="block text-sm log-muted mt-1">
              Add exercises and record sets as you train.
            </span>
          </span>
          <ArrowRight size={17} />
        </button>
        {lastWorkout && (
          <button
            className="workout-start-row"
            onClick={() => onRepeat(lastWorkout)}
          >
            <Repeat2 size={22} />
            <span className="flex-1">
              <span className="block font-semibold">Repeat last workout</span>
              <span className="block text-sm log-muted mt-1">
                {lastWorkout.name || "Previous workout"}
              </span>
            </span>
            <ArrowRight size={17} />
          </button>
        )}
      </div>
      <section id="programs">
        <h2 className="text-xl font-semibold mb-4">Your templates</h2>
        {templates.length ? (
          <div className="border-t divide-y">
            {templates.map((template) => (
              <button
                key={template.id}
                className="workout-start-row"
                onClick={() => onTemplate(template.id)}
              >
                <span className="flex-1">
                  <span className="block font-medium">{template.name}</span>
                  {template.programName && (
                    <span className="text-sm log-muted mt-1">
                      {template.programName}
                    </span>
                  )}
                </span>
                <ArrowRight size={17} />
              </button>
            ))}
          </div>
        ) : (
          <p className="text-sm log-muted">
            No saved templates yet. Start an empty workout to build your
            session.
          </p>
        )}
      </section>
    </div>
  );
}
