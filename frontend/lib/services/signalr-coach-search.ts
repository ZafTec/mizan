import * as signalR from "@microsoft/signalr";
import { getApiToken } from "@/lib/api.client";
import { logger } from "@/lib/logger";

const coachSearchLogger = logger.createModuleLogger("signalr-coach-search-service");

export interface CoachSearchResult {
  id: string;
  name: string;
  email: string;
  specialties: string[];
  rating: number;
  clientCount: number;
  image?: string;
}

export class CoachSearchService {
  private connection: signalR.HubConnection | null = null;
  private reconnectAttempts = 0;
  private maxReconnectAttempts = 5;

  async connect(): Promise<void> {
    if (this.connection?.state === signalR.HubConnectionState.Connected) {
      return;
    }

    const apiUrl = process.env.NEXT_PUBLIC_API_URL!;
    const token = await getApiToken();

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(`${apiUrl}/hubs/coach-search`, {
        accessTokenFactory: () => token || "",
        transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.ServerSentEvents,
      })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          if (retryContext.previousRetryCount >= this.maxReconnectAttempts) {
            return null;
          }
          // Ensure the delay is always positive and within reasonable bounds
          const delay = Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 30000);
          return Math.max(100, delay); // Minimum 100ms to avoid negative or zero values
        },
      })
      .withServerTimeout(this.validateTimeout(60000))
      .withKeepAliveInterval(this.validateTimeout(30000))
      .configureLogging(signalR.LogLevel.Information)
      .build();

    this.connection.onreconnecting(() => {
      coachSearchLogger.warn("Reconnecting to coach search hub", { attempt: this.reconnectAttempts + 1 });
      this.reconnectAttempts++;
    });

    this.connection.onreconnected(() => {
      coachSearchLogger.info("Reconnected to coach search hub");
      this.reconnectAttempts = 0;
    });

    this.connection.onclose((error) => {
      coachSearchLogger.error("Disconnected from coach search hub", { error });
      this.reconnectAttempts = 0;
    });

    try {
      await this.connection.start();
      coachSearchLogger.info("Connected to coach search hub");
    } catch (error) {
      coachSearchLogger.error("Failed to connect to coach search hub", { error });
      throw error;
    }
  }

  /**
   * Validates that timeout values are positive and within reasonable bounds
   */
  private validateTimeout(timeout: number): number {
    // Ensure timeout is positive and within reasonable bounds
    const validatedTimeout = Math.max(1000, Math.min(timeout, 300000)); // Between 1 second and 5 minutes
    return validatedTimeout;
  }

  async disconnect(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
    }
  }

  async searchCoaches(query: string, page: number = 1): Promise<void> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      throw new Error("Not connected to coach search hub");
    }

    await this.connection.invoke("SearchCoaches", query, page);
  }

  async requestCoach(coachId: string): Promise<void> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      throw new Error("Not connected to coach search hub");
    }

    await this.connection.invoke("RequestCoach", coachId);
  }

  onCoachResults(callback: (results: CoachSearchResult[]) => void): void {
    if (!this.connection) {
      throw new Error("Connection not initialized");
    }

    this.connection.on("ReceiveCoachResults", callback);
  }

  onCoachRequestReceived(callback: (coachId: string) => void): void {
    if (!this.connection) {
      throw new Error("Connection not initialized");
    }

    this.connection.on("CoachRequestReceived", callback);
  }

  off(methodName: string): void {
    if (this.connection) {
      this.connection.off(methodName);
    }
  }

  get connectionState(): signalR.HubConnectionState {
    return this.connection?.state || signalR.HubConnectionState.Disconnected;
  }
}

export const coachSearchService = new CoachSearchService();
