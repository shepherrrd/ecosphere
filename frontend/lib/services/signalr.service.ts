import * as signalR from "@microsoft/signalr";
import {
  IncomingCallEvent,
  CallAnsweredEvent,
  CallEndedEvent,
  ReceiveOfferEvent,
  ReceiveAnswerEvent,
  ReceiveIceCandidateEvent,
} from "../types";
import { apiService } from "./api.service";
import { getOrCreateClientId } from "../utils/clientId";

export type CallHubEventHandlers = {
  onIncomingCall?: (event: IncomingCallEvent) => void;
  onCallAnswered?: (event: CallAnsweredEvent) => void;
  onCallAnsweredElsewhere?: (callUuid: string) => void;
  onCallRejected?: (callUuid: string, userId: number) => void;
  onCallEnded?: (event: CallEndedEvent) => void;
  onReceiveOffer?: (event: ReceiveOfferEvent) => void;
  onReceiveAnswer?: (event: ReceiveAnswerEvent) => void;
  onReceiveIceCandidate?: (event: ReceiveIceCandidateEvent) => void;
  onUserOnline?: (userId: number) => void;
  onUserOffline?: (userId: number) => void;
};

class SignalRService {
  private connection: signalR.HubConnection | null = null;
  private eventHandlers: CallHubEventHandlers = {};
  private genericEventHandlers: Map<string, ((...args: any[]) => void)[]> = new Map();
  private reconnectAttempts = 0;
  private maxReconnectAttempts = 5;

  async connect(handlers: CallHubEventHandlers): Promise<void> {
    this.eventHandlers = handlers;

    const hubUrl = process.env.NEXT_PUBLIC_HUB_URL || "http://localhost:5000/callHub";
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

    // Listen for connection initialization
    this.connection.on("ConnectionInitialized", (connectionId: string) => {
      console.log(`Connection initialized with ID: ${connectionId}`);
    });

    try {
      await this.connection.start();
      console.log("SignalR Connected");
      this.reconnectAttempts = 0;

      const deviceToken = navigator.userAgent;
      const deviceName = navigator.userAgent.substring(0, 50);

    } catch (err) {
      console.error("SignalR Connection Error: ", err);
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

    // Incoming Call
    this.connection.on("IncomingCall", (event: IncomingCallEvent) => {
      console.log("Incoming call:", event);
      this.eventHandlers.onIncomingCall?.(event);
    });

    // Call Initiated (sent to caller when call starts ringing)
    this.connection.on("CallInitiated", (event: any) => {
      console.log("Call initiated:", event);
      // This is sent to the caller, we can use it to show "ringing" state
    });

    this.connection.on("CallAccepted", (event: CallAnsweredEvent) => {
      console.log("Call accepted:", event);
      this.eventHandlers.onCallAnswered?.(event);
    });

    this.connection.on("CallConnected", (event: any) => {
      console.log("Call connected:", event);
    });

    // Call Answered Elsewhere
    this.connection.on("CallAnsweredElsewhere", (data: { callUuid: string }) => {
      console.log("Call answered elsewhere:", data.callUuid);
      this.eventHandlers.onCallAnsweredElsewhere?.(data.callUuid);
    });

    // Call Rejected
    this.connection.on("CallRejected", (data: { callUuid: string; userId: number }) => {
      console.log("Call rejected:", data);
      this.eventHandlers.onCallRejected?.(data.callUuid, data.userId);
    });

    // Call Ended
    this.connection.on("CallEnded", (event: CallEndedEvent) => {
      console.log("Call ended:", event);
      this.eventHandlers.onCallEnded?.(event);
    });

    // WebRTC Signaling - Offer
    this.connection.on("ReceiveOffer", (event: ReceiveOfferEvent) => {
      console.log("Received offer:", event);
      this.eventHandlers.onReceiveOffer?.(event);
    });

    // WebRTC Signaling - Answer
    this.connection.on("ReceiveAnswer", (event: ReceiveAnswerEvent) => {
      console.log("Received answer:", event);
      this.eventHandlers.onReceiveAnswer?.(event);
    });

    // WebRTC Signaling - ICE Candidate
    this.connection.on("ReceiveIceCandidate", (event: ReceiveIceCandidateEvent) => {
      console.log("Received ICE candidate:", event);
      this.eventHandlers.onReceiveIceCandidate?.(event);
    });

    // User Status
    this.connection.on("UserOnline", (data: { userId: number }) => {
      console.log("User online:", data.userId);
      this.eventHandlers.onUserOnline?.(data.userId);
    });

    this.connection.on("UserOffline", (data: { userId: number }) => {
      console.log("User offline:", data.userId);
      this.eventHandlers.onUserOffline?.(data.userId);
    });
  }

  async initiateCall(targetUserId: number, isVideoCall: boolean): Promise<string> {
    if (!this.connection) throw new Error("Not connected to SignalR hub");

    try {
      const callType = isVideoCall ? "Video" : "Audio";
      const callUuid = await this.connection.invoke<string>(
        "InitiateCall",
        targetUserId,
        callType,
        isVideoCall
      );
      return callUuid;
    } catch (err) {
      console.error("Error initiating call:", err);
      throw err;
    }
  }

  async acceptCall(callUuid: string): Promise<void> {
    if (!this.connection) throw new Error("Not connected to SignalR hub");

    try {
      await this.connection.invoke("AcceptCall", callUuid);
    } catch (err) {
      console.error("Error accepting call:", err);
      throw err;
    }
  }

  async rejectCall(callUuid: string, reason?: string): Promise<void> {
    if (!this.connection) throw new Error("Not connected to SignalR hub");

    try {
      await this.connection.invoke("RejectCall", callUuid, reason || "Call declined");
    } catch (err) {
      console.error("Error rejecting call:", err);
      throw err;
    }
  }

  async endCall(callUuid: string): Promise<void> {
    if (!this.connection) throw new Error("Not connected to SignalR hub");

    try {
      await this.connection.invoke("EndCall", callUuid);
    } catch (err) {
      console.error("Error ending call:", err);
      throw err;
    }
  }

  async sendOffer(callUuid: string, sdp: string): Promise<void> {
    if (!this.connection) throw new Error("Not connected to SignalR hub");

    try {
      await this.connection.invoke("SendOffer", callUuid, sdp);
    } catch (err) {
      console.error("Error sending offer:", err);
      throw err;
    }
  }

  async sendAnswer(callUuid: string, sdp: string): Promise<void> {
    if (!this.connection) throw new Error("Not connected to SignalR hub");

    try {
      await this.connection.invoke("SendAnswer", callUuid, sdp);
    } catch (err) {
      console.error("Error sending answer:", err);
      throw err;
    }
  }

  async sendIceCandidate(
    callUuid: string,
    candidate: string,
    sdpMid: string,
    sdpMLineIndex: number
  ): Promise<void> {
    if (!this.connection) throw new Error("Not connected to SignalR hub");

    try {
      await this.connection.invoke("SendIceCandidate", callUuid, candidate, sdpMid, sdpMLineIndex);
    } catch (err) {
      console.error("Error sending ICE candidate:", err);
      throw err;
    }
  }

  async disconnect(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
      console.log("SignalR Disconnected");
    }
  }

  isConnected(): boolean {
    return this.connection?.state === signalR.HubConnectionState.Connected;
  }

  // Generic event listener methods (for non-call related events like MeetingInvite, ReceiveDirectMessage)
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

  // Generic invoke method for sending messages to the hub
  async invoke<T = any>(methodName: string, ...args: any[]): Promise<T> {
    if (!this.connection) {
      throw new Error("Not connected to SignalR hub");
    }
    return await this.connection.invoke<T>(methodName, ...args);
  }
}

export const signalRService = new SignalRService();
