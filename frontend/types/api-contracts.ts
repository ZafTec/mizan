import type { components } from "@/types/api.generated";

type Schemas = components["schemas"];

export type AchievementListResultDto = Schemas["GetAchievementsResult"];
export type AchievementDto = Schemas["AchievementDto"];
export type StreakDto = Schemas["GetStreakResult"];

export type MyTrainerDto = Schemas["MyTrainerDto"];
export type MyTrainerRequestDto = Schemas["MyTrainerRequestDto"];
export type MyTrainerRequestPagedResultDto = Schemas["MyTrainerRequestDtoPagedResult"];
export type TrainerClientDto = Schemas["TrainerClientDto"];
export type TrainerClientPagedResultDto = Schemas["TrainerClientDtoPagedResult"];
export type TrainerPendingRequestDto = Schemas["TrainerPendingRequestDto"];
export type TrainerPendingRequestPagedResultDto = Schemas["TrainerPendingRequestDtoPagedResult"];
export type TrainerPublicDto = Schemas["TrainerPublicDto"];
export type TrainerPublicPagedResultDto = Schemas["TrainerPublicDtoPagedResult"];

export type CreateMcpTokenCommand = Schemas["CreateMcpTokenCommand"];
export type CreateMcpTokenResultDto = Schemas["CreateMcpTokenResult"];
export type McpTokenDto = Schemas["McpTokenDto"];
export type McpTokenPagedResultDto = Schemas["McpTokenDtoPagedResult"];
export type McpUsageAnalyticsResultDto = Schemas["McpUsageAnalyticsResult"];
export type ValidateTokenResultDto = Schemas["ValidateTokenResult"];

export function getPagedItems<T>(result: { items?: T[] | null } | null | undefined): T[] {
  return result?.items ?? [];
}
