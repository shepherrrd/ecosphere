"use client";

import { useState, useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/contexts/AuthContext";
import LoginForm from "@/components/auth/LoginForm";
import RegisterForm from "@/components/auth/RegisterForm";

export default function AuthPage() {
  const { isAuthenticated, isLoading } = useAuth();
  const router = useRouter();
  const [showLogin, setShowLogin] = useState(true);

  useEffect(() => {
    if (!isLoading && isAuthenticated) {
      router.push("/");
    }
  }, [isAuthenticated, isLoading, router]);

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-black">
        <div className="text-gray-400">Loading...</div>
      </div>
    );
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-black p-4">
      {showLogin ? (
        <LoginForm onSwitchToRegister={() => setShowLogin(false)} />
      ) : (
        <RegisterForm onSwitchToLogin={() => setShowLogin(true)} />
      )}
    </div>
  );
}
