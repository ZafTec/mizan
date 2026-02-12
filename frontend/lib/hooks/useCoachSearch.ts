"use client";

import { useState, useEffect, useCallback } from "react";
import { coachSearchService, CoachSearchResult } from "@/lib/services/signalr-coach-search";
import { toast } from "sonner";

export function useCoachSearch() {
  const [results, setResults] = useState<CoachSearchResult[]>([]);
  const [isConnected, setIsConnected] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const connect = async () => {
      try {
        await coachSearchService.connect();
        setIsConnected(true);
        setError(null);

        coachSearchService.onCoachResults((newResults) => {
          setResults(newResults);
          setIsLoading(false);
        });

        coachSearchService.onCoachRequestReceived((coachId) => {
          // Feature not yet implemented
          console.log("Coach request received:", coachId);
          toast.info("Coach Request Feature", {
            description: "This feature is coming soon! You'll be able to receive and respond to coach requests."
          });
        });
      } catch (err) {
        setError(err instanceof Error ? err.message : "Failed to connect");
        setIsConnected(false);
        setIsLoading(false);
      }
    };

    connect();

    return () => {
      coachSearchService.off("ReceiveCoachResults");
      coachSearchService.off("CoachRequestReceived");
      coachSearchService.disconnect();
    };
  }, []);

  const searchCoaches = useCallback(
    async (query: string, page: number = 1) => {
      if (!isConnected) {
        setError("Not connected to search service");
        return;
      }

      setIsLoading(true);
      setError(null);

      try {
        await coachSearchService.searchCoaches(query, page);
      } catch (err) {
        setError(err instanceof Error ? err.message : "Search failed");
        setIsLoading(false);
      }
    },
    [isConnected]
  );

  const requestCoach = useCallback(
    async (coachId: string) => {
      if (!isConnected) {
        setError("Not connected to search service");
        return;
      }

      try {
        await coachSearchService.requestCoach(coachId);
        setError(null);
      } catch (err) {
        setError(err instanceof Error ? err.message : "Request failed");
      }
    },
    [isConnected]
  );

  return {
    results,
    isConnected,
    isLoading,
    error,
    searchCoaches,
    requestCoach,
  };
}
