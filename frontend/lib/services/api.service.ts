import {
  BaseResponse,
  LoginRequest,
  RegisterRequest,
  AuthResponse,
  Contact,
  IceServerConfig,
} from "../types";

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000/api";

class ApiService {
  private getHeaders(includeAuth: boolean = false): HeadersInit {
    const headers: HeadersInit = {
      "Content-Type": "application/json",
    };

    if (includeAuth) {
      const token = this.getToken();
      if (token) {
        headers["Authorization"] = `Bearer ${token}`;
      }
    }

    return headers;
  }

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
    const response = await fetch(`${API_BASE_URL}/auth/login`, {
      method: "POST",
      headers: this.getHeaders(),
      body: JSON.stringify(request),
    });

    console.log("Login response status:", response.status);
    const data: BaseResponse<AuthResponse> = await response.json();
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
    const response = await fetch(`${API_BASE_URL}/auth/register`, {
      method: "POST",
      headers: this.getHeaders(),
      body: JSON.stringify(request),
    });

    console.log("Register response status:", response.status);
    const data: BaseResponse<AuthResponse> = await response.json();
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

    const response = await fetch(`${API_BASE_URL}/auth/RefreshToken`, {
      method: "POST",
      headers: this.getHeaders(),
      body: JSON.stringify({ refreshToken }),
    });

    if (!response.ok) {
      return { status: false, message: "Failed to refresh token", data: undefined };
    }

    const data = await response.json();

    if (data.status && data.data) {
      this.setToken(data.data.token);
      this.setRefreshToken(data.data.refreshToken);
    }

    return data;
  }

  async getContacts(): Promise<BaseResponse<Contact[]>> {
    const response = await fetch(`${API_BASE_URL}/contact`, {
      method: "GET",
      headers: this.getHeaders(true),
    });

    return await response.json();
  }

  async sendContactRequest(username: string): Promise<BaseResponse<string>> {
    const response = await fetch(`${API_BASE_URL}/contact/request`, {
      method: "POST",
      headers: this.getHeaders(true),
      body: JSON.stringify({ username }),
    });

    return await response.json();
  }

  async getPendingContactRequests(): Promise<BaseResponse<any[]>> {
    const response = await fetch(`${API_BASE_URL}/contact/requests/pending`, {
      method: "GET",
      headers: this.getHeaders(true),
    });

    return await response.json();
  }

  async approveContactRequest(requestId: number): Promise<BaseResponse<string>> {
    const response = await fetch(`${API_BASE_URL}/contact/request/${requestId}/approve`, {
      method: "POST",
      headers: this.getHeaders(true),
    });

    return await response.json();
  }

  async rejectContactRequest(requestId: number): Promise<BaseResponse<string>> {
    const response = await fetch(`${API_BASE_URL}/contact/request/${requestId}/reject`, {
      method: "POST",
      headers: this.getHeaders(true),
    });

    return await response.json();
  }

  async createMeeting(title: string, description?: string, maxParticipants: number = 50, isPublic: boolean = false): Promise<BaseResponse<any>> {
    const response = await fetch(`${API_BASE_URL}/meeting`, {
      method: "POST",
      headers: this.getHeaders(true),
      body: JSON.stringify({ title, description, maxParticipants, isPublic }),
    });

    return await response.json();
  }

  async joinMeeting(meetingCode: string): Promise<BaseResponse<string>> {
    const response = await fetch(`${API_BASE_URL}/meeting/join`, {
      method: "POST",
      headers: this.getHeaders(true),
      body: JSON.stringify({ meetingCode }),
    });

    return await response.json();
  }

  async getMeeting(meetingCode: string): Promise<BaseResponse<any>> {
    const response = await fetch(`${API_BASE_URL}/meeting/${meetingCode}`, {
      method: "GET",
      headers: this.getHeaders(true),
    });

    return await response.json();
  }

  async getIceServers(): Promise<BaseResponse<IceServerConfig[]>> {
    const response = await fetch(`${API_BASE_URL}/iceconfig`, {
      method: "GET",
      headers: this.getHeaders(true),
    });

    return await response.json();
  }

  logout(): void {
    this.clearTokens();
  }
}

export const apiService = new ApiService();
