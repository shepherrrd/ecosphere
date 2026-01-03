"use client";

import { Suspense, useState, useEffect } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { useAuth } from "@/lib/contexts/AuthContext";
import LoginForm from "@/components/auth/LoginForm";
import RegisterForm from "@/components/auth/RegisterForm";
import SpaceBackground from "@/components/landing/SpaceBackground";

function AuthContent() {
  const { isAuthenticated, isLoading } = useAuth();
  const router = useRouter();
  const searchParams = useSearchParams();
  const mode = searchParams.get("mode");
  const [showLogin, setShowLogin] = useState(mode !== "register");

  useEffect(() => {
    if (mode === "register") {
      setShowLogin(false);
    } else if (mode === "login") {
      setShowLogin(true);
    }
  }, [mode]);

  useEffect(() => {
    if (!isLoading && isAuthenticated) {
      router.push("/dashboard");
    }
  }, [isAuthenticated, isLoading, router]);

  if (isLoading) {
    return (
      <div className="relative min-h-screen flex items-center justify-center">
        <SpaceBackground />
        <div className="relative z-10 text-gray-400">Loading...</div>
      </div>
    );
  }

  return (
    <div className="relative min-h-screen flex items-center justify-center p-4">
      <SpaceBackground />
      <div className="relative z-10 w-full max-w-md">
        {showLogin ? (
          <LoginForm onSwitchToRegister={() => setShowLogin(false)} />
        ) : (
          <RegisterForm onSwitchToLogin={() => setShowLogin(true)} />
        )}
      </div>
    </div>
  );
}

export default function AuthPage() {
  return (
    <Suspense fallback={
      <div className="relative min-h-screen flex items-center justify-center">
        <SpaceBackground />
        <div className="relative z-10 text-gray-400">Loading...</div>
      </div>
    }>
      <AuthContent />
    </Suspense>
  );
}
