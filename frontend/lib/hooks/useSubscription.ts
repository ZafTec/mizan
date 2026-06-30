"use client";

import { useCallback, useEffect, useState } from "react";
import { clientApi } from "@/lib/api.client";
import type { MySubscription } from "@/types/subscription";

export function useSubscription() {
  const [subscription, setSubscription] = useState<MySubscription | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await clientApi<MySubscription>("/api/Subscriptions/me");
      setSubscription(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load subscription");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    refresh();
  }, [refresh]);

  return {
    subscription,
    isPro: subscription?.isPro ?? false,
    loading,
    error,
    refresh,
  };
}
