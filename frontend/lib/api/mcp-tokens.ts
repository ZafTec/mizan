import type {
  CreateMcpTokenCommand,
  CreateMcpTokenResult,
  GetMcpTokensResult,
  McpTokenDto,
  McpUsageAnalyticsResult,
} from "@/types/mcp";
import { getApiToken } from "@/lib/api.client";
import { logger } from "@/lib/logger";

const API_BASE = () => {
  const baseUrl = typeof window === "undefined"
    ? process.env.API_URL || ""
    : process.env.NEXT_PUBLIC_API_URL || "";
  return `${baseUrl.replace(/\/$/, "")}/api`;
};

const mcpTokenLogger = logger.createModuleLogger("mcp-token-api");

export class McpTokenApiError extends Error {
  constructor(
    public status: number,
    public message: string,
    public data?: any
  ) {
    super(message);
  }
}

async function fetchApi<T>(
  path: string,
  options: RequestInit = {}
): Promise<T> {
  const startTime = Date.now();

  try {
    const token = await getApiToken();
    const response = await fetch(`${API_BASE()}${path}`, {
      ...options,
      headers: {
        "Content-Type": "application/json",
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
        ...options.headers,
      },
    });

    const duration = Date.now() - startTime;

    if (!response.ok) {
      const error = await response.json().catch(() => ({}));
      mcpTokenLogger.error("MCP API request failed", {
        path,
        status: response.status,
        statusText: response.statusText,
        duration,
      });
      throw new McpTokenApiError(
        response.status,
        error.error || response.statusText,
        error
      );
    }

    mcpTokenLogger.debug("MCP API request successful", {
      path,
      status: response.status,
      duration,
    });

    if (response.status === 204) {
      return {} as T;
    }

    return response.json();
  } catch (error) {
    const duration = Date.now() - startTime;

    if (error instanceof McpTokenApiError) {
      throw error;
    }

    mcpTokenLogger.error("MCP API request exception", {
      path,
      error: error instanceof Error ? error.message : String(error),
      duration,
    });

    throw error;
  }
}

export const mcpTokenApi = {
  async createToken(
    command: CreateMcpTokenCommand
  ): Promise<CreateMcpTokenResult> {
    mcpTokenLogger.info("Creating MCP token", { name: command.name });
    const result = await fetchApi<CreateMcpTokenResult>("/McpTokens", {
      method: "POST",
      body: JSON.stringify(command),
    });
    mcpTokenLogger.info("MCP token created successfully", { tokenId: result.id });
    return result;
  },

  async getMyTokens(): Promise<McpTokenDto[]> {
    mcpTokenLogger.debug("Fetching user MCP tokens");
    const result = await fetchApi<GetMcpTokensResult>("/McpTokens", {
      method: "GET",
    });
    mcpTokenLogger.debug("Retrieved user MCP tokens", { count: result.tokens.length });
    return result.tokens;
  },

  async revokeToken(tokenId: string): Promise<void> {
    mcpTokenLogger.info("Revoking MCP token", { tokenId });
    await fetchApi<void>(`/McpTokens/${tokenId}`, {
      method: "DELETE",
    });
    mcpTokenLogger.info("MCP token revoked successfully", { tokenId });
  },

  async getAnalytics(
    startDate?: Date,
    endDate?: Date
  ): Promise<McpUsageAnalyticsResult> {
    const params = new URLSearchParams();
    if (startDate) {
      params.append("startDate", startDate.toISOString());
    }
    if (endDate) {
      params.append("endDate", endDate.toISOString());
    }

    const queryString = params.toString();
    const path = queryString ? `/McpTokens/analytics?${queryString}` : "/McpTokens/analytics";

    mcpTokenLogger.debug("Fetching MCP usage analytics", { startDate, endDate });
    const result = await fetchApi<McpUsageAnalyticsResult>(path, {
      method: "GET",
    });
    mcpTokenLogger.debug("Retrieved MCP usage analytics");
    return result;
  },
};
