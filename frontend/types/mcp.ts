// Temporary type definitions for MCP tokens
// These will be replaced by code generation from OpenAPI spec

export interface McpTokenDto {
  id: string;
  name: string;
  createdAt: string;
  expiresAt: string | null;
  lastUsedAt: string | null;
  isActive: boolean;
}

export interface CreateMcpTokenCommand {
  name: string;
  expiresAt?: string | null;
}

export interface CreateMcpTokenResult {
  id: string;
  plaintextToken: string;
  name: string;
  createdAt: string;
  expiresAt: string | null;
}

export interface GetMcpTokensResult {
  items: McpTokenDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface ValidateTokenCommand {
  token: string;
}

export interface ValidateTokenResult {
  userId: string;
  isValid: boolean;
}

export interface McpUsageAnalyticsResult {
  overview: UsageOverview;
  toolUsage: ToolUsageDto[];
  tokenUsage: TokenUsageDto[];
  dailyUsage: DailyUsageDto[];
}

export interface UsageOverview {
  totalCalls: number;
  successfulCalls: number;
  failedCalls: number;
  successRate: number;
  averageExecutionTimeMs: number;
  uniqueTokensUsed: number;
}

export interface ToolUsageDto {
  toolName: string;
  callCount: number;
  successCount: number;
  failureCount: number;
  averageExecutionTimeMs: number;
}

export interface TokenUsageDto {
  tokenId: string;
  tokenName: string;
  callCount: number;
  lastUsed: string;
}

export interface DailyUsageDto {
  date: string;
  callCount: number;
  successCount: number;
  failureCount: number;
}
