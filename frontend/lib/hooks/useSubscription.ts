"use client";

import { useCallback, useEffect, useState } from "react";
import { clientApi } from "@/lib/api.client";
import type { MySubscription } from "@/types/subscription";

export function useSubscription() {
  const [subscription, setSubscription] = useState<MySubscription | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Resolves with what it loaded, so a caller waiting on a change (checkout
  // provisioning) can act on the result instead of an effect watching state.
  const refresh = useCallback(async (): Promise<MySubscription | null> => {
    setLoading(true);
    setError(null);
    try {
      const data = await clientApi<MySubscription>("/api/Subscriptions/me");
      setSubscription(data);
      return data;
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load subscription");
      return null;
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
