// API Response types
export interface BaseResponse<T = any> {
  status: boolean;  
  message: string;
  data?: T;
  errors?: string[];
}

// User types
export interface User {
  id: number;
  userName: string;
  email: string;
  displayName?: string;
  profileImageUrl?: string;
  roles?: string[];
  isOnline?: boolean;
  lastSeen?: string;
}

// Auth types
export interface LoginRequest {
  email: string;
  password: string;
  deviceToken?: string;
  deviceName?: string;
  deviceType?: string;
}

export interface RegisterRequest {
  userName: string;
  email: string;
  password: string;
  displayName?: string;
  deviceToken?: string;
  deviceName?: string;
  deviceType?: string;
}

export interface AuthResponse {
  token: string;
  refreshToken: string;
  expires: number;
  user: User;
}

// Contact types
export interface Contact {
  id: number;
  userId: number;
  contactUserId: number;
  contactUser: User;
  createdAt: string;
}

// Call types
export enum CallType {
  Audio = 0,
  Video = 1,
  Conference = 2,
}

export enum CallStatus {
  Initiated = 0,
  Ringing = 1,
  InProgress = 2,
  Ended = 3,
  Missed = 4,
  Rejected = 5,
}

export enum CallParticipantStatus {
  Calling = 0,
  Ringing = 1,
  Joined = 2,
  Left = 3,
  Rejected = 4,
  Missed = 5,
}

export interface Call {
  id: number;
  callUuid: string;
  callType: CallType;
  status: CallStatus;
  initiatorId: number;
  startedAt?: string;
  endedAt?: string;
  duration?: string;
}

// ICE Server Configuration
export interface IceServerConfig {
  urls: string[];
  username?: string;
  credential?: string;
}

// SignalR Hub Events
export interface IncomingCallEvent {
  callUuid: string;
  callId: number;
  caller: {
    id: number;
    userName: string;
    displayName: string;
    profileImageUrl?: string;
  };
  isVideoCall: boolean;
}

export interface CallAnsweredEvent {
  callUuid: string;
  userId: number;
}

export interface CallEndedEvent {
  callUuid: string;
  endedBy: number;
}

export interface ReceiveOfferEvent {
  callUuid: string;
  sdp: string;
  fromUserId: number;
}

export interface ReceiveAnswerEvent {
  callUuid: string;
  sdp: string;
  fromUserId: number;
}

export interface ReceiveIceCandidateEvent {
  callUuid: string;
  candidate: string;
  sdpMid: string;
  sdpMLineIndex: number;
  fromUserId: number;
}
