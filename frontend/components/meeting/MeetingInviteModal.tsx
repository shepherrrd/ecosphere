"use client";

import { useRouter } from "next/navigation";
import { soundManager } from "@/lib/utils/soundManager";

interface MeetingInviteModalProps {
  meetingCode: string;
  meetingTitle: string;
  inviterName: string;
  onAccept: () => void;
  onReject: () => void;
}

export default function MeetingInviteModal({
  meetingCode,
  meetingTitle,
  inviterName,
  onAccept,
  onReject,
}: MeetingInviteModalProps) {
  const router = useRouter();

  const handleAccept = () => {
    soundManager.stopLoop();
    onAccept();
    router.push(`/meeting/${meetingCode}`);
  };

  const handleReject = () => {
    soundManager.stopLoop();
    onReject();
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black bg-opacity-75">
      <div className="relative bg-gray-900 rounded-lg border-2 border-purple-500 p-8 max-w-md w-full mx-4 shadow-2xl shadow-purple-500/50 animate-pulse-slow">
        {/* Meeting Icon */}
        <div className="flex justify-center mb-6">
          <div className="w-20 h-20 bg-gradient-to-br from-purple-600 to-blue-600 rounded-full flex items-center justify-center">
            <svg
              className="w-10 h-10 text-white"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z"
              />
            </svg>
          </div>
        </div>

        {/* Content */}
        <div className="text-center mb-6">
          <h2 className="text-2xl font-bold text-white mb-2">Meeting Invitation</h2>
          <p className="text-gray-300 text-lg mb-1">
            <span className="font-semibold text-purple-400">{inviterName}</span> is inviting you to join
          </p>
          <p className="text-xl font-bold text-white">{meetingTitle}</p>
          <p className="text-sm text-gray-400 mt-2">Meeting Code: {meetingCode}</p>
        </div>

        {/* Buttons */}
        <div className="flex gap-4">
          <button
            onClick={handleReject}
            className="flex-1 px-6 py-3 bg-red-600 hover:bg-red-700 text-white font-semibold rounded-lg transition-all duration-200 transform hover:scale-105"
          >
            Decline
          </button>
          <button
            onClick={handleAccept}
            className="flex-1 px-6 py-3 bg-gradient-to-r from-green-600 to-blue-600 hover:from-green-700 hover:to-blue-700 text-white font-semibold rounded-lg transition-all duration-200 transform hover:scale-105 shadow-lg"
          >
            Join Meeting
          </button>
        </div>
      </div>
    </div>
  );
}
