"use client";

import { useState } from "react";
import { useAuth } from "@/lib/contexts/AuthContext";

export default function LoginForm({ onSwitchToRegister }: { onSwitchToRegister: () => void }) {
  const { login } = useAuth();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [isLoading, setIsLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");
    setIsLoading(true);

    try {
      const result = await login({
        email,
        password,
        deviceToken: navigator.userAgent,
        deviceName: navigator.userAgent.substring(0, 50),
        deviceType: /Mobile|Android|iPhone/i.test(navigator.userAgent) ? "Mobile" : "Desktop",
      });

      console.log("Login result:", result);

      if (!result.success) {
        setError(result.message);
      }
    } catch (err) {
      console.error("Login error:", err);
      setError("An error occurred during login");
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="w-full max-w-md p-8 bg-gray-900 rounded-lg border border-gray-800">
      <h2 className="text-3xl font-bold text-center mb-6 text-white">
        Welcome to Ecosphere
      </h2>

      <form onSubmit={handleSubmit} className="space-y-4">
        <div>
          <label htmlFor="email" className="block text-sm font-medium text-gray-300 mb-1">
            Email
          </label>
          <input
            id="email"
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
            className="w-full px-4 py-2 border border-gray-700 rounded-md focus:ring-2 focus:ring-blue-500 focus:border-transparent bg-black text-white placeholder-gray-500"
            placeholder="your@email.com"
          />
        </div>

        <div>
          <label htmlFor="password" className="block text-sm font-medium text-gray-300 mb-1">
            Password
          </label>
          <input
            id="password"
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
            className="w-full px-4 py-2 border border-gray-700 rounded-md focus:ring-2 focus:ring-blue-500 focus:border-transparent bg-black text-white placeholder-gray-500"
            placeholder="••••••••"
          />
        </div>

        {error && (
          <div className="p-3 bg-red-900 border border-red-700 text-red-200 rounded-md text-sm">
            {error}
          </div>
        )}

        <button
          type="submit"
          disabled={isLoading}
          className="w-full py-2 px-4 bg-blue-600 hover:bg-blue-700 disabled:bg-blue-400 text-white font-medium rounded-md transition-colors duration-200"
        >
          {isLoading ? "Signing in..." : "Sign In"}
        </button>
      </form>

      <div className="mt-6 text-center">
        <p className="text-sm text-gray-400">
          Don&apos;t have an account?{" "}
          <button
            onClick={onSwitchToRegister}
            className="text-blue-400 hover:underline font-medium"
          >
            Create one
          </button>
        </p>
      </div>
    </div>
  );
}
