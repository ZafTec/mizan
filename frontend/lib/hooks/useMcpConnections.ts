"use client";

import { useCallback, useState } from "react";
import { mcpConnectionsApi, McpApiError, type HouseholdChoice } from "@/lib/api/mcp";
import type { McpConnection, McpUsageAnalyticsResult, ScopeGroup } from "@/types/mcp";
import { appToast } from "@/lib/toast";

const messageOf = (error: unknown, fallback: string) =>
  error instanceof McpApiError ? error.message : fallback;

export function useMcpConnections() {
  const [connections, setConnections] = useState<McpConnection[]>([]);
  const [groups, setGroups] = useState<ScopeGroup[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setError(null);
    try {
      const [list, scopes] = await Promise.all([mcpConnectionsApi.list(), mcpConnectionsApi.scopes()]);
      setConnections(list);
      setGroups(scopes);
    } catch (caught) {
      setError(messageOf(caught, "Could not load your connected apps"));
    } finally {
      setLoading(false);
    }
  }, []);

  const update = useCallback(
    async (id: string, change: { scopes?: string[] } & Partial<HouseholdChoice>) => {
      try {
        await mcpConnectionsApi.update(id, change);
        await load();
        appToast.success("Access updated");
        return true;
      } catch (caught) {
        appToast.error(messageOf(caught, "Could not update access"));
        return false;
      }
    },
    [load],
  );

  const disconnect = useCallback(
    async (id: string) => {
      try {
        await mcpConnectionsApi.revoke(id);
        await load();
        appToast.success("App disconnected");
        return true;
      } catch (caught) {
        appToast.error(messageOf(caught, "Could not disconnect the app"));
        return false;
      }
    },
    [load],
  );

  return { connections, groups, loading, error, load, update, disconnect };
}

export function useMcpAnalytics() {
  const [analytics, setAnalytics] = useState<McpUsageAnalyticsResult | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    try {
      setAnalytics(await mcpConnectionsApi.analytics());
    } catch {
      setAnalytics(null);
    } finally {
      setLoading(false);
    }
  }, []);

  return { analytics, loading, load };
}
