import type { components } from "@/types/api.generated";

type Schemas = components["schemas"];

export type ExerciseDto = Schemas["ExerciseDto"];
export type WorkoutTemplateDto = Schemas["WorkoutTemplateDto"];
export type NextSessionDto = Schemas["NextSessionDto"];
export type WorkoutSummaryDto = Schemas["WorkoutSummaryDto"];
export type WorkoutStatsDto = Schemas["WorkoutStatsDto"];
