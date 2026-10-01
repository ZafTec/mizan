import type { components } from "@/types/api.generated";

type Schemas = components["schemas"];

export type AchievementListResultDto = Schemas["GetAchievementsResult"];
export type AchievementDto = Schemas["AchievementDto"];
export type StreakDto = Schemas["GetStreakResult"];

export type StreakUpdateDto = Schemas["StreakUpdate"];
export type UnlockedAchievementDto = Schemas["UnlockedAchievement"];

export type CreateAchievementCommandDto = Schemas["CreateAchievementCommand"];
export type UpdateAchievementCommandDto = Schemas["UpdateAchievementCommand"];
export type AchievementAnalyticsDto = Schemas["GetAchievementAnalyticsResult"];
export type AchievementAnalyticsRowDto = Schemas["AchievementAnalyticsRow"];
export type AchievementAnalyticsCategoryDto = Schemas["CategoryBreakdown"];

export type LogBodyMeasurementResultDto = Schemas["LogBodyMeasurementResult"];

export type MyTrainerDto = Schemas["MyTrainerDto"];
export type MyTrainerRequestDto = Schemas["MyTrainerRequestDto"];
export type MyTrainerRequestPagedResultDto = Schemas["MyTrainerRequestDtoPagedResult"];
export type TrainerClientDto = Schemas["TrainerClientDto"];
export type TrainerClientPagedResultDto = Schemas["TrainerClientDtoPagedResult"];
export type TrainerPendingRequestDto = Schemas["TrainerPendingRequestDto"];
export type TrainerPendingRequestPagedResultDto = Schemas["TrainerPendingRequestDtoPagedResult"];
export type TrainerPublicDto = Schemas["TrainerPublicDto"];
export type TrainerPublicPagedResultDto = Schemas["TrainerPublicDtoPagedResult"];

export type McpConnectionDto = Schemas["McpConnectionDto"];
export type McpConnectionHouseholdDto = Schemas["McpConnectionHouseholdDto"];
export type McpUsageAnalyticsResultDto = Schemas["McpUsageAnalyticsResult"];
export type ScopeGroupDto = Schemas["ScopeGroupView"];
export type AuthorizationRequestViewDto = Schemas["AuthorizationRequestView"];
export type DecideAuthorizationResultDto = Schemas["DecideAuthorizationResult"];

export function getPagedItems<T>(result: { items?: T[] | null } | null | undefined): T[] {
  return result?.items ?? [];
}
