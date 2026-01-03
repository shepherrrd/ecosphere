"use client";

import { useAuth } from "@/lib/contexts/AuthContext";
import LandingPage from "@/components/landing/LandingPage";

export default function HomePage() {
  const { isLoading } = useAuth();

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-black">
        <div className="text-gray-400">Loading...</div>
      </div>
    );
  }

  return <LandingPage />;
}
