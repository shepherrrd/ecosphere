"use client";

import { useState, useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/contexts/AuthContext";
import { useCall, CallState } from "@/lib/contexts/CallContext";
import { Contact } from "@/lib/types";
import { apiService } from "@/lib/services/api.service";
import { signalRService } from "@/lib/services/signalr.service";
import { messageHubService } from "@/lib/services/messageHub.service";
import IncomingCallModal from "../call/IncomingCallModal";
import ActiveCallView from "../call/ActiveCallView";
import ChatBox from "../chat/ChatBox";
import MeetingInviteModal from "../meeting/MeetingInviteModal";
import { soundManager } from "@/lib/utils/soundManager";

export default function Dashboard() {
  const router = useRouter();
  const { user, logout } = useAuth();
  const { callState, initiateCall } = useCall();
  const [contacts, setContacts] = useState<Contact[]>([]);
  const [pendingRequests, setPendingRequests] = useState<any[]>([]);
  const [unreadCounts, setUnreadCounts] = useState<Record<number, number>>({});
  const [isLoading, setIsLoading] = useState(true);
  const [showAddContact, setShowAddContact] = useState(false);
  const [showCreateMeeting, setShowCreateMeeting] = useState(false);
  const [showJoinMeeting, setShowJoinMeeting] = useState(false);
  const [newContactUsername, setNewContactUsername] = useState("");
  const [meetingTitle, setMeetingTitle] = useState("");
  const [meetingCode, setMeetingCode] = useState("");
  const [createdMeetingCode, setCreatedMeetingCode] = useState("");
  const [isPublicMeeting, setIsPublicMeeting] = useState(false);
  const [activeChatContact, setActiveChatContact] = useState<{ id: number; name: string } | null>(null);
  const [isChatVisible, setIsChatVisible] = useState(false);
  const [meetingInvite, setMeetingInvite] = useState<{
    meetingCode: string;
    meetingTitle: string;
    inviterName: string;
  } | null>(null);

  useEffect(() => {
    loadData();

    // Listen for meeting invites
    const handleMeetingInvite = (invite: any) => {
      console.log("[Dashboard] Received meeting invite:", invite);
      setMeetingInvite({
        meetingCode: invite.meetingCode,
        meetingTitle: invite.meetingTitle,
        inviterName: invite.inviterName,
      });

      // Play meeting ringing sound in loop
      soundManager.playLoop("meetingRinging");
    };

    // Listen for incoming direct messages
    const handleReceiveDirectMessage = (message: any) => {
      console.log("[Dashboard] Received direct message:", message);

      // Only play sound and update count if chat is not open for this contact
      if (!activeChatContact || activeChatContact.id !== message.senderId) {
        // Play incoming message sound
        soundManager.play("incomingMessage");

        // Update unread count
        setUnreadCounts((prev) => ({
          ...prev,
          [message.senderId]: (prev[message.senderId] || 0) + 1,
        }));
      }
    };

    signalRService.on("MeetingInvite", handleMeetingInvite);
    messageHubService.on("ReceiveDirectMessage", handleReceiveDirectMessage);

    return () => {
      signalRService.off("MeetingInvite", handleMeetingInvite);
      messageHubService.off("ReceiveDirectMessage", handleReceiveDirectMessage);
    };
  }, [activeChatContact]);

  const loadData = async () => {
    await Promise.all([loadContacts(), loadPendingRequests(), loadUnreadCounts()]);
  };

  const loadContacts = async () => {
    try {
      const response = await apiService.getContacts();
      if (response.status && response.data) {
        setContacts(response.data);
      }
    } catch (error) {
      console.error("Error loading contacts:", error);
    } finally {
      setIsLoading(false);
    }
  };

  const loadPendingRequests = async () => {
    try {
      const response = await apiService.getPendingContactRequests();
      if (response.status && response.data) {
        setPendingRequests(response.data);
      }
    } catch (error) {
      console.error("Error loading pending requests:", error);
    }
  };

  const loadUnreadCounts = async () => {
    try {
      const response = await apiService.getUnreadMessageCounts();
      if (response.status && response.data) {
        setUnreadCounts(response.data);
      }
    } catch (error) {
      console.error("Error loading unread counts:", error);
    }
  };

  const handleSendContactRequest = async () => {
    try {
      if (!newContactUsername.trim()) {
        alert("Please enter a username");
        return;
      }

      const response = await apiService.sendContactRequest(newContactUsername);
      if (response.status) {
        alert(response.message);
        setShowAddContact(false);
        setNewContactUsername("");
      } else {
        alert(response.message);
      }
    } catch (error) {
      console.error("Error sending contact request:", error);
      alert("Failed to send contact request");
    }
  };

  const handleApproveRequest = async (requestId: number) => {
    try {
      const response = await apiService.approveContactRequest(requestId);
      if (response.status) {
        alert(response.message);
        await loadData();
      } else {
        alert(response.message);
      }
    } catch (error) {
      console.error("Error approving request:", error);
      alert("Failed to approve request");
    }
  };

  const handleRejectRequest = async (requestId: number) => {
    try {
      const response = await apiService.rejectContactRequest(requestId);
      if (response.status) {
        alert(response.message);
        await loadData();
      } else {
        alert(response.message);
      }
    } catch (error) {
      console.error("Error rejecting request:", error);
      alert("Failed to reject request");
    }
  };

  const handleCreateMeeting = async () => {
    try {
      if (!meetingTitle.trim()) {
        alert("Please enter a meeting title");
        return;
      }

      const response = await apiService.createMeeting(meetingTitle, undefined, 50, isPublicMeeting);
      if (response.status && response.data) {
        // Redirect to meeting room
        router.push(`/meeting/${response.data.meetingCode}`);
      } else {
        alert(response.message);
      }
    } catch (error) {
      console.error("Error creating meeting:", error);
      alert("Failed to create meeting");
    }
  };

  const handleJoinMeeting = async () => {
    try {
      if (!meetingCode.trim()) {
        alert("Please enter a meeting code");
        return;
      }

      const response = await apiService.joinMeeting(meetingCode);
      if (response.status) {
        // Redirect to meeting room
        router.push(`/meeting/${meetingCode}`);
      } else {
        // If already have a pending request, just go to the meeting page
        if (response.message?.includes("already have a pending join request") ||
            response.message?.includes("already in this meeting")) {
          console.log("Already joined or pending, redirecting to meeting page");
          router.push(`/meeting/${meetingCode}`);
        } else {
          alert(response.message);
        }
      }
    } catch (error) {
      console.error("Error joining meeting:", error);
      alert("Failed to join meeting");
    }
  };

  const handleCall = async (contactUserId: number, isVideo: boolean) => {
    try {
      await initiateCall(contactUserId, isVideo);
    } catch (error) {
      console.error("Error initiating call:", error);
      alert("Failed to initiate call");
    }
  };

  const handleLogout = () => {
    logout();
    router.push("/");
  };

  // Show meeting invite modal if there's an invite
  if (meetingInvite) {
    return (
      <MeetingInviteModal
        meetingCode={meetingInvite.meetingCode}
        meetingTitle={meetingInvite.meetingTitle}
        inviterName={meetingInvite.inviterName}
        onAccept={() => setMeetingInvite(null)}
        onReject={() => setMeetingInvite(null)}
      />
    );
  }

  // Show call UI when in a call
  if (callState === CallState.IncomingCall) {
    return <IncomingCallModal />;
  }

  if (
    callState === CallState.Ringing ||
    callState === CallState.Connecting ||
    callState === CallState.Active
  ) {
    return <ActiveCallView />;
  }

  return (
    <div className="min-h-screen bg-black">
      {/* Header */}
      <header className="bg-gray-900 border-b border-gray-800">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-4 flex justify-between items-center">
          <div>
            <h1 className="text-2xl font-bold text-white">Ecosphere</h1>
            <p className="text-sm text-gray-400">
              Welcome, {user?.displayName || user?.userName}
            </p>
          </div>
          <button
            onClick={handleLogout}
            className="px-4 py-2 bg-red-600 hover:bg-red-700 text-white rounded-md transition-colors duration-200"
          >
            Logout
          </button>
        </div>
      </header>

      {/* Main Content */}
      <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        {/* Meeting Actions */}
        <div className="mb-6 grid grid-cols-1 md:grid-cols-2 gap-4">
          <div className="bg-gray-900 rounded-lg border border-gray-800 p-4">
            <h3 className="text-lg font-semibold text-white mb-3">Create Meeting</h3>
            {!showCreateMeeting ? (
              <button
                onClick={() => setShowCreateMeeting(true)}
                className="w-full px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-md transition-colors duration-200"
              >
                New Meeting
              </button>
            ) : (
              <div className="space-y-2">
                <input
                  type="text"
                  value={meetingTitle}
                  onChange={(e) => setMeetingTitle(e.target.value)}
                  placeholder="Meeting title"
                  className="w-full px-4 py-2 border border-gray-700 rounded-md focus:ring-2 focus:ring-blue-500 focus:border-transparent bg-black text-white placeholder-gray-500"
                />
                <label className="flex items-center space-x-2 text-white cursor-pointer">
                  <input
                    type="checkbox"
                    checked={isPublicMeeting}
                    onChange={(e) => setIsPublicMeeting(e.target.checked)}
                    className="w-4 h-4 text-blue-600 bg-gray-700 border-gray-600 rounded focus:ring-blue-500"
                  />
                  <span className="text-sm">Make this meeting public (anyone with code can join)</span>
                </label>
                <div className="flex gap-2">
                  <button
                    onClick={handleCreateMeeting}
                    className="flex-1 px-4 py-2 bg-green-600 hover:bg-green-700 text-white rounded-md transition-colors duration-200"
                  >
                    Create
                  </button>
                  <button
                    onClick={() => {
                      setShowCreateMeeting(false);
                      setMeetingTitle("");
                    }}
                    className="flex-1 px-4 py-2 bg-gray-600 hover:bg-gray-700 text-white rounded-md transition-colors duration-200"
                  >
                    Cancel
                  </button>
                </div>
                {createdMeetingCode && (
                  <div className="mt-2 p-3 bg-green-900 border border-green-700 rounded-md">
                    <p className="text-sm text-green-200">Meeting Code:</p>
                    <p className="text-lg font-bold text-white">{createdMeetingCode}</p>
                  </div>
                )}
              </div>
            )}
          </div>

          <div className="bg-gray-900 rounded-lg border border-gray-800 p-4">
            <h3 className="text-lg font-semibold text-white mb-3">Join Meeting</h3>
            {!showJoinMeeting ? (
              <button
                onClick={() => setShowJoinMeeting(true)}
                className="w-full px-4 py-2 bg-purple-600 hover:bg-purple-700 text-white rounded-md transition-colors duration-200"
              >
                Join with Code
              </button>
            ) : (
              <div className="space-y-2">
                <input
                  type="text"
                  value={meetingCode}
                  onChange={(e) => setMeetingCode(e.target.value.toUpperCase())}
                  placeholder="Enter meeting code"
                  maxLength={10}
                  className="w-full px-4 py-2 border border-gray-700 rounded-md focus:ring-2 focus:ring-purple-500 focus:border-transparent bg-black text-white placeholder-gray-500 uppercase"
                />
                <div className="flex gap-2">
                  <button
                    onClick={handleJoinMeeting}
                    className="flex-1 px-4 py-2 bg-green-600 hover:bg-green-700 text-white rounded-md transition-colors duration-200"
                  >
                    Join
                  </button>
                  <button
                    onClick={() => {
                      setShowJoinMeeting(false);
                      setMeetingCode("");
                    }}
                    className="flex-1 px-4 py-2 bg-gray-600 hover:bg-gray-700 text-white rounded-md transition-colors duration-200"
                  >
                    Cancel
                  </button>
                </div>
              </div>
            )}
          </div>
        </div>

        {/* Pending Contact Requests */}
        {pendingRequests.length > 0 && (
          <div className="mb-6 bg-gray-900 rounded-lg border border-gray-800">
            <div className="px-6 py-4 border-b border-gray-800">
              <h2 className="text-xl font-semibold text-white">Pending Contact Requests</h2>
            </div>
            <div className="divide-y divide-gray-800">
              {pendingRequests.map((request) => (
                <div key={request.id} className="px-6 py-4 flex items-center justify-between">
                  <div className="flex items-center gap-4">
                    <div className="w-12 h-12 bg-gray-700 rounded-full flex items-center justify-center text-white text-xl font-bold">
                      {request.sender?.displayName?.charAt(0).toUpperCase() || request.sender?.userName?.charAt(0).toUpperCase() || "?"}
                    </div>
                    <div>
                      <h3 className="text-lg font-medium text-white">
                        {request.sender?.displayName || request.sender?.userName || "Unknown User"}
                      </h3>
                      <p className="text-sm text-gray-400">@{request.sender?.userName || "unknown"}</p>
                    </div>
                  </div>
                  <div className="flex gap-2">
                    <button
                      onClick={() => handleApproveRequest(request.id)}
                      className="px-4 py-2 bg-green-600 hover:bg-green-700 text-white rounded-md transition-colors duration-200"
                    >
                      Accept
                    </button>
                    <button
                      onClick={() => handleRejectRequest(request.id)}
                      className="px-4 py-2 bg-red-600 hover:bg-red-700 text-white rounded-md transition-colors duration-200"
                    >
                      Reject
                    </button>
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Contacts */}
        <div className="bg-gray-900 rounded-lg border border-gray-800">
          <div className="px-6 py-4 border-b border-gray-800 flex justify-between items-center">
            <h2 className="text-xl font-semibold text-white">Contacts</h2>
            <button
              onClick={() => setShowAddContact(!showAddContact)}
              className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-md transition-colors duration-200 flex items-center gap-2"
            >
              <svg
                xmlns="http://www.w3.org/2000/svg"
                className="h-5 w-5"
                viewBox="0 0 20 20"
                fill="currentColor"
              >
                <path
                  fillRule="evenodd"
                  d="M10 3a1 1 0 011 1v5h5a1 1 0 110 2h-5v5a1 1 0 11-2 0v-5H4a1 1 0 110-2h5V4a1 1 0 011-1z"
                  clipRule="evenodd"
                />
              </svg>
              Add Contact
            </button>
          </div>

          {/* Add Contact Form */}
          {showAddContact && (
            <div className="px-6 py-4 bg-gray-800 border-b border-gray-700">
              <div className="flex gap-2">
                <input
                  type="text"
                  value={newContactUsername}
                  onChange={(e) => setNewContactUsername(e.target.value)}
                  placeholder="Enter username"
                  className="flex-1 px-4 py-2 border border-gray-700 rounded-md focus:ring-2 focus:ring-blue-500 focus:border-transparent bg-black text-white placeholder-gray-500"
                />
                <button
                  onClick={handleSendContactRequest}
                  className="px-4 py-2 bg-green-600 hover:bg-green-700 text-white rounded-md transition-colors duration-200"
                >
                  Send Request
                </button>
                <button
                  onClick={() => {
                    setShowAddContact(false);
                    setNewContactUsername("");
                  }}
                  className="px-4 py-2 bg-gray-600 hover:bg-gray-700 text-white rounded-md transition-colors duration-200"
                >
                  Cancel
                </button>
              </div>
            </div>
          )}

          {/* Contacts List */}
          <div className="divide-y divide-gray-800">
            {isLoading ? (
              <div className="px-6 py-8 text-center text-gray-400">
                Loading contacts...
              </div>
            ) : contacts.length === 0 ? (
              <div className="px-6 py-8 text-center text-gray-400">
                No contacts yet. Send a contact request to start calling!
              </div>
            ) : (
              contacts.map((contact) => (
                <div
                  key={contact.id}
                  className="px-6 py-4 hover:bg-gray-800 transition-colors duration-200"
                >
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-4">
                      <div className="w-12 h-12 bg-gray-700 rounded-full flex items-center justify-center text-white text-xl font-bold relative">
                        {contact.contactUser?.displayName?.charAt(0).toUpperCase() || contact.contactUser?.userName?.charAt(0).toUpperCase() || "?"}
                        {unreadCounts[contact.contactUserId] > 0 && (
                          <span className="absolute -top-1 -right-1 bg-red-500 text-white text-xs font-bold rounded-full h-6 w-6 flex items-center justify-center">
                            {unreadCounts[contact.contactUserId] > 9 ? "9+" : unreadCounts[contact.contactUserId]}
                          </span>
                        )}
                      </div>
                      <div>
                        <h3 className="text-lg font-medium text-white flex items-center gap-2">
                          {contact.contactUser?.displayName || contact.contactUser?.userName || "Unknown User"}
                          {unreadCounts[contact.contactUserId] > 0 && (
                            <span className="bg-red-500 text-white text-xs px-2 py-1 rounded-full">
                              {unreadCounts[contact.contactUserId]} new
                            </span>
                          )}
                        </h3>
                        <p className="text-sm text-gray-400">
                          @{contact.contactUser?.userName || "unknown"}
                          {contact.contactUser?.isOnline && (
                            <span className="ml-2 inline-flex items-center">
                              <span className="h-2 w-2 bg-green-500 rounded-full mr-1"></span>
                              Online
                            </span>
                          )}
                        </p>
                      </div>
                    </div>

                    <div className="flex gap-2">
                      <button
                        onClick={() => {
                          setActiveChatContact({
                            id: contact.contactUserId,
                            name: contact.contactUser?.displayName || contact.contactUser?.userName || "Unknown"
                          });
                          setIsChatVisible(true);
                        }}
                        className="p-3 bg-purple-600 hover:bg-purple-700 text-white rounded-full transition-colors duration-200"
                        title="Send message"
                      >
                        <svg
                          xmlns="http://www.w3.org/2000/svg"
                          className="h-5 w-5"
                          viewBox="0 0 20 20"
                          fill="currentColor"
                        >
                          <path d="M2.003 5.884L10 9.882l7.997-3.998A2 2 0 0016 4H4a2 2 0 00-1.997 1.884z" />
                          <path d="M18 8.118l-8 4-8-4V14a2 2 0 002 2h12a2 2 0 002-2V8.118z" />
                        </svg>
                      </button>
                      <button
                        onClick={() => handleCall(contact.contactUserId, false)}
                        className="p-3 bg-green-600 hover:bg-green-700 text-white rounded-full transition-colors duration-200"
                        title="Audio call"
                      >
                        <svg
                          xmlns="http://www.w3.org/2000/svg"
                          className="h-5 w-5"
                          viewBox="0 0 20 20"
                          fill="currentColor"
                        >
                          <path d="M2 3a1 1 0 011-1h2.153a1 1 0 01.986.836l.74 4.435a1 1 0 01-.54 1.06l-1.548.773a11.037 11.037 0 006.105 6.105l.774-1.548a1 1 0 011.059-.54l4.435.74a1 1 0 01.836.986V17a1 1 0 01-1 1h-2C7.82 18 2 12.18 2 5V3z" />
                        </svg>
                      </button>
                      <button
                        onClick={() => handleCall(contact.contactUserId, true)}
                        className="p-3 bg-blue-600 hover:bg-blue-700 text-white rounded-full transition-colors duration-200"
                        title="Video call"
                      >
                        <svg
                          xmlns="http://www.w3.org/2000/svg"
                          className="h-5 w-5"
                          viewBox="0 0 20 20"
                          fill="currentColor"
                        >
                          <path d="M2 6a2 2 0 012-2h6a2 2 0 012 2v8a2 2 0 01-2 2H4a2 2 0 01-2-2V6zM14.553 7.106A1 1 0 0014 8v4a1 1 0 00.553.894l2 1A1 1 0 0018 13V7a1 1 0 00-1.447-.894l-2 1z" />
                        </svg>
                      </button>
                    </div>
                  </div>
                </div>
              ))
            )}
          </div>
        </div>

        {/* Info Box */}

      </main>

      {/* Chat Box - Fixed position overlay */}
      {activeChatContact && (
        <div className={`fixed bottom-4 right-4 w-96 h-[600px] z-50 shadow-2xl transition-transform duration-300 ${
          isChatVisible ? 'translate-x-0' : 'translate-x-[120%]'
        }`}>
          <ChatBox
            contactUserId={activeChatContact.id}
            contactName={activeChatContact.name}
            onClose={() => setIsChatVisible(false)}
            onMessagesRead={(contactUserId) => {
              setUnreadCounts((prev) => {
                const updated = { ...prev };
                delete updated[contactUserId];
                return updated;
              });
            }}
          />
        </div>
      )}
    </div>
  );
}
