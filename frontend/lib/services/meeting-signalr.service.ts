import * as signalR from "@microsoft/signalr";
import { apiService } from "./api.service";

const MEETING_HUB_URL = process.env.NEXT_PUBLIC_MEETING_HUB_URL || "http://localhost:5107/meetingHub";

type EventHandler = (...args: any[]) => void;

class MeetingSignalRService {
  private connection: signalR.HubConnection | null = null;
  private eventHandlers: Map<string, EventHandler[]> = new Map();
  private reconnectAttempts = 0;
  private maxReconnectAttempts = 5;
  private isReconnecting = false;
  private connectingPromise: Promise<void> | null = null;

  async connect(): Promise<void> {
    // If already connected, return immediately
    if (this.connection?.state === signalR.HubConnectionState.Connected) {
      console.log("[MeetingSignalR] Already connected");
      return;
    }

    // If connection is in progress, wait for it
    if (this.connectingPromise) {
      console.log("[MeetingSignalR] Connection already in progress, waiting...");
      return this.connectingPromise;
    }

    // Start new connection
    this.connectingPromise = this._doConnect();

    try {
      await this.connectingPromise;
    } finally {
      this.connectingPromise = null;
    }
  }

  private async _doConnect(): Promise<void> {
    const token = apiService.getAccessToken();
    if (!token) {
      throw new Error("No access token available");
    }

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(MEETING_HUB_URL, {
        accessTokenFactory: () => token,
      })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          if (retryContext.previousRetryCount < 5) {
            return Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 30000);
          }
          return null;
        },
      })
      .configureLogging(signalR.LogLevel.Information)
      .build();

    this.setupConnectionHandlers();
    this.registerStoredHandlers();

    try {
      await this.connection.start();
      console.log("[MeetingSignalR] Connected to meeting hub");
      this.reconnectAttempts = 0;
      this.isReconnecting = false;
    } catch (error) {
      console.error("[MeetingSignalR] Connection error:", error);
      this.connection = null;
      throw error;
    }
  }

  private setupConnectionHandlers(): void {
    if (!this.connection) return;

    this.connection.onreconnecting((error) => {
      console.warn("[MeetingSignalR] Connection lost. Reconnecting...", error);
      this.isReconnecting = true;
    });

    this.connection.onreconnected((connectionId) => {
      console.log("[MeetingSignalR] Reconnected. Connection ID:", connectionId);
      this.isReconnecting = false;
      this.reconnectAttempts = 0;
    });

    this.connection.onclose(async (error) => {
      console.log("[MeetingSignalR] Connection closed", error);
      this.isReconnecting = false;

      if (this.reconnectAttempts < this.maxReconnectAttempts) {
        this.reconnectAttempts++;
        const delay = Math.min(1000 * Math.pow(2, this.reconnectAttempts), 30000);
        console.log(`[MeetingSignalR] Attempting reconnect ${this.reconnectAttempts}/${this.maxReconnectAttempts} in ${delay}ms`);

        setTimeout(async () => {
          try {
            await this.connect();
          } catch (err) {
            console.error("[MeetingSignalR] Reconnection failed:", err);
          }
        }, delay);
      }
    });
  }

  private registerStoredHandlers(): void {
    if (!this.connection) return;

    // Register global error handler to catch backend error messages
    this.connection.on("Error", (message: string) => {
      console.error("[MeetingSignalR] Server error:", message);
    });

    for (const [event, handlers] of this.eventHandlers.entries()) {
      for (const handler of handlers) {
        this.connection.on(event, handler);
      }
    }
  }

  on(eventName: string, handler: EventHandler): void {
    const handlers = this.eventHandlers.get(eventName) || [];
    handlers.push(handler);
    this.eventHandlers.set(eventName, handlers);

    if (this.connection) {
      this.connection.on(eventName, handler);
    }
  }

  off(eventName: string, handler?: EventHandler): void {
    if (handler) {
      const handlers = this.eventHandlers.get(eventName) || [];
      const index = handlers.indexOf(handler);
      if (index > -1) {
        handlers.splice(index, 1);
        this.eventHandlers.set(eventName, handlers);
      }

      if (this.connection) {
        this.connection.off(eventName, handler);
      }
    } else {
      this.eventHandlers.delete(eventName);
      if (this.connection) {
        this.connection.off(eventName);
      }
    }
  }

  async joinMeeting(meetingCode: string): Promise<any> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      throw new Error("Not connected to meeting hub");
    }

    return await this.connection.invoke("JoinMeeting", meetingCode);
  }

  async leaveMeeting(meetingCode: string): Promise<void> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      return;
    }

    await this.connection.invoke("LeaveMeeting", meetingCode);
  }

  async sendOffer(meetingCode: string, targetUserId: number, sdp: string): Promise<void> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      throw new Error("Not connected to meeting hub");
    }

    await this.connection.invoke("SendOffer", meetingCode, targetUserId, sdp);
  }

  async sendAnswer(meetingCode: string, targetConnectionId: string, sdp: string): Promise<void> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      throw new Error("Not connected to meeting hub");
    }

    await this.connection.invoke("SendAnswer", meetingCode, targetConnectionId, sdp);
  }

  async sendIceCandidate(meetingCode: string, targetConnectionId: string, candidate: string, sdpMid: string, sdpMLineIndex: number): Promise<void> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      throw new Error("Not connected to meeting hub");
    }

    await this.connection.invoke("SendIceCandidate", meetingCode, targetConnectionId, candidate, sdpMid, sdpMLineIndex);
  }

  async approveJoinRequest(requestId: number): Promise<void> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      throw new Error("Not connected to meeting hub");
    }

    await this.connection.invoke("ApproveJoinRequest", requestId);
  }

  async rejectJoinRequest(requestId: number): Promise<void> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      throw new Error("Not connected to meeting hub");
    }

    await this.connection.invoke("RejectJoinRequest", requestId);
  }

  async invoke<T = any>(methodName: string, ...args: any[]): Promise<T> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      throw new Error("Not connected to meeting hub");
    }

    return await this.connection.invoke<T>(methodName, ...args);
  }

  async disconnect(): Promise<void> {
    this.connectingPromise = null;
    if (this.connection) {
      try {
        await this.connection.stop();
      } catch (error) {
        console.error("[MeetingSignalR] Error during disconnect:", error);
      }
      this.connection = null;
      this.eventHandlers.clear();
      console.log("[MeetingSignalR] Disconnected");
    }
  }

  getConnectionState(): signalR.HubConnectionState | null {
    return this.connection?.state || null;
  }
}

export const meetingSignalRService = new MeetingSignalRService();
