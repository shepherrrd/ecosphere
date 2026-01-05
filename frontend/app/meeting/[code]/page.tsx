"use client";

import { use, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/contexts/AuthContext";
import { apiService } from "@/lib/services/api.service";
import { meetingSignalRService } from "@/lib/services/meeting-signalr.service";

interface MeetingPageProps {
  params: Promise<{ code: string }>;
}

interface Participant {
  userId: number;
  userName: string;
  displayName: string;
  profileImageUrl?: string;
  connectionId: string;
  stream?: MediaStream;
}

export default function MeetingPage({ params }: MeetingPageProps) {
  const resolvedParams = use(params);
  const meetingCode = resolvedParams.code;

  const router = useRouter();
  const { user } = useAuth();

  const [meeting, setMeeting] = useState<any>(null);
  const [participants, setParticipants] = useState<Participant[]>([]);
  const [localStream, setLocalStream] = useState<MediaStream | null>(null);
  const [isAudioMuted, setIsAudioMuted] = useState(false);
  const [isVideoOff, setIsVideoOff] = useState(false);
  const [isLoading, setIsLoading] = useState(true);
  const [showShareModal, setShowShareModal] = useState(false);
  const [peerConnections, setPeerConnections] = useState<Map<string, RTCPeerConnection>>(new Map());
  const [joinRequests, setJoinRequests] = useState<any[]>([]);
  const [waitingForApproval, setWaitingForApproval] = useState(false);
  const [showControls, setShowControls] = useState(true);

  // Use refs to store the latest values for event handlers
  const localStreamRef = useRef<MediaStream | null>(null);
  const peerConnectionsRef = useRef<Map<string, RTCPeerConnection>>(new Map());
  const pendingIceCandidatesRef = useRef<Map<string, RTCIceCandidate[]>>(new Map());
  const hideControlsTimeoutRef = useRef<NodeJS.Timeout | null>(null);

  useEffect(() => {
    // Wait for user to be loaded from Auth context before initializing meeting
    if (!user) {
      console.log("[WebRTC] Waiting for user to be loaded...");
      return;
    }

    let isMounted = true;
    let hasInitialized = false;

    const init = async () => {
      if (!isMounted || hasInitialized) return;
      hasInitialized = true;
      await initializeMeeting();
    };

    init();

    return () => {
      isMounted = false;
      cleanup();
    };
  }, [user]); // Run when user becomes available

  const initializeMeeting = async () => {
    try {
      // Fetch meeting details
      const response = await apiService.getMeeting(meetingCode);
      if (!response.status || !response.data) {
        alert("Meeting not found");
        router.push("/dashboard");
        return;
      }

      setMeeting(response.data);

      // Get local media
      const stream = await navigator.mediaDevices.getUserMedia({
        video: true,
        audio: true,
      });
      setLocalStream(stream);
      localStreamRef.current = stream;

      // First, join via HTTP to create participant record in database
      const httpJoinResponse = await apiService.joinMeeting(meetingCode);
      console.log("HTTP Join response:", httpJoinResponse);

      // Handle "already in meeting" or "already pending" as success (React Strict Mode double-mount)
      if (!httpJoinResponse.status) {
        const isAlreadyJoined = httpJoinResponse.message?.includes("already in this meeting") ||
                                 httpJoinResponse.message?.includes("already have a pending join request");
        if (!isAlreadyJoined) {
          alert(httpJoinResponse.message || "Failed to join meeting");
          router.push("/dashboard");
          return;
        }
        // If already joined/pending, continue with the flow
        console.log("Already joined or pending, continuing...");
      }

      // Connect to SignalR
      await meetingSignalRService.connect();

      // Setup event handlers
      setupSignalRHandlers();

      // Join meeting via SignalR for real-time updates
      const joinResult = await meetingSignalRService.joinMeeting(meetingCode);
      console.log("SignalR Joined meeting:", joinResult);

      // Check if approval is required
      if (joinResult.requiresApproval) {
        setWaitingForApproval(true);
        setIsLoading(false);
        return;
      }

      // Add current participants to state and create peer connections
      if (joinResult.currentParticipants && joinResult.currentParticipants.length > 0) {
        const participantsList = joinResult.currentParticipants.map((p: any) => ({
          userId: p.userId,
          userName: p.userName,
          displayName: p.displayName,
          profileImageUrl: p.profileImageUrl,
          connectionId: p.connectionId
        }));
        setParticipants(participantsList);

        console.log(`[WebRTC] Cascading offer pattern: New joiner will create offers to ALL ${participantsList.length} existing participants`);

       
        if (user) {
          console.log(`[WebRTC] User is available (userId=${user.id}), creating offers...`);
          for (const participant of joinResult.currentParticipants) {
            console.log(`[WebRTC] Creating offer to existing participant ${participant.userId} (${participant.displayName})`);
            await createPeerConnection(participant.userId, participant.connectionId, stream, true);
          }
        } else {
          console.error(`[WebRTC] ERROR: User is not available! Cannot create offers to existing participants.`);
        }
      }

      setIsLoading(false);
    } catch (error) {
      console.error("Error initializing meeting:", error);
      alert("Failed to join meeting");
      router.push("/dashboard");
    }
  };

  const setupSignalRHandlers = () => {
    // Remove any existing handlers first to prevent duplicates
    meetingSignalRService.off("ParticipantJoined", handleParticipantJoined);
    meetingSignalRService.off("ParticipantLeft", handleParticipantLeft);
    meetingSignalRService.off("ReceiveOffer", handleReceiveOffer);
    meetingSignalRService.off("ReceiveAnswer", handleReceiveAnswer);
    meetingSignalRService.off("ReceiveIceCandidate", handleReceiveIceCandidate);
    meetingSignalRService.off("JoinRequestReceived", handleJoinRequestReceived);
    meetingSignalRService.off("JoinRequestApproved", handleJoinRequestApproved);
    meetingSignalRService.off("JoinRequestRejected", handleJoinRequestRejected);

    // Add handlers
    meetingSignalRService.on("ParticipantJoined", handleParticipantJoined);
    meetingSignalRService.on("ParticipantLeft", handleParticipantLeft);
    meetingSignalRService.on("ReceiveOffer", handleReceiveOffer);
    meetingSignalRService.on("ReceiveAnswer", handleReceiveAnswer);
    meetingSignalRService.on("ReceiveIceCandidate", handleReceiveIceCandidate);
    meetingSignalRService.on("JoinRequestReceived", handleJoinRequestReceived);
    meetingSignalRService.on("JoinRequestApproved", handleJoinRequestApproved);
    meetingSignalRService.on("JoinRequestRejected", handleJoinRequestRejected);
  };

  const createPeerConnection = async (
    userId: number,
    connectionId: string,
    stream: MediaStream,
    createOffer: boolean
  ): Promise<RTCPeerConnection> => {
    // Check if peer connection already exists (use ref to avoid duplicates)
    const existingPc = peerConnectionsRef.current.get(connectionId);
    if (existingPc) {
      console.log("[WebRTC] Peer connection already exists for", connectionId, "- skipping duplicate");
      return existingPc;
    }

    // Fetch ICE servers
    const iceResponse = await apiService.getIceServers();
    const iceServers = iceResponse.data || [];

    const pc = new RTCPeerConnection({
      iceServers: iceServers.map(server => ({
        urls: server.urls,
        username: server.username,
        credential: server.credential
      })),
      iceCandidatePoolSize: 10,
    });

    // Add local stream
    stream.getTracks().forEach(track => {
      pc.addTrack(track, stream);
    });

    // Handle remote stream - create a new MediaStream and collect tracks
    const remoteStream = new MediaStream();
    pc.ontrack = (event) => {
      console.log("[WebRTC] Received remote track:", event.track.kind, "from", userId, "connectionId:", connectionId);

      // Add track to the remote stream
      event.streams[0].getTracks().forEach((track) => {
        console.log("[WebRTC] Adding track to remote stream:", track.kind);
        remoteStream.addTrack(track);
      });

      setParticipants(prev => {
        const existingIndex = prev.findIndex(p => p.connectionId === connectionId);
        if (existingIndex !== -1) {
          // Create a new participant object instead of mutating
          const updated = [...prev];
          updated[existingIndex] = {
            ...updated[existingIndex],
            stream: remoteStream
          };
          console.log("[WebRTC] Updated participant stream for", connectionId, "total tracks:", remoteStream.getTracks().length);
          return updated;
        } else {
          // Participant not in list yet, add them
          console.log("[WebRTC] Adding new participant with stream:", connectionId, "total tracks:", remoteStream.getTracks().length);
          return [...prev, {
            userId,
            userName: "",
            displayName: "",
            connectionId,
            stream: remoteStream
          }];
        }
      });
    };

    // Handle ICE candidates
    pc.onicecandidate = async (event) => {
      if (event.candidate) {
        console.log("[WebRTC] ICE candidate generated:", {
          type: event.candidate.type,
          protocol: event.candidate.protocol,
          address: event.candidate.address,
          targetConnectionId: connectionId
        });
        await meetingSignalRService.sendIceCandidate(
          meetingCode,
          connectionId,
          event.candidate.candidate,
          event.candidate.sdpMid || "",
          event.candidate.sdpMLineIndex || 0
        );
      }
    };

    // Handle ICE connection state
    pc.oniceconnectionstatechange = () => {
      console.log("[WebRTC] ICE connection state:", pc.iceConnectionState, "for", connectionId);
    };

    // Handle connection state
    pc.onconnectionstatechange = () => {
      console.log("[WebRTC] Connection state:", pc.connectionState, "for", connectionId);

      if (pc.connectionState === 'connected') {
        console.log("[WebRTC] Peer connection established successfully for", connectionId);
      } else if (pc.connectionState === 'failed') {
        console.log("[WebRTC] Peer connection failed for", connectionId);
      }
    };

    // Store peer connection in both state and ref
    setPeerConnections(prev => new Map(prev).set(connectionId, pc));
    peerConnectionsRef.current.set(connectionId, pc);

    // Create offer if initiator
    if (createOffer) {
      const offer = await pc.createOffer();
      await pc.setLocalDescription(offer);
      await meetingSignalRService.sendOffer(meetingCode, userId, offer.sdp!);
    }

    return pc;
  };

  const handleParticipantJoined = async (data: any) => {
    console.log("Participant joined:", data);

    // Check if participant already exists to prevent duplicates
    setParticipants(prev => {
      const exists = prev.some(p => p.connectionId === data.connectionId);
      if (exists) {
        console.log("Participant already in list, skipping");
        return prev;
      }

      return [...prev, {
        userId: data.userId,
        userName: data.userName,
        displayName: data.displayName,
        profileImageUrl: data.profileImageUrl,
        connectionId: data.connectionId
      }];
    });

    
    console.log(`[WebRTC] Participant ${data.userId} (${data.displayName}) joined - waiting for their offer`);
  };

  const handleParticipantLeft = (data: any) => {
    console.log("Participant left:", data);

    setParticipants(prev => prev.filter(p => p.userId !== data.userId));

    // Close peer connection
    const connections = peerConnections;
    for (const [connId, pc] of connections.entries()) {
      if (connId.includes(data.userId.toString())) {
        pc.close();
        connections.delete(connId);
      }
    }
    setPeerConnections(new Map(connections));
  };

  const handleReceiveOffer = async (data: any) => {
    console.log("[WebRTC] Received offer from:", data.fromUserId, "connectionId:", data.fromConnectionId);

    const stream = localStreamRef.current;
    if (!stream) {
      console.error("[WebRTC] No local stream available");
      return;
    }


    const existingPc = peerConnectionsRef.current.get(data.fromConnectionId);

    // If connection already exists and is connected or connecting, ignore the offer
    if (existingPc) {
      const state = existingPc.iceConnectionState;
      if (state === 'connected' || state === 'completed') {
        console.log("[WebRTC] Connection already established for", data.fromConnectionId, "- ignoring offer");
        return;
      }

      // Only handle glare condition if we're in the middle of negotiation
      if (existingPc.signalingState === "have-local-offer") {
        console.warn("[WebRTC] Glare condition! Both sides sent offers");

        if (user && user.id < data.fromUserId) {
          console.log("[WebRTC] We have lower ID, keeping our offer");
          return;
        } else {
          console.log("[WebRTC] They have lower ID, accepting their offer");
          existingPc.close();
          peerConnectionsRef.current.delete(data.fromConnectionId);
          setPeerConnections(prev => {
            const newMap = new Map(prev);
            newMap.delete(data.fromConnectionId);
            return newMap;
          });
        }
      } else {
        // Connection exists but not established yet, close and recreate
        console.log("[WebRTC] Connection exists but not established, recreating");
        existingPc.close();
        peerConnectionsRef.current.delete(data.fromConnectionId);
      }
    }

    const pc = await createPeerConnection(data.fromUserId, data.fromConnectionId, stream, false);
    await pc.setRemoteDescription(new RTCSessionDescription({ type: "offer", sdp: data.sdp }));
    console.log("[WebRTC] Set remote description (offer)");

    // Process any queued ICE candidates
    const queuedCandidates = pendingIceCandidatesRef.current.get(data.fromConnectionId);
    if (queuedCandidates && queuedCandidates.length > 0) {
      console.log(`[WebRTC] Processing ${queuedCandidates.length} queued ICE candidates`);
      for (const candidate of queuedCandidates) {
        await pc.addIceCandidate(candidate);
      }
      pendingIceCandidatesRef.current.delete(data.fromConnectionId);
    }

    const answer = await pc.createAnswer();
    await pc.setLocalDescription(answer);
    console.log("[WebRTC] Created and set local description (answer)");

    await meetingSignalRService.sendAnswer(meetingCode, data.fromConnectionId, answer.sdp!);
    console.log("[WebRTC] Sent answer to:", data.fromConnectionId);
  };

  const handleReceiveAnswer = async (data: any) => {
    console.log("[WebRTC] Received answer from:", data.fromUserId, "connectionId:", data.fromConnectionId);

    const pc = peerConnectionsRef.current.get(data.fromConnectionId);
    if (pc) {
      // Check if we're in the correct state to receive an answer
      if (pc.signalingState === "have-local-offer") {
        await pc.setRemoteDescription(new RTCSessionDescription({ type: "answer", sdp: data.sdp }));
        console.log("[WebRTC] Set remote description (answer)");
      } else {
        console.warn("[WebRTC] Ignoring answer - peer connection in wrong state:", pc.signalingState);
      }
    } else {
      console.error("[WebRTC] No peer connection found for:", data.fromConnectionId);
    }
  };

  const handleReceiveIceCandidate = async (data: any) => {
    console.log("[WebRTC] Received ICE candidate from:", data.fromUserId, "connectionId:", data.fromConnectionId);

    const pc = peerConnectionsRef.current.get(data.fromConnectionId);
    if (pc) {
      const candidate = new RTCIceCandidate({
        candidate: data.candidate,
        sdpMid: data.sdpMid,
        sdpMLineIndex: data.sdpMLineIndex
      });

      // Check if remote description is set
      if (pc.remoteDescription) {
        await pc.addIceCandidate(candidate);
        console.log("[WebRTC] Added ICE candidate");
      } else {
        // Queue the candidate until remote description is set
        const queue = pendingIceCandidatesRef.current.get(data.fromConnectionId) || [];
        queue.push(candidate);
        pendingIceCandidatesRef.current.set(data.fromConnectionId, queue);
        console.log("[WebRTC] Queued ICE candidate (waiting for remote description)");
      }
    } else {
      console.error("[WebRTC] No peer connection found for:", data.fromConnectionId);
    }
  };

  const handleJoinRequestReceived = (data: any) => {
    console.log("[JOIN REQUEST] Received join request:", data);
    setJoinRequests(prev => {
      // Prevent duplicates by checking if request already exists
      if (prev.some(req => req.requestId === data.requestId)) {
        console.log("[JOIN REQUEST] Duplicate request ignored:", data.requestId);
        return prev;
      }
      return [...prev, data];
    });
  };

  const handleJoinRequestApproved = async (data: any) => {
    console.log("Join request approved:", data);
    setWaitingForApproval(false);
    // Retry joining the meeting
    window.location.reload();
  };

  const handleJoinRequestRejected = (data: any) => {
    console.log("Join request rejected:", data);
    alert(data.message || "Your join request was rejected");
    router.push("/dashboard");
  };

  const approveJoinRequest = async (requestId: number) => {
    try {
      await meetingSignalRService.approveJoinRequest(requestId);
      setJoinRequests(prev => prev.filter(r => r.requestId !== requestId));
    } catch (error) {
      console.error("Error approving join request:", error);
    }
  };

  const rejectJoinRequest = async (requestId: number) => {
    try {
      await meetingSignalRService.rejectJoinRequest(requestId);
      setJoinRequests(prev => prev.filter(r => r.requestId !== requestId));
    } catch (error) {
      console.error("Error rejecting join request:", error);
    }
  };

  const toggleAudio = () => {
    if (localStream) {
      localStream.getAudioTracks().forEach(track => {
        track.enabled = !track.enabled;
      });
      setIsAudioMuted(!isAudioMuted);
    }
  };

  const toggleVideo = () => {
    if (localStream) {
      localStream.getVideoTracks().forEach(track => {
        track.enabled = !track.enabled;
      });
      setIsVideoOff(!isVideoOff);
    }
  };

  const leaveMeeting = async () => {
    await cleanup();
    router.push("/dashboard");
  };

  const cleanup = async () => {
    // Stop local stream
    if (localStream) {
      localStream.getTracks().forEach(track => track.stop());
    }

    // Close all peer connections
    peerConnections.forEach(pc => pc.close());
    setPeerConnections(new Map());

    // Leave meeting on server
    try {
      await meetingSignalRService.leaveMeeting(meetingCode);
      await meetingSignalRService.disconnect();
    } catch (error) {
      console.error("Error leaving meeting:", error);
    }
  };

  const copyMeetingLink = () => {
    const link = `${window.location.origin}/meeting/${meetingCode}`;
    navigator.clipboard.writeText(link);
    alert("Meeting link copied to clipboard!");
  };

  // Update video elements when participant streams change
  useEffect(() => {
    participants.forEach(participant => {
      const videoElement = document.getElementById(`remote-video-${participant.connectionId}`) as HTMLVideoElement;
      if (videoElement && participant.stream) {
        // Only set srcObject if it's different to avoid interrupting playback
        if (videoElement.srcObject !== participant.stream) {
          console.log("[WebRTC] useEffect: Setting srcObject for", participant.connectionId, "stream tracks:", participant.stream.getTracks().length);
          videoElement.srcObject = participant.stream;
          // Explicitly play the video
          videoElement.play().catch((err) => {
            console.log("[WebRTC] Auto-play prevented for", participant.connectionId, ":", err.message);
          });
        }
      }
    });
  }, [participants]);

  // Auto-hide controls on mouse inactivity
  useEffect(() => {
    const handleMouseMove = () => {
      setShowControls(true);

      // Clear existing timeout
      if (hideControlsTimeoutRef.current) {
        clearTimeout(hideControlsTimeoutRef.current);
      }

      // Set new timeout to hide controls after 5 seconds
      hideControlsTimeoutRef.current = setTimeout(() => {
        setShowControls(false);
      }, 5000);
    };

    window.addEventListener('mousemove', handleMouseMove);

    // Initial timeout
    hideControlsTimeoutRef.current = setTimeout(() => {
      setShowControls(false);
    }, 5000);

    return () => {
      window.removeEventListener('mousemove', handleMouseMove);
      if (hideControlsTimeoutRef.current) {
        clearTimeout(hideControlsTimeoutRef.current);
      }
    };
  }, []);

  if (isLoading) {
    return (
      <div className="min-h-screen bg-gray-900 flex items-center justify-center">
        <div className="text-white text-xl">Loading meeting...</div>
      </div>
    );
  }

  if (waitingForApproval) {
    return (
      <div className="min-h-screen bg-gray-900 flex items-center justify-center">
        <div className="text-center">
          <div className="text-white text-xl mb-4">Waiting for host approval...</div>
          <p className="text-gray-400">The host needs to approve your join request</p>
          <button
            onClick={() => router.push("/dashboard")}
            className="mt-6 px-6 py-3 bg-gray-700 hover:bg-gray-600 text-white rounded"
          >
            Cancel
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gray-900 flex flex-col relative">
      {/* Header */}
      <div className="bg-gray-800 px-6 py-4 flex items-center justify-between">
        <div>
          <h1 className="text-white text-xl font-semibold">{meeting?.title}</h1>
          <p className="text-gray-400 text-sm">Code: {meetingCode}</p>
        </div>
        <button
          onClick={() => setShowShareModal(true)}
          className="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded"
        >
          Share
        </button>
      </div>

      {/* Join Requests - Google Meet style popup in bottom-right */}
      <div className="fixed bottom-24 right-6 z-50 space-y-3">
        {joinRequests.map((request, index) => (
          <div
            key={request.requestId}
            className="bg-white rounded-lg shadow-2xl p-4 w-80 animate-slide-in-right"
            style={{ animationDelay: `${index * 100}ms` }}
          >
            <div className="flex items-start space-x-3">
              <div className="flex-shrink-0">
                <div className="w-10 h-10 bg-blue-600 rounded-full flex items-center justify-center text-white font-semibold">
                  {(request.displayName || request.userName || "?").charAt(0).toUpperCase()}
                </div>
              </div>
              <div className="flex-1 min-w-0">
                <p className="text-sm font-medium text-gray-900 truncate">
                  {request.displayName || request.userName}
                </p>
                <p className="text-sm text-gray-500">wants to join the meeting</p>
              </div>
            </div>
            <div className="mt-4 flex space-x-2">
              <button
                onClick={() => rejectJoinRequest(request.requestId)}
                className="flex-1 px-4 py-2 border border-gray-300 rounded-md text-sm font-medium text-gray-700 hover:bg-gray-50 transition-colors"
              >
                Deny
              </button>
              <button
                onClick={() => approveJoinRequest(request.requestId)}
                className="flex-1 px-4 py-2 bg-blue-600 border border-transparent rounded-md text-sm font-medium text-white hover:bg-blue-700 transition-colors"
              >
                Admit
              </button>
            </div>
          </div>
        ))}
      </div>

      {/* Video Grid */}
      <div className="flex-1 p-4 grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        {/* Local video */}
        <div className="bg-gray-800 rounded-lg relative aspect-video">
          <video
            ref={(video) => {
              if (video && localStream) {
                video.srcObject = localStream;
              }
            }}
            autoPlay
            playsInline
            muted
            className="w-full h-full object-cover rounded-lg"
          />
          <div className="absolute bottom-2 left-2 bg-black bg-opacity-50 text-white px-2 py-1 rounded text-sm">
            You {isAudioMuted && "(Muted)"}
          </div>
        </div>

        {/* Remote videos */}
        {participants.map((participant) => (
          <div key={participant.connectionId} className="bg-gray-800 rounded-lg relative aspect-video">
            <video
              id={`remote-video-${participant.connectionId}`}
              autoPlay
              playsInline
              className="w-full h-full object-cover rounded-lg"
            />
            {!participant.stream && (
              <div className="absolute inset-0 flex items-center justify-center text-white bg-gray-900 bg-opacity-75">
                Loading...
              </div>
            )}
            <div className="absolute bottom-2 left-2 bg-black bg-opacity-50 text-white px-2 py-1 rounded text-sm">
              {participant.displayName || participant.userName}
            </div>
          </div>
        ))}
      </div>

      {/* Controls - positioned absolutely at bottom, auto-hide */}
      <div className={`absolute bottom-0 left-0 right-0 px-6 py-4 flex items-center justify-center space-x-4 transition-opacity duration-300 ${showControls ? 'opacity-100' : 'opacity-0 pointer-events-none'}`}>
        {/* Mute Button */}
        <button
          onClick={toggleAudio}
          className={`p-4 rounded-full transition-colors duration-200 ${
            isAudioMuted
              ? "bg-red-600 hover:bg-red-700"
              : "bg-gray-700 hover:bg-gray-600"
          }`}
          title={isAudioMuted ? "Unmute" : "Mute"}
        >
          {isAudioMuted ? (
            <svg
              xmlns="http://www.w3.org/2000/svg"
              className="h-6 w-6 text-white"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M5.586 15H4a1 1 0 01-1-1v-4a1 1 0 011-1h1.586l4.707-4.707C10.923 3.663 12 4.109 12 5v14c0 .891-1.077 1.337-1.707.707L5.586 15z"
              />
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M17 14l2-2m0 0l2-2m-2 2l-2-2m2 2l2 2"
              />
            </svg>
          ) : (
            <svg
              xmlns="http://www.w3.org/2000/svg"
              className="h-6 w-6 text-white"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M19 11a7 7 0 01-7 7m0 0a7 7 0 01-7-7m7 7v4m0 0H8m4 0h4m-4-8a3 3 0 01-3-3V5a3 3 0 116 0v6a3 3 0 01-3 3z"
              />
            </svg>
          )}
        </button>

        {/* Video Toggle Button */}
        <button
          onClick={toggleVideo}
          className={`p-4 rounded-full transition-colors duration-200 ${
            isVideoOff
              ? "bg-red-600 hover:bg-red-700"
              : "bg-gray-700 hover:bg-gray-600"
          }`}
          title={isVideoOff ? "Turn on camera" : "Turn off camera"}
        >
          {isVideoOff ? (
            <svg
              xmlns="http://www.w3.org/2000/svg"
              className="h-6 w-6 text-white"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M18.364 18.364A9 9 0 005.636 5.636m12.728 12.728A9 9 0 015.636 5.636m12.728 12.728L5.636 5.636"
              />
            </svg>
          ) : (
            <svg
              xmlns="http://www.w3.org/2000/svg"
              className="h-6 w-6 text-white"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M15 10l4.553-2.276A1 1 0 0121 8.618v6.764a1 1 0 01-1.447.894L15 14M5 18h8a2 2 0 002-2V8a2 2 0 00-2-2H5a2 2 0 00-2 2v8a2 2 0 002 2z"
              />
            </svg>
          )}
        </button>

        {/* End Call Button */}
        <button
          onClick={leaveMeeting}
          className="p-4 bg-red-600 hover:bg-red-700 rounded-full transition-colors duration-200"
          title="Leave meeting"
        >
          <svg
            xmlns="http://www.w3.org/2000/svg"
            className="h-6 w-6 text-white"
            fill="none"
            viewBox="0 0 24 24"
            stroke="currentColor"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={2}
              d="M16 8l2-2m0 0l2-2m-2 2l-2-2m2 2l2 2M5 3a2 2 0 00-2 2v1c0 8.284 6.716 15 15 15h1a2 2 0 002-2v-3.28a1 1 0 00-.684-.948l-4.493-1.498a1 1 0 00-1.21.502l-1.13 2.257a11.042 11.042 0 01-5.516-5.517l2.257-1.128a1 1 0 00.502-1.21L9.228 3.683A1 1 0 008.279 3H5z"
            />
          </svg>
        </button>
      </div>

      {/* Share Modal */}
      {showShareModal && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
          <div className="bg-white rounded-lg p-6 max-w-md w-full">
            <h2 className="text-xl font-semibold mb-4">Share Meeting</h2>
            <div className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">Meeting Code</label>
                <div className="flex items-center space-x-2">
                  <input
                    type="text"
                    value={meetingCode}
                    readOnly
                    className="flex-1 px-3 py-2 border border-gray-300 rounded"
                  />
                  <button
                    onClick={() => {
                      navigator.clipboard.writeText(meetingCode);
                      alert("Code copied!");
                    }}
                    className="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded"
                  >
                    Copy
                  </button>
                </div>
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">Meeting Link</label>
                <div className="flex items-center space-x-2">
                  <input
                    type="text"
                    value={`${window.location.origin}/meeting/${meetingCode}`}
                    readOnly
                    className="flex-1 px-3 py-2 border border-gray-300 rounded text-sm"
                  />
                  <button
                    onClick={copyMeetingLink}
                    className="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded"
                  >
                    Copy
                  </button>
                </div>
              </div>
            </div>
            <div className="mt-6 flex justify-end">
              <button
                onClick={() => setShowShareModal(false)}
                className="bg-gray-300 hover:bg-gray-400 text-gray-800 px-4 py-2 rounded"
              >
                Close
              </button>
            </div>
          </div>
        </div>
      )}

    </div>
  );
}
