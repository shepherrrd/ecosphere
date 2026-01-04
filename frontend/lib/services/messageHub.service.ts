import * as signalR from "@microsoft/signalr";
import { apiService } from "./api.service";
import { getOrCreateClientId } from "../utils/clientId";

export type MessageHubEventHandlers = {
  onReceiveDirectMessage?: (message: any) => void;
  onReceiveMeetingMessage?: (message: any) => void;
  onMessageSent?: (message: any) => void;
  onMessageError?: (error: string) => void;
};

class MessageHubService {
  private connection: signalR.HubConnection | null = null;
  private eventHandlers: MessageHubEventHandlers = {};
  private genericEventHandlers: Map<string, ((...args: any[]) => void)[]> = new Map();
  private reconnectAttempts = 0;
  private maxReconnectAttempts = 5;

  async connect(handlers: MessageHubEventHandlers): Promise<void> {
    this.eventHandlers = handlers;

    const hubUrl = process.env.NEXT_PUBLIC_MESSAGE_HUB_URL || "http://localhost:5000/hubs/messageHub";
    const clientId = getOrCreateClientId();

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => {
          const token = apiService.getToken();
          if (!token) {
            console.error("No authentication token found");
            return "";
          }
          return token;
        },
        headers: {
          "X-ClientId": clientId,
        },
      })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          if (retryContext.previousRetryCount >= this.maxReconnectAttempts) {
            return null; // Stop reconnecting
          }
          return Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 30000);
        },
      })
      .configureLogging(signalR.LogLevel.Information)
      .build();

    this.registerEventHandlers();

    try {
      await this.connection.start();
      console.log("MessageHub Connected");
      this.reconnectAttempts = 0;
    } catch (err) {
      console.error("MessageHub Connection Error: ", err);
      throw err;
    }
  }

  private registerStoredGenericHandlers(): void {
    if (!this.connection) return;

    for (const [event, handlers] of this.genericEventHandlers.entries()) {
      for (const handler of handlers) {
        this.connection.on(event, handler);
      }
    }
  }

  private registerEventHandlers(): void {
    if (!this.connection) return;

    // Register generic event handlers first
    this.registerStoredGenericHandlers();

    // Receive Direct Message
    this.connection.on("ReceiveDirectMessage", (message: any) => {
      console.log("Received direct message:", message);
      this.eventHandlers.onReceiveDirectMessage?.(message);
    });

    // Receive Meeting Message
    this.connection.on("ReceiveMeetingMessage", (message: any) => {
      console.log("Received meeting message:", message);
      this.eventHandlers.onReceiveMeetingMessage?.(message);
    });

    // Message Sent Confirmation
    this.connection.on("MessageSent", (message: any) => {
      console.log("Message sent:", message);
      this.eventHandlers.onMessageSent?.(message);
    });

    // Message Error
    this.connection.on("MessageError", (error: string) => {
      console.error("Message error:", error);
      this.eventHandlers.onMessageError?.(error);
    });
  }

  async disconnect(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
      console.log("MessageHub Disconnected");
    }
  }

  async sendDirectMessage(receiverId: number, content: string): Promise<void> {
    if (!this.connection) throw new Error("Not connected to MessageHub");

    try {
      await this.connection.invoke("SendDirectMessage", receiverId, content);
    } catch (err) {
      console.error("Error sending direct message:", err);
      throw err;
    }
  }

  async sendMeetingMessage(meetingId: number, content: string): Promise<void> {
    if (!this.connection) throw new Error("Not connected to MessageHub");

    try {
      await this.connection.invoke("SendMeetingMessage", meetingId, content);
    } catch (err) {
      console.error("Error sending meeting message:", err);
      throw err;
    }
  }

  async markMessagesAsRead(contactUserId: number): Promise<void> {
    if (!this.connection) throw new Error("Not connected to MessageHub");

    try {
      await this.connection.invoke("MarkMessagesAsRead", contactUserId);
    } catch (err) {
      console.error("Error marking messages as read:", err);
      throw err;
    }
  }

  // Generic event listener methods
  on(eventName: string, handler: (...args: any[]) => void): void {
    // Store the handler
    const handlers = this.genericEventHandlers.get(eventName) || [];
    handlers.push(handler);
    this.genericEventHandlers.set(eventName, handlers);

    // Register immediately if connected
    if (this.connection) {
      this.connection.on(eventName, handler);
    }
  }

  off(eventName: string, handler: (...args: any[]) => void): void {
    // Remove from stored handlers
    const handlers = this.genericEventHandlers.get(eventName) || [];
    const index = handlers.indexOf(handler);
    if (index > -1) {
      handlers.splice(index, 1);
      this.genericEventHandlers.set(eventName, handlers);
    }

    // Unregister from connection if connected
    if (this.connection) {
      this.connection.off(eventName, handler);
    }
  }

  async invoke<T = any>(methodName: string, ...args: any[]): Promise<T> {
    if (!this.connection) {
      throw new Error("Not connected to MessageHub");
    }
    return await this.connection.invoke<T>(methodName, ...args);
  }

  isConnected(): boolean {
    return this.connection?.state === signalR.HubConnectionState.Connected;
  }
}

export const messageHubService = new MessageHubService();
