"use client";

import type { WorkoutSummaryDto, WorkoutTemplateDto } from "@/types/workout";

const START_ROW =
  "flex min-h-24 w-full items-center gap-5 px-4 py-[22px] text-left hover:bg-muted [&>svg]:shrink-0 [&>svg]:text-muted-foreground";

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
        <button className={START_ROW} onClick={onEmpty}>
          <span className="flex-1">
            <span className="block font-semibold">Empty workout</span>
            <span className="block text-sm text-muted-foreground mt-1">
              Add exercises and record sets as you train.
            </span>
          </span>
        </button>
        {lastWorkout && (
          <button
            className={START_ROW}
            onClick={() => onRepeat(lastWorkout)}
          >
            <span className="flex-1">
              <span className="block font-semibold">Repeat last workout</span>
              <span className="block text-sm text-muted-foreground mt-1">
                {lastWorkout.name || "Previous workout"}
              </span>
            </span>
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
                className={START_ROW}
                onClick={() => onTemplate(template.id)}
              >
                <span className="flex-1">
                  <span className="block font-medium">{template.name}</span>
                  {template.programName && (
                    <span className="text-sm text-muted-foreground mt-1">
                      {template.programName}
                    </span>
                  )}
                </span>
              </button>
            ))}
          </div>
        ) : (
          <p className="text-sm text-muted-foreground">
            No saved templates yet. Start an empty workout to build your
            session.
          </p>
        )}
      </section>
    </div>
  );
}
