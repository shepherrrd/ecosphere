"use client";

import React, { createContext, useContext, useState, useEffect, ReactNode } from "react";
import { User, LoginRequest, RegisterRequest } from "../types";
import { apiService } from "../services/api.service";
import { signalRService } from "../services/signalr.service";

interface AuthContextType {
  user: User | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (request: LoginRequest) => Promise<{ success: boolean; message: string; errors?: string[] }>;
  register: (request: RegisterRequest) => Promise<{ success: boolean; message: string; errors?: string[] }>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    // Check if user is already logged in
    const storedUser = localStorage.getItem("user");
    const token = apiService.getToken();

    if (storedUser && token) {
      setUser(JSON.parse(storedUser));
    }

    setIsLoading(false);
  }, []);

  // Automatic token refresh every 15 minutes
  useEffect(() => {
    if (!user) return;

    const refreshToken = async () => {
      try {
        const response = await apiService.refreshToken();
        if (response.status && response.data) {
          // Token refreshed successfully
          console.log("Token refreshed successfully");
        } else {
          // Refresh failed - just log it, don't logout (user might still have valid token)
          console.warn("Token refresh failed:", response.message);
        }
      } catch (error) {
        console.error("Error refreshing token:", error);
      }
    };

    // Set interval to refresh every 15 minutes (900000 ms)
    // Don't refresh immediately on mount - token is still fresh from login
    const intervalId = setInterval(refreshToken, 15 * 60 * 1000);

    return () => clearInterval(intervalId);
  }, [user]);

  const login = async (
    request: LoginRequest
  ): Promise<{ success: boolean; message: string; errors?: string[] }> => {
    try {
      const response = await apiService.login(request);

      if (response.status && response.data) {
        setUser(response.data.user);
        return { success: true, message: response.message };
      }

      // Check if response has errors array (from backend validation)
      const errors = (response as any).errors || [];
      return {
        success: false,
        message: response.message,
        errors: errors.length > 0 ? errors : undefined
      };
    } catch (error) {
      return { success: false, message: "An error occurred during login" };
    }
  };

  const register = async (
    request: RegisterRequest
  ): Promise<{ success: boolean; message: string; errors?: string[] }> => {
    try {
      const response = await apiService.register(request);

      if (response.status && response.data) {
        setUser(response.data.user);
        return { success: true, message: response.message };
      }

      // Check if response has errors array (from backend validation)
      const errors = (response as any).errors || [];
      return {
        success: false,
        message: response.message,
        errors: errors.length > 0 ? errors : undefined
      };
    } catch (error) {
      return { success: false, message: "An error occurred during registration" };
    }
  };

  const logout = () => {
    signalRService.disconnect();
    apiService.logout();
    setUser(null);
  };

  return (
    <AuthContext.Provider
      value={{
        user,
        isAuthenticated: !!user,
        isLoading,
        login,
        register,
        logout,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (context === undefined) {
    throw new Error("useAuth must be used within an AuthProvider");
  }
  return context;
}
