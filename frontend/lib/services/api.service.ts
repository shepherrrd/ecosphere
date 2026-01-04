import {
  BaseResponse,
  LoginRequest,
  RegisterRequest,
  AuthResponse,
  Contact,
  IceServerConfig,
} from "../types";
import { apiGet, apiPost } from "../utils/apiClient";

class ApiService {

  getToken(): string | null {
    if (typeof window === "undefined") return null;
    return localStorage.getItem("token");
  }

  getAccessToken(): string | null {
    return this.getToken();
  }

  setToken(token: string): void {
    if (typeof window === "undefined") return;
    localStorage.setItem("token", token);
  }

  getRefreshToken(): string | null {
    if (typeof window === "undefined") return null;
    return localStorage.getItem("refreshToken");
  }

  setRefreshToken(token: string): void {
    if (typeof window === "undefined") return;
    localStorage.setItem("refreshToken", token);
  }

  clearTokens(): void {
    if (typeof window === "undefined") return;
    localStorage.removeItem("token");
    localStorage.removeItem("refreshToken");
    localStorage.removeItem("user");
  }

  async login(request: LoginRequest): Promise<BaseResponse<AuthResponse>> {
    console.log("Login request:", request);

    const data = await apiPost<BaseResponse<AuthResponse>>(
      '/auth/login',
      request,
      undefined,
      { skipAuth: true }
    );

    console.log("Login response data:", data);

    if (data.status && data.data) {
      this.setToken(data.data.token);
      this.setRefreshToken(data.data.refreshToken);
      localStorage.setItem("user", JSON.stringify(data.data.user));
    }

    return data;
  }

  async register(request: RegisterRequest): Promise<BaseResponse<AuthResponse>> {
    console.log("Register request:", request);

    const data = await apiPost<BaseResponse<AuthResponse>>(
      '/auth/register',
      request,
      undefined,
      { skipAuth: true }
    );

    console.log("Register response data:", data);

    if (data.status && data.data) {
      this.setToken(data.data.token);
      this.setRefreshToken(data.data.refreshToken);
      localStorage.setItem("user", JSON.stringify(data.data.user));
    }

    return data;
  }

  async refreshToken(): Promise<BaseResponse<{ token: string; refreshToken: string }>> {
    const refreshToken = this.getRefreshToken();
    if (!refreshToken) {
      return { status: false, message: "No refresh token found", data: undefined };
    }

    try {
      const data = await apiPost<BaseResponse<{ token: string; refreshToken: string }>>(
        '/auth/RefreshToken',
        { refreshToken },
        undefined,
        { skipAuth: true }
      );

      if (data.status && data.data) {
        this.setToken(data.data.token);
        this.setRefreshToken(data.data.refreshToken);
      }

      return data;
    } catch (error) {
      return { status: false, message: "Failed to refresh token", data: undefined };
    }
  }

  async getContacts(): Promise<BaseResponse<Contact[]>> {
    return await apiGet<BaseResponse<Contact[]>>('/contact', this.getToken() || undefined);
  }

  async sendContactRequest(username: string): Promise<BaseResponse<string>> {
    return await apiPost<BaseResponse<string>>(
      '/contact/request',
      { username },
      this.getToken() || undefined
    );
  }

  async getPendingContactRequests(): Promise<BaseResponse<any[]>> {
    return await apiGet<BaseResponse<any[]>>(
      '/contact/requests/pending',
      this.getToken() || undefined
    );
  }

  async approveContactRequest(requestId: number): Promise<BaseResponse<string>> {
    return await apiPost<BaseResponse<string>>(
      `/contact/request/${requestId}/approve`,
      {},
      this.getToken() || undefined
    );
  }

  async rejectContactRequest(requestId: number): Promise<BaseResponse<string>> {
    return await apiPost<BaseResponse<string>>(
      `/contact/request/${requestId}/reject`,
      {},
      this.getToken() || undefined
    );
  }

  async createMeeting(title: string, description?: string, maxParticipants: number = 50, isPublic: boolean = false): Promise<BaseResponse<any>> {
    return await apiPost<BaseResponse<any>>(
      '/meeting',
      { title, description, maxParticipants, isPublic },
      this.getToken() || undefined
    );
  }

  async joinMeeting(meetingCode: string): Promise<BaseResponse<string>> {
    return await apiPost<BaseResponse<string>>(
      '/meeting/join',
      { meetingCode },
      this.getToken() || undefined
    );
  }

  async getMeeting(meetingCode: string): Promise<BaseResponse<any>> {
    return await apiGet<BaseResponse<any>>(
      `/meeting/${meetingCode}`,
      this.getToken() || undefined
    );
  }

  async getIceServers(): Promise<BaseResponse<IceServerConfig[]>> {
    return await apiGet<BaseResponse<IceServerConfig[]>>(
      '/iceconfig',
      this.getToken() || undefined
    );
  }

  // Message endpoints
  async getMessages(contactUserId: number, pageSize: number = 50, beforeMessageId?: number): Promise<BaseResponse<any[]>> {
    const params = new URLSearchParams();
    if (pageSize) params.append('pageSize', pageSize.toString());
    if (beforeMessageId) params.append('beforeMessageId', beforeMessageId.toString());

    const query = params.toString() ? `?${params.toString()}` : '';
    return await apiGet<BaseResponse<any[]>>(
      `/message/conversation/${contactUserId}${query}`,
      this.getToken() || undefined
    );
  }

  async getMeetingMessages(meetingId: number, pageSize: number = 100, beforeMessageId?: number): Promise<BaseResponse<any[]>> {
    const params = new URLSearchParams();
    if (pageSize) params.append('pageSize', pageSize.toString());
    if (beforeMessageId) params.append('beforeMessageId', beforeMessageId.toString());

    const query = params.toString() ? `?${params.toString()}` : '';
    return await apiGet<BaseResponse<any[]>>(
      `/message/meeting/${meetingId}${query}`,
      this.getToken() || undefined
    );
  }

  async sendMessage(receiverId: number, content: string, meetingId?: number): Promise<BaseResponse<any>> {
    return await apiPost<BaseResponse<any>>(
      '/message/send',
      { receiverId, content, meetingId },
      this.getToken() || undefined
    );
  }

  async getUnreadMessageCounts(): Promise<BaseResponse<Record<number, number>>> {
    return await apiGet<BaseResponse<Record<number, number>>>(
      '/message/unread-counts',
      this.getToken() || undefined
    );
  }

  logout(): void {
    this.clearTokens();
  }
}

export const apiService = new ApiService();
