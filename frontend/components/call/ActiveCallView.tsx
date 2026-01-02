"use client";

import { useEffect, useRef, useState } from "react";
import { useCall, CallState } from "@/lib/contexts/CallContext";

export default function ActiveCallView() {
  const {
    callState,
    isVideoCall,
    localStream,
    remoteStream,
    isMuted,
    isVideoEnabled,
    endCall,
    toggleMute,
    toggleVideo,
  } = useCall();

  const localVideoRef = useRef<HTMLVideoElement>(null);
  const remoteVideoRef = useRef<HTMLVideoElement>(null);
  const remoteAudioRef = useRef<HTMLAudioElement>(null);
  const [showControls, setShowControls] = useState(true);
  const hideControlsTimeoutRef = useRef<NodeJS.Timeout | null>(null);

  useEffect(() => {
    if (localVideoRef.current && localStream) {
      localVideoRef.current.srcObject = localStream;
    }
  }, [localStream]);

  useEffect(() => {
    if (remoteVideoRef.current && remoteStream && isVideoCall) {
      remoteVideoRef.current.srcObject = remoteStream;
    }
  }, [remoteStream, isVideoCall]);

  useEffect(() => {
    if (remoteAudioRef.current && remoteStream && !isVideoCall) {
      remoteAudioRef.current.srcObject = remoteStream;
    }
  }, [remoteStream, isVideoCall]);

  const isRinging = callState === CallState.Ringing;
  const isConnecting = callState === CallState.Connecting;
  const isActive = callState === CallState.Active;

  // Auto-hide controls on mouse inactivity
  useEffect(() => {
    const handleMouseMove = () => {
      setShowControls(true);

      if (hideControlsTimeoutRef.current) {
        clearTimeout(hideControlsTimeoutRef.current);
      }

      hideControlsTimeoutRef.current = setTimeout(() => {
        setShowControls(false);
      }, 5000);
    };

    window.addEventListener('mousemove', handleMouseMove);

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

  return (
    <div className="fixed inset-0 bg-gray-900 z-50 flex flex-col overflow-hidden relative">
      {/* Hidden audio element for audio-only calls */}
      {!isVideoCall && (
        <audio ref={remoteAudioRef} autoPlay playsInline />
      )}

      {/* Remote Video / Status */}
      <div className="flex-1 relative overflow-hidden flex items-center justify-center bg-black">
        {isVideoCall && remoteStream ? (
          <video
            ref={remoteVideoRef}
            autoPlay
            playsInline
            className="max-w-full max-h-full object-contain"
          />
        ) : (
          <div className="text-center">
            <div className="w-32 h-32 bg-gray-800 rounded-full mx-auto flex items-center justify-center text-white text-6xl font-bold mb-4">
              ?
            </div>
            <p className="text-white text-xl">
              {isRinging && "Calling..."}
              {isConnecting && "Connecting..."}
              {isActive && "In Call"}
            </p>
          </div>
        )}

        {/* Local Video (Picture-in-Picture) */}
        {isVideoCall && localStream && (
          <div className="absolute bottom-24 right-4 w-48 h-36 bg-gray-800 rounded-lg overflow-hidden shadow-lg">
            <video
              ref={localVideoRef}
              autoPlay
              playsInline
              muted
              className="w-full h-full object-cover"
            />
          </div>
        )}
      </div>

      {/* Controls - positioned absolutely at bottom, auto-hide */}
      <div className={`absolute bottom-0 left-0 right-0 p-6 transition-opacity duration-300 ${showControls ? 'opacity-100' : 'opacity-0 pointer-events-none'}`}>
        <div className="max-w-md mx-auto flex justify-center gap-4">
          {/* Mute Button */}
          <button
            onClick={toggleMute}
            className={`p-4 rounded-full transition-colors duration-200 ${
              isMuted
                ? "bg-red-600 hover:bg-red-700"
                : "bg-gray-700 hover:bg-gray-600"
            }`}
            title={isMuted ? "Unmute" : "Mute"}
          >
            {isMuted ? (
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

          {/* Video Toggle (only for video calls) */}
          {isVideoCall && (
            <button
              onClick={toggleVideo}
              className={`p-4 rounded-full transition-colors duration-200 ${
                !isVideoEnabled
                  ? "bg-red-600 hover:bg-red-700"
                  : "bg-gray-700 hover:bg-gray-600"
              }`}
              title={isVideoEnabled ? "Turn off camera" : "Turn on camera"}
            >
              {isVideoEnabled ? (
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
                    d="M18.364 18.364A9 9 0 005.636 5.636m12.728 12.728A9 9 0 015.636 5.636m12.728 12.728L5.636 5.636"
                  />
                </svg>
              )}
            </button>
          )}

          {/* End Call Button */}
          <button
            onClick={endCall}
            className="p-4 bg-red-600 hover:bg-red-700 rounded-full transition-colors duration-200"
            title="End call"
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

          {/* Screen Share Button - Coming Soon */}
          <button
            disabled
            className="p-4 bg-gray-600 rounded-full opacity-50 cursor-not-allowed"
            title="Screen sharing - Coming Soon"
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
                d="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17h14a2 2 0 002-2V5a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z"
              />
            </svg>
          </button>
        </div>

        <p className="text-center text-gray-400 text-sm mt-4">
          Screen sharing coming soon
        </p>
      </div>
    </div>
  );
}
