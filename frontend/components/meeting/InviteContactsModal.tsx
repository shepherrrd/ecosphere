// "use client";

// import { useState, useEffect } from "react";
// import { apiService } from "@/lib/services/api.service";
// import { meetingSignalRService } from "@/lib/services/meeting-signalr.service";

// interface Contact {
//   id: number;
//   contactUserId: number;
//   contactUser: {
//     id: number;
//     userName: string;
//     displayName: string;
//     isOnline: boolean;
//   };
// }

// interface InviteContactsModalProps {
//   meetingCode: string;
//   onClose: () => void;
// }

// export default function InviteContactsModal({ meetingCode, onClose }: InviteContactsModalProps) {
//   const [contacts, setContacts] = useState<Contact[]>([]);
//   const [isLoading, setIsLoading] = useState(true);
//   const [invitedContacts, setInvitedContacts] = useState<Set<number>>(new Set());

//   useEffect(() => {
//     loadContacts();
//     ensureMeetingHubConnection();

//     // Listen for errors from the server
//     const handleError = (message: string) => {
//       alert(`Error: ${message}`);
//     };

//     meetingSignalRService.on("Error", handleError);

//     return () => {
//       meetingSignalRService.off("Error", handleError);
//     };
//   }, []);

//   const ensureMeetingHubConnection = async () => {
//     try {
//       await meetingSignalRService.connect();
//     } catch (error) {
//       console.error("[InviteModal] Failed to connect to meeting hub:", error);
//     }
//   };

//   const loadContacts = async () => {
//     try {
//       const response = await apiService.getContacts();
//       if (response.status && response.data) {
//         setContacts(response.data);
//       }
//     } catch (error) {
//       console.error("Error loading contacts:", error);
//     } finally {
//       setIsLoading(false);
//     }
//   };

//   const handleInvite = async (contactUserId: number) => {
//     try {
//       await meetingSignalRService.invoke("InviteUserToMeeting", meetingCode, contactUserId);
//       setInvitedContacts((prev) => new Set(prev).add(contactUserId));
//       console.log(`[InviteModal] Invited user ${contactUserId} to meeting ${meetingCode}`);
//     } catch (error) {
//       console.error("Error inviting contact:", error);
//       alert("Failed to send invite. Please try again.");
//     }
//   };

//   return (
//     <div className="fixed inset-0 z-50 flex items-center justify-center bg-black bg-opacity-75">
//       <div className="bg-gray-900 rounded-lg border border-gray-800 p-6 max-w-md w-full mx-4 max-h-[80vh] flex flex-col">
//         {/* Header */}
//         <div className="flex items-center justify-between mb-4">
//           <h2 className="text-2xl font-bold text-white">Invite to Meeting</h2>
//           <button
//             onClick={onClose}
//             className="text-gray-400 hover:text-white transition-colors"
//           >
//             <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
//               <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
//             </svg>
//           </button>
//         </div>

//         {/* Content */}
//         <div className="flex-1 overflow-y-auto">
//           {isLoading ? (
//             <div className="flex items-center justify-center py-8">
//               <p className="text-gray-400">Loading contacts...</p>
//             </div>
//           ) : contacts.length === 0 ? (
//             <div className="flex items-center justify-center py-8">
//               <p className="text-gray-400">No contacts available</p>
//             </div>
//           ) : (
//             <div className="space-y-2">
//               {contacts.map((contact) => {
//                 const isInvited = invitedContacts.has(contact.contactUserId);
//                 return (
//                   <div
//                     key={contact.id}
//                     className="flex items-center justify-between p-3 bg-gray-800 rounded-lg hover:bg-gray-750 transition-colors"
//                   >
//                     <div className="flex items-center gap-3">
//                       <div className="w-10 h-10 bg-gray-700 rounded-full flex items-center justify-center text-white font-bold">
//                         {contact.contactUser?.displayName?.charAt(0).toUpperCase() ||
//                           contact.contactUser?.userName?.charAt(0).toUpperCase() ||
//                           "?"}
//                       </div>
//                       <div>
//                         <h3 className="text-white font-medium">
//                           {contact.contactUser?.displayName || contact.contactUser?.userName || "Unknown"}
//                         </h3>
//                         <p className="text-sm text-gray-400">
//                           @{contact.contactUser?.userName || "unknown"}
//                           {contact.contactUser?.isOnline && (
//                             <span className="ml-2 inline-flex items-center">
//                               <span className="h-2 w-2 bg-green-500 rounded-full mr-1"></span>
//                               Online
//                             </span>
//                           )}
//                         </p>
//                       </div>
//                     </div>

//                     <button
//                       onClick={() => handleInvite(contact.contactUserId)}
//                       disabled={isInvited}
//                       className={`px-4 py-2 rounded-lg font-medium transition-colors ${
//                         isInvited
//                           ? "bg-green-900 text-green-200 cursor-not-allowed"
//                           : "bg-blue-600 hover:bg-blue-700 text-white"
//                       }`}
//                     >
//                       {isInvited ? "Invited" : "Invite"}
//                     </button>
//                   </div>
//                 );
//               })}
//             </div>
//           )}
//         </div>

//         {/* Footer */}
//         <div className="mt-4 pt-4 border-t border-gray-800">
//           <button
//             onClick={onClose}
//             className="w-full px-4 py-2 bg-gray-700 hover:bg-gray-600 text-white rounded-lg transition-colors"
//           >
//             Close
//           </button>
//         </div>
//       </div>
//     </div>
//   );
// }
