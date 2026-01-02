"use client";

import { useCall } from "@/lib/contexts/CallContext";

export default function IncomingCallModal() {
  const { callerName, isVideoCall, acceptCall, rejectCall } = useCall();

  return (
    <div className="fixed inset-0 bg-black bg-opacity-95 flex items-center justify-center z-50">
      <div className="bg-gray-900 border border-gray-800 rounded-lg p-8 max-w-md w-full mx-4">
        <div className="text-center">
          <div className="mb-6">
            <div className="w-24 h-24 bg-gray-700 rounded-full mx-auto flex items-center justify-center text-white text-4xl font-bold">
              {callerName?.charAt(0).toUpperCase() || "?"}
            </div>
          </div>

          <h2 className="text-2xl font-bold text-white mb-2">
            {callerName || "Unknown"}
          </h2>

          <p className="text-gray-400 mb-8">
            Incoming {isVideoCall ? "video" : "audio"} call
          </p>

          <div className="flex gap-4 justify-center">
            <button
              onClick={rejectCall}
              className="px-8 py-3 bg-red-600 hover:bg-red-700 text-white font-medium rounded-full transition-colors duration-200 flex items-center gap-2"
            >
              <svg
                xmlns="http://www.w3.org/2000/svg"
                className="h-5 w-5"
                viewBox="0 0 20 20"
                fill="currentColor"
              >
                <path d="M2 3a1 1 0 011-1h2.153a1 1 0 01.986.836l.74 4.435a1 1 0 01-.54 1.06l-1.548.773a11.037 11.037 0 006.105 6.105l.774-1.548a1 1 0 011.059-.54l4.435.74a1 1 0 01.836.986V17a1 1 0 01-1 1h-2C7.82 18 2 12.18 2 5V3z" />
              </svg>
              Decline
            </button>

            <button
              onClick={acceptCall}
              className="px-8 py-3 bg-green-600 hover:bg-green-700 text-white font-medium rounded-full transition-colors duration-200 flex items-center gap-2"
            >
              <svg
                xmlns="http://www.w3.org/2000/svg"
                className="h-5 w-5"
                viewBox="0 0 20 20"
                fill="currentColor"
              >
                <path d="M2 3a1 1 0 011-1h2.153a1 1 0 01.986.836l.74 4.435a1 1 0 01-.54 1.06l-1.548.773a11.037 11.037 0 006.105 6.105l.774-1.548a1 1 0 011.059-.54l4.435.74a1 1 0 01.836.986V17a1 1 0 01-1 1h-2C7.82 18 2 12.18 2 5V3z" />
              </svg>
              Accept
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
