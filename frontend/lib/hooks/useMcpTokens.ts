"use client";

import { useState, useCallback } from "react";
import { mcpTokenApi, McpTokenApiError } from "@/lib/api/mcp-tokens";
import type { McpTokenDto, CreateMcpTokenCommand, CreateMcpTokenResult, McpUsageAnalyticsResult } from "@/types/mcp";

export function useMcpTokens() {
  const [tokens, setTokens] = useState<McpTokenDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchTokens = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await mcpTokenApi.getMyTokens();
      setTokens(data);
    } catch (err) {
      const message = err instanceof McpTokenApiError ? err.message : "Failed to fetch tokens";
      setError(message);
    } finally {
      setLoading(false);
    }
  }, []);

  const createToken = useCallback(async (command: CreateMcpTokenCommand): Promise<CreateMcpTokenResult | null> => {
    setLoading(true);
    setError(null);
    try {
      const result = await mcpTokenApi.createToken(command);
      await fetchTokens(); // Refresh list
      return result;
    } catch (err) {
      const message = err instanceof McpTokenApiError ? err.message : "Failed to create token";
      setError(message);
      return null;
    } finally {
      setLoading(false);
    }
  }, [fetchTokens]);

  const revokeToken = useCallback(async (tokenId: string): Promise<boolean> => {
    setLoading(true);
    setError(null);
    try {
      await mcpTokenApi.revokeToken(tokenId);
      await fetchTokens(); // Refresh list
      return true;
    } catch (err) {
      const message = err instanceof McpTokenApiError ? err.message : "Failed to revoke token";
      setError(message);
      return false;
    } finally {
      setLoading(false);
    }
  }, [fetchTokens]);

  return {
    tokens,
    loading,
    error,
    fetchTokens,
    createToken,
    revokeToken,
  };
}

export function useMcpAnalytics() {
  const [analytics, setAnalytics] = useState<McpUsageAnalyticsResult | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchAnalytics = useCallback(async (startDate?: Date, endDate?: Date) => {
    setLoading(true);
    setError(null);
    try {
      const data = await mcpTokenApi.getAnalytics(startDate, endDate);
      setAnalytics(data);
    } catch (err) {
      const message = err instanceof McpTokenApiError ? err.message : "Failed to fetch analytics";
      setError(message);
    } finally {
      setLoading(false);
    }
  }, []);

  return {
    analytics,
    loading,
    error,
    fetchAnalytics,
  };
}
