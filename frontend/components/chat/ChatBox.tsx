"use client";

import { useState, useEffect, useRef } from "react";
import { useAuth } from "@/lib/contexts/AuthContext";
import { soundManager } from "@/lib/utils/soundManager";
import { messageHubService } from "@/lib/services/messageHub.service";
import { apiService } from "@/lib/services/api.service";

interface Message {
  id: number;
  senderId: number;
  senderName: string;
  receiverId: number;
  content: string;
  timestamp: Date;
  isRead: boolean;
}

interface ChatBoxProps {
  contactUserId: number;
  contactName: string;
  onClose: () => void;
  onMessagesRead?: (contactUserId: number) => void;
}

export default function ChatBox({ contactUserId, contactName, onClose, onMessagesRead }: ChatBoxProps) {
  const { user } = useAuth();
  const [messages, setMessages] = useState<Message[]>([]);
  const [newMessage, setNewMessage] = useState("");
  const [isLoading, setIsLoading] = useState(false);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  };

  useEffect(() => {
    scrollToBottom();
  }, [messages]);

  useEffect(() => {
    loadMessages();

    // Mark messages as read when chat opens
    const markAsRead = async () => {
      try {
        await messageHubService.markMessagesAsRead(contactUserId);
        // Notify parent to clear unread count
        onMessagesRead?.(contactUserId);
      } catch (error) {
        console.error("Error marking messages as read:", error);
      }
    };
    markAsRead();

    // Listen for incoming messages
    const handleReceiveMessage = (message: any) => {
      if (message.senderId === contactUserId) {
        setMessages((prev) => [...prev, {
          id: message.id,
          senderId: message.senderId,
          senderName: message.senderName,
          receiverId: message.receiverId,
          content: message.content,
          timestamp: new Date(message.sentAt),
          isRead: message.isRead,
        }]);

        // Play incoming message sound
        soundManager.play("incomingMessage");

        // Mark this message as read immediately
        messageHubService.markMessagesAsRead(contactUserId).catch(console.error);
      }
    };

    messageHubService.on("ReceiveDirectMessage", handleReceiveMessage);

    return () => {
      messageHubService.off("ReceiveDirectMessage", handleReceiveMessage);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [contactUserId]);

  const loadMessages = async () => {
    setIsLoading(true);
    try {
      const response = await apiService.getMessages(contactUserId);
      if (response.status && response.data) {
        setMessages(response.data.map((msg: any) => ({
          id: msg.id,
          senderId: msg.senderId,
          senderName: msg.senderName,
          receiverId: msg.receiverId,
          content: msg.content,
          timestamp: new Date(msg.sentAt),
          isRead: msg.isRead,
        })));
      }
    } catch (error) {
      console.error("Error loading messages:", error);
    } finally {
      setIsLoading(false);
    }
  };

  const handleSendMessage = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!newMessage.trim() || !user) return;

    const messageContent = newMessage.trim();
    setNewMessage("");

    try {
      // Optimistically add message to UI
      const tempMessage: Message = {
        id: Date.now(),
        senderId: user.id,
        senderName: user.displayName || user.userName,
        receiverId: contactUserId,
        content: messageContent,
        timestamp: new Date(),
        isRead: false,
      };
      setMessages([...messages, tempMessage]);

      // Send via MessageHub
      await messageHubService.sendDirectMessage(contactUserId, messageContent);

      // Play message sent sound
      soundManager.play("messageSent");
    } catch (error) {
      console.error("Error sending message:", error);
      // Restore message on error
      setNewMessage(messageContent);
      // Remove optimistic message
      setMessages(messages);
    }
  };

  return (
    <div className="flex flex-col h-full bg-gray-900 rounded-lg border border-gray-800 overflow-hidden">
      {/* Chat Header */}
      <div className="px-4 py-3 bg-gray-800 border-b border-gray-700 flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 bg-gray-700 rounded-full flex items-center justify-center text-white font-bold">
            {contactName.charAt(0).toUpperCase()}
          </div>
          <div>
            <h3 className="text-white font-semibold">{contactName}</h3>
            <p className="text-xs text-gray-400">Online</p>
          </div>
        </div>
        <button
          onClick={onClose}
          className="text-gray-400 hover:text-white transition-colors"
        >
          <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
          </svg>
        </button>
      </div>

      {/* Messages Area */}
      <div className="flex-1 overflow-y-auto p-4 space-y-3">
        {isLoading ? (
          <div className="flex items-center justify-center h-full">
            <p className="text-gray-400">Loading messages...</p>
          </div>
        ) : messages.length === 0 ? (
          <div className="flex items-center justify-center h-full">
            <p className="text-gray-400">No messages yet. Start the conversation!</p>
          </div>
        ) : (
          messages.map((message) => {
            const isSentByMe = message.senderId === user?.id;
            return (
              <div
                key={message.id}
                className={`flex ${isSentByMe ? "justify-end" : "justify-start"}`}
              >
                <div
                  className={`max-w-xs lg:max-w-md px-4 py-2 rounded-lg ${
                    isSentByMe
                      ? "bg-blue-600 text-white"
                      : "bg-gray-800 text-gray-100"
                  }`}
                >
                  <p className="text-sm break-words">{message.content}</p>
                  <p className={`text-xs mt-1 ${isSentByMe ? "text-blue-200" : "text-gray-400"}`}>
                    {new Date(message.timestamp).toLocaleTimeString([], {
                      hour: "2-digit",
                      minute: "2-digit",
                    })}
                  </p>
                </div>
              </div>
            );
          })
        )}
        <div ref={messagesEndRef} />
      </div>

      {/* Message Input */}
      <form onSubmit={handleSendMessage} className="p-4 bg-gray-800 border-t border-gray-700">
        <div className="flex gap-2">
          <input
            type="text"
            value={newMessage}
            onChange={(e) => setNewMessage(e.target.value)}
            placeholder="Type a message..."
            className="flex-1 px-4 py-2 bg-gray-900 border border-gray-700 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent text-white placeholder-gray-500"
          />
          <button
            type="submit"
            disabled={!newMessage.trim()}
            className="px-6 py-2 bg-blue-600 hover:bg-blue-700 disabled:bg-gray-700 disabled:cursor-not-allowed text-white rounded-lg transition-colors duration-200 font-semibold"
          >
            Send
          </button>
        </div>
      </form>
    </div>
  );
}
