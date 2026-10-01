import type {
  AuthorizationRequest,
  DecideAuthorizationResult,
  McpConnection,
  McpUsageAnalyticsResult,
  ScopeGroup,
} from "@/types/mcp";
import { resolvePublicApiOrigin } from "@/lib/api-base";

const API_BASE = () => `${resolvePublicApiOrigin()}/api`;

export class McpApiError extends Error {
  constructor(
    public status: number,
    message: string,
  ) {
    super(message);
  }
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const response = await fetch(`${API_BASE()}${path}`, {
    ...init,
    credentials: "include",
    headers: { "Content-Type": "application/json", ...init.headers },
  });

  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    throw new McpApiError(
      response.status,
      body.error || body.message || body.detail || body.title || response.statusText,
    );
  }

  if (response.status === 204) return undefined as T;
  return response.json();
}

export type HouseholdChoice = {
  householdMode: "none" | "selected" | "all";
  householdIds: string[];
};

/** Connected apps and what they may do. */
export const mcpConnectionsApi = {
  list: () => request<McpConnection[]>("/McpConnections"),

  scopes: () => request<ScopeGroup[]>("/McpConnections/scopes"),

  update: (id: string, change: { scopes?: string[] } & Partial<HouseholdChoice>) =>
    request<void>(`/McpConnections/${id}`, { method: "PATCH", body: JSON.stringify(change) }),

  revoke: (id: string) => request<void>(`/McpConnections/${id}`, { method: "DELETE" }),

  analytics: (days = 30) => {
    const params = new URLSearchParams({ startDate: new Date(Date.now() - days * 86_400_000).toISOString() });
    return request<McpUsageAnalyticsResult>(`/McpConnections/analytics?${params}`);
  },
};

/** The consent screen. The request secret goes in the body, never in a URL. */
export const oauthConsentApi = {
  view: (requestSecret: string) =>
    request<AuthorizationRequest>("/oauth/authorization-requests/view", {
      method: "POST",
      body: JSON.stringify({ request: requestSecret }),
    }),

  decide: (
    requestSecret: string,
    decision: { approve: boolean; scopes?: string[] } & Partial<HouseholdChoice>,
  ) =>
    request<DecideAuthorizationResult>("/oauth/authorization-requests/decision", {
      method: "POST",
      body: JSON.stringify({ request: requestSecret, ...decision }),
    }),
};

/** The user's households, by name, for the edit dialog when an app currently sees all of them. */
export async function fetchMyHouseholds(): Promise<{ id: string; name: string }[]> {
  const result = await request<{ households?: { id: string; name: string }[] }>("/Households/mine");
  return result.households ?? [];
}
