"use client";

import React, { createContext, useContext, useState, useEffect, ReactNode, useCallback, useRef } from "react";
import { IncomingCallEvent, IceServerConfig } from "../types";
import { signalRService } from "../services/signalr.service";
import { webRTCService } from "../services/webrtc.service";
import { apiService } from "../services/api.service";
import { useAuth } from "./AuthContext";

export enum CallState {
  Idle = "idle",
  Initiating = "initiating",
  Ringing = "ringing",
  IncomingCall = "incoming",
  Connecting = "connecting",
  Active = "active",
}

interface CallContextType {
  callState: CallState;
  currentCallUuid: string | null;
  isVideoCall: boolean;
  callerId: number | null;
  callerName: string | null;
  localStream: MediaStream | null;
  remoteStream: MediaStream | null;
  isMuted: boolean;
  isVideoEnabled: boolean;
  initiateCall: (targetUserId: number, isVideo: boolean) => Promise<void>;
  acceptCall: () => Promise<void>;
  rejectCall: () => Promise<void>;
  endCall: () => Promise<void>;
  toggleMute: () => void;
  toggleVideo: () => void;
}

const CallContext = createContext<CallContextType | undefined>(undefined);

export function CallProvider({ children }: { children: ReactNode }) {
  const { user, isAuthenticated } = useAuth();
  const [callState, setCallState] = useState<CallState>(CallState.Idle);
  const [currentCallUuid, setCurrentCallUuid] = useState<string | null>(null);
  const [isVideoCall, setIsVideoCall] = useState(false);
  const [callerId, setCallerId] = useState<number | null>(null);
  const [callerName, setCallerName] = useState<string | null>(null);
  const [localStream, setLocalStream] = useState<MediaStream | null>(null);
  const [remoteStream, setRemoteStream] = useState<MediaStream | null>(null);
  const [isMuted, setIsMuted] = useState(false);
  const [isVideoEnabled, setIsVideoEnabled] = useState(true);
  const [iceServers, setIceServers] = useState<IceServerConfig[]>([]);
  const [isInitiator, setIsInitiator] = useState(false);
  const isInitiatorRef = useRef(false);
  const currentCallUuidRef = useRef<string | null>(null);

  // Fetch ICE servers on mount
  useEffect(() => {
    if (isAuthenticated) {
      fetchIceServers();
    }
  }, [isAuthenticated]);

  const fetchIceServers = async () => {
    try {
      console.log("[CallContext] Fetching ICE servers...");
      const response = await apiService.getIceServers();
      if (response.status && response.data) {
        console.log("[CallContext] ICE servers fetched:", response.data);
        setIceServers(response.data);
        webRTCService.setIceServers(response.data);
      } else {
        console.error("[CallContext] Failed to fetch ICE servers:", response.message);
      }
    } catch (error) {
      console.error("[CallContext] Error fetching ICE servers:", error);
    }
  };

  // Setup SignalR event handlers
  useEffect(() => {
    if (!isAuthenticated || !user) return;

    const connectSignalR = async () => {
      try {
        await signalRService.connect({
          onIncomingCall: handleIncomingCall,
          onCallAnswered: handleCallAnswered,
          onCallAnsweredElsewhere: handleCallAnsweredElsewhere,
          onCallRejected: handleCallRejected,
          onCallEnded: handleCallEnded,
          onReceiveOffer: handleReceiveOffer,
          onReceiveAnswer: handleReceiveAnswer,
          onReceiveIceCandidate: handleReceiveIceCandidate,
        });
      } catch (error) {
        console.error("Failed to connect to SignalR:", error);
      }
    };

    connectSignalR();

    return () => {
      signalRService.disconnect();
    };
  }, [isAuthenticated, user]);

  // Setup WebRTC stream callbacks
  useEffect(() => {
    webRTCService.setLocalStreamCallback((stream) => {
      setLocalStream(stream);
    });

    webRTCService.setRemoteStreamCallback((stream) => {
      setRemoteStream(stream);
    });
  }, []);

  const handleIncomingCall = useCallback((event: IncomingCallEvent) => {
    const displayName = event.caller.displayName || event.caller.userName;
    console.log("Incoming call from:", displayName);
    setCallState(CallState.IncomingCall);
    setCurrentCallUuid(event.callUuid);
    currentCallUuidRef.current = event.callUuid;
    setIsVideoCall(event.isVideoCall);
    setCallerId(event.caller.id);
    setCallerName(displayName);
    setIsInitiator(false); // Receiver is not the initiator
    isInitiatorRef.current = false; // Also set ref
  }, []);

  const handleCallAnswered = useCallback(async () => {
    console.log("Call answered, isInitiator:", isInitiatorRef.current, "callUuid:", currentCallUuidRef.current);
    setCallState(CallState.Connecting);

    // Only the initiator creates the offer
    if (isInitiatorRef.current && currentCallUuidRef.current) {
      // Ensure callUuid is set in webRTCService (in case it was cleared)
      webRTCService.setCallUuid(currentCallUuidRef.current);
      webRTCService.createPeerConnection();
      await webRTCService.createOffer();
    }
    // The receiver waits for the offer and will handle it in handleReceiveOffer
  }, []);

  const handleCallAnsweredElsewhere = useCallback((callUuid: string) => {
    console.log("Call answered elsewhere, callUuid:", callUuid, "current:", currentCallUuidRef.current);
    if (currentCallUuidRef.current === callUuid) {
      console.log("Cleaning up - call was answered on another device");
      cleanup();
    }
  }, []);

  const handleCallRejected = useCallback((callUuid: string) => {
    console.log("Call rejected, callUuid:", callUuid);
    if (currentCallUuidRef.current === callUuid) {
      cleanup();
    }
  }, []);

  const handleCallEnded = useCallback(() => {
    console.log("Call ended");
    cleanup();
  }, []);

  const handleReceiveOffer = useCallback(async (event: any) => {
    console.log("Received offer");
    // Receiver creates peer connection when receiving offer
    if (!isInitiatorRef.current) {
      webRTCService.createPeerConnection();
    }
    await webRTCService.handleOffer(event.sdp);
    setCallState(CallState.Active);
  }, []);

  const handleReceiveAnswer = useCallback(async (event: any) => {
    console.log("Received answer");
    await webRTCService.handleAnswer(event.sdp);
    setCallState(CallState.Active);
  }, []);

  const handleReceiveIceCandidate = useCallback(async (event: any) => {
    console.log("Received ICE candidate");
    await webRTCService.handleIceCandidate(event.candidate, event.sdpMid, event.sdpMLineIndex);
  }, []);

  const initiateCall = async (targetUserId: number, isVideo: boolean) => {
    try {
      setCallState(CallState.Initiating);
      setIsVideoCall(isVideo);
      setIsInitiator(true); // Mark as initiator
      isInitiatorRef.current = true; // Also set ref

      // Get local media stream
      await webRTCService.initializeLocalStream(isVideo);
      setIsVideoEnabled(isVideo);

      // Initiate call via SignalR
      const callUuid = await signalRService.initiateCall(targetUserId, isVideo);
      setCurrentCallUuid(callUuid);
      currentCallUuidRef.current = callUuid;
      webRTCService.setCallUuid(callUuid);

      setCallState(CallState.Ringing);
    } catch (error) {
      console.error("Error initiating call:", error);
      cleanup();
    }
  };

  const acceptCall = async () => {
    try {
      if (!currentCallUuid) return;

      setCallState(CallState.Connecting);

      // Get local media stream
      await webRTCService.initializeLocalStream(isVideoCall);
      setIsVideoEnabled(isVideoCall);

      // Create peer connection
      webRTCService.setCallUuid(currentCallUuid);
      webRTCService.createPeerConnection();

      // Accept call via SignalR
      await signalRService.acceptCall(currentCallUuid);
    } catch (error) {
      console.error("Error accepting call:", error);
      cleanup();
    }
  };

  const rejectCall = async () => {
    try {
      if (!currentCallUuid) return;

      await signalRService.rejectCall(currentCallUuid);
      cleanup();
    } catch (error) {
      console.error("Error rejecting call:", error);
      cleanup();
    }
  };

  const endCall = async () => {
    try {
      if (!currentCallUuid) return;

      await signalRService.endCall(currentCallUuid);
      cleanup();
    } catch (error) {
      console.error("Error ending call:", error);
      cleanup();
    }
  };

  const toggleMute = () => {
    const newMuteState = !isMuted;
    setIsMuted(newMuteState);
    webRTCService.toggleAudio(!newMuteState);
  };

  const toggleVideo = () => {
    const newVideoState = !isVideoEnabled;
    setIsVideoEnabled(newVideoState);
    webRTCService.toggleVideo(newVideoState);
  };

  const cleanup = () => {
    webRTCService.cleanup();
    setCallState(CallState.Idle);
    setCurrentCallUuid(null);
    currentCallUuidRef.current = null;
    setCallerId(null);
    setCallerName(null);
    setLocalStream(null);
    setRemoteStream(null);
    setIsMuted(false);
    setIsVideoEnabled(true);
    setIsInitiator(false);
    isInitiatorRef.current = false;
  };

  return (
    <CallContext.Provider
      value={{
        callState,
        currentCallUuid,
        isVideoCall,
        callerId,
        callerName,
        localStream,
        remoteStream,
        isMuted,
        isVideoEnabled,
        initiateCall,
        acceptCall,
        rejectCall,
        endCall,
        toggleMute,
        toggleVideo,
      }}
    >
      {children}
    </CallContext.Provider>
  );
}

export function useCall() {
  const context = useContext(CallContext);
  if (context === undefined) {
    throw new Error("useCall must be used within a CallProvider");
  }
  return context;
}
