import { IceServerConfig } from "../types";
import { signalRService } from "./signalr.service";

export type MediaStreamCallback = (stream: MediaStream) => void;

class WebRTCService {
  private peerConnection: RTCPeerConnection | null = null;
  private localStream: MediaStream | null = null;
  private remoteStream: MediaStream | null = null;
  private callUuid: string | null = null;
  private iceServers: RTCIceServer[] = [];

  private onRemoteStreamCallback?: MediaStreamCallback;
  private onLocalStreamCallback?: MediaStreamCallback;

  setCallUuid(callUuid: string): void {
    console.log("[WebRTC] Setting callUuid:", callUuid);
    this.callUuid = callUuid;
  }

  setIceServers(iceServers: IceServerConfig[]): void {
    console.log("[WebRTC] Setting ICE servers. Count:", iceServers.length);
    this.iceServers = iceServers.map((server) => ({
      urls: server.urls,
      username: server.username,
      credential: server.credential,
    }));
    console.log("[WebRTC] ICE servers configured:", this.iceServers);
  }

  setRemoteStreamCallback(callback: MediaStreamCallback): void {
    this.onRemoteStreamCallback = callback;
  }

  setLocalStreamCallback(callback: MediaStreamCallback): void {
    this.onLocalStreamCallback = callback;
  }

  async initializeLocalStream(isVideoCall: boolean): Promise<MediaStream> {
    try {
      const constraints: MediaStreamConstraints = {
        audio: true,
        video: isVideoCall
          ? {
              width: { ideal: 1280 },
              height: { ideal: 720 },
              frameRate: { ideal: 30 },
            }
          : false,
      };

      this.localStream = await navigator.mediaDevices.getUserMedia(constraints);
      this.onLocalStreamCallback?.(this.localStream);

      return this.localStream;
    } catch (err) {
      console.error("Error accessing media devices:", err);
      throw err;
    }
  }

  createPeerConnection(): void {
    if (this.peerConnection) {
      console.warn("Peer connection already exists");
      return;
    }

    console.log("[WebRTC] Creating peer connection, callUuid is:", this.callUuid);
    console.log("[WebRTC] Using ICE servers:", this.iceServers);

    const config: RTCConfiguration = {
      iceServers: this.iceServers,
      iceCandidatePoolSize: 10,
      // Force generation of all candidate types including TURN relay
      iceTransportPolicy: 'all', // 'all' generates host, srflx, and relay candidates
      bundlePolicy: 'max-bundle',
      rtcpMuxPolicy: 'require',
    };

    console.log("[WebRTC] Peer connection config:", config);
    this.peerConnection = new RTCPeerConnection(config);

    // Add local stream tracks to peer connection
    if (this.localStream) {
      this.localStream.getTracks().forEach((track) => {
        this.peerConnection!.addTrack(track, this.localStream!);
      });
    }

    // Handle remote stream
    this.remoteStream = new MediaStream();
    this.peerConnection.ontrack = (event) => {
      console.log("Received remote track:", event.track.kind);
      event.streams[0].getTracks().forEach((track) => {
        this.remoteStream!.addTrack(track);
      });
      this.onRemoteStreamCallback?.(this.remoteStream!);
    };

    // Handle ICE candidates
    this.peerConnection.onicecandidate = async (event) => {
      if (event.candidate) {
        console.log("[WebRTC] ICE candidate generated:", {
          type: event.candidate.type,
          protocol: event.candidate.protocol,
          address: event.candidate.address,
          port: event.candidate.port,
          candidate: event.candidate.candidate.substring(0, 50) + "..."
        });

        if (this.callUuid) {
          console.log("[WebRTC] Sending ICE candidate to peer");
          try {
            await signalRService.sendIceCandidate(
              this.callUuid,
              event.candidate.candidate,
              event.candidate.sdpMid || "",
              event.candidate.sdpMLineIndex || 0
            );
          } catch (err) {
            console.error("[WebRTC] Error sending ICE candidate:", err);
          }
        }
      } else {
        console.log("[WebRTC] ICE gathering complete");
      }
    };

    // Handle connection state changes
    this.peerConnection.onconnectionstatechange = () => {
      console.log("[WebRTC] Connection state:", this.peerConnection?.connectionState);

      if (this.peerConnection?.connectionState === "failed") {
        console.error("[WebRTC] Peer connection failed!");
        console.error("[WebRTC] This usually means NAT traversal failed. You may need a TURN server.");
      }

      if (this.peerConnection?.connectionState === "disconnected") {
        console.warn("[WebRTC] Peer connection disconnected");
      }

      if (this.peerConnection?.connectionState === "connected") {
        console.log("[WebRTC] Peer connection established successfully!");
      }
    };

    // Handle ICE connection state changes
    this.peerConnection.oniceconnectionstatechange = () => {
      console.log("[WebRTC] ICE connection state:", this.peerConnection?.iceConnectionState);

      if (this.peerConnection?.iceConnectionState === "failed") {
        console.error("[WebRTC] ICE connection failed!");
        console.error("[WebRTC] Possible reasons:");
        console.error("[WebRTC] 1. Both devices are behind strict NAT/firewalls");
        console.error("[WebRTC] 2. STUN servers can't determine public IP");
        console.error("[WebRTC] 3. TURN relay server needed but not configured");
      }

      if (this.peerConnection?.iceConnectionState === "disconnected") {
        console.warn("[WebRTC] ICE connection disconnected");
      }

      if (this.peerConnection?.iceConnectionState === "connected" ||
          this.peerConnection?.iceConnectionState === "completed") {
        console.log("[WebRTC] ICE connection successful!");
      }
    };

    // Log ICE gathering state
    this.peerConnection.onicegatheringstatechange = () => {
      console.log("[WebRTC] ICE gathering state:", this.peerConnection?.iceGatheringState);
    };
  }

  async createOffer(): Promise<string> {
    if (!this.peerConnection) {
      throw new Error("Peer connection not initialized");
    }

    try {
      console.log("[WebRTC] Creating offer...");
      const offer = await this.peerConnection.createOffer({
        offerToReceiveAudio: true,
        offerToReceiveVideo: true,
      });

      console.log("[WebRTC] Setting local description (offer)");
      await this.peerConnection.setLocalDescription(offer);

      if (!this.callUuid) {
        throw new Error("Call UUID not set");
      }

      console.log("[WebRTC] Sending offer to SignalR");
      await signalRService.sendOffer(this.callUuid, offer.sdp || "");
      console.log("[WebRTC] Offer sent successfully");

      return offer.sdp || "";
    } catch (err) {
      console.error("Error creating offer:", err);
      throw err;
    }
  }

  async handleOffer(sdp: string): Promise<void> {
    if (!this.peerConnection) {
      throw new Error("Peer connection not initialized");
    }

    try {
      console.log("[WebRTC] Handling incoming offer");
      const offer = new RTCSessionDescription({
        type: "offer",
        sdp: sdp,
      });

      console.log("[WebRTC] Setting remote description (offer)");
      await this.peerConnection.setRemoteDescription(offer);

      console.log("[WebRTC] Creating answer");
      const answer = await this.peerConnection.createAnswer();

      console.log("[WebRTC] Setting local description (answer)");
      await this.peerConnection.setLocalDescription(answer);

      if (!this.callUuid) {
        throw new Error("Call UUID not set");
      }

      console.log("[WebRTC] Sending answer to SignalR");
      await signalRService.sendAnswer(this.callUuid, answer.sdp || "");
      console.log("[WebRTC] Answer sent successfully");
    } catch (err) {
      console.error("Error handling offer:", err);
      throw err;
    }
  }

  async handleAnswer(sdp: string): Promise<void> {
    if (!this.peerConnection) {
      throw new Error("Peer connection not initialized");
    }

    try {
      const answer = new RTCSessionDescription({
        type: "answer",
        sdp: sdp,
      });

      await this.peerConnection.setRemoteDescription(answer);
    } catch (err) {
      console.error("Error handling answer:", err);
      throw err;
    }
  }

  async handleIceCandidate(
    candidate: string,
    sdpMid: string,
    sdpMLineIndex: number
  ): Promise<void> {
    if (!this.peerConnection) {
      throw new Error("Peer connection not initialized");
    }

    try {
      const iceCandidate = new RTCIceCandidate({
        candidate: candidate,
        sdpMid: sdpMid,
        sdpMLineIndex: sdpMLineIndex,
      });

      await this.peerConnection.addIceCandidate(iceCandidate);
    } catch (err) {
      console.error("Error adding ICE candidate:", err);
      throw err;
    }
  }

  toggleAudio(enabled: boolean): void {
    if (this.localStream) {
      this.localStream.getAudioTracks().forEach((track) => {
        track.enabled = enabled;
      });
    }
  }

  toggleVideo(enabled: boolean): void {
    if (this.localStream) {
      this.localStream.getVideoTracks().forEach((track) => {
        track.enabled = enabled;
      });
    }
  }

  cleanup(): void {
    // Stop all tracks
    if (this.localStream) {
      this.localStream.getTracks().forEach((track) => track.stop());
      this.localStream = null;
    }

    if (this.remoteStream) {
      this.remoteStream.getTracks().forEach((track) => track.stop());
      this.remoteStream = null;
    }

    // Close peer connection
    if (this.peerConnection) {
      this.peerConnection.close();
      this.peerConnection = null;
    }

    this.callUuid = null;
  }

  getLocalStream(): MediaStream | null {
    return this.localStream;
  }

  getRemoteStream(): MediaStream | null {
    return this.remoteStream;
  }
}

export const webRTCService = new WebRTCService();
