import * as signalR from "@microsoft/signalr";
import { getApiToken } from "@/lib/auth-client";

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
          return Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 30000);
        },
      })
      .withServerTimeout(60000)
      .withKeepAliveInterval(30000)
      .configureLogging(signalR.LogLevel.Information)
      .build();

    this.connection.onreconnecting(() => {
      console.log("[SignalR] Reconnecting to coach search hub...");
      this.reconnectAttempts++;
    });

    this.connection.onreconnected(() => {
      console.log("[SignalR] Reconnected to coach search hub");
      this.reconnectAttempts = 0;
    });

    this.connection.onclose((error) => {
      console.error("[SignalR] Connection closed:", error);
      this.reconnectAttempts = 0;
    });

    try {
      await this.connection.start();
      console.log("[SignalR] Connected to coach search hub");
    } catch (error) {
      console.error("[SignalR] Failed to connect:", error);
      throw error;
    }
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
