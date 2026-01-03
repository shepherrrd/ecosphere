"use client";

import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/contexts/AuthContext";
import SpaceBackground from "./SpaceBackground";

export default function LandingPage() {
  const router = useRouter();
  const { isAuthenticated } = useAuth();

  return (
    <div className="relative min-h-screen overflow-hidden">
      <SpaceBackground />

      <div className="relative z-10">
        {/* Hero Section */}
        <div className="min-h-screen flex flex-col items-center justify-center px-4">
          <div className="text-center max-w-5xl mx-auto">
            <h1 className="text-7xl md:text-8xl font-bold mb-6 bg-gradient-to-r from-blue-400 via-purple-400 to-pink-400 bg-clip-text text-transparent animate-gradient">
              Ecosphere
            </h1>
            <p className="text-2xl md:text-3xl text-gray-300 mb-4 font-light">
              Connect Beyond Boundaries
            </p>
            <p className="text-lg md:text-xl text-gray-400 mb-12 max-w-3xl mx-auto leading-relaxed">
              Experience seamless video calling, instant messaging, and collaborative meetings
              in a unified platform designed for the modern world.
            </p>

            <div className="flex flex-col sm:flex-row gap-4 justify-center items-center">
              {isAuthenticated ? (
                <button
                  onClick={() => router.push("/dashboard")}
                  className="group relative px-8 py-4 bg-gray-800 text-white text-lg font-semibold rounded-full overflow-hidden transition-all duration-300 hover:scale-105 hover:shadow-2xl hover:shadow-purple-500/50"
                >
                  <span className="relative z-10">Go to Dashboard</span>
                  <div className="absolute inset-0 bg-gradient-to-r from-blue-600 via-purple-600 to-pink-600 opacity-0 group-hover:opacity-100 transition-opacity duration-500"></div>
                </button>
              ) : (
                <>
                  <button
                    onClick={() => router.push("/auth?mode=register")}
                    className="group relative px-8 py-4 bg-gray-800 text-white text-lg font-semibold rounded-full overflow-hidden transition-all duration-300 hover:scale-105 hover:shadow-2xl hover:shadow-purple-500/50"
                  >
                    <span className="relative z-10">Get Started</span>
                    <div className="absolute inset-0 bg-gradient-to-r from-blue-600 via-purple-600 to-pink-600 opacity-0 group-hover:opacity-100 transition-opacity duration-500"></div>
                  </button>

                  <button
                    onClick={() => router.push("/auth?mode=login")}
                    className="px-8 py-4 bg-transparent border-2 border-gray-600 text-white text-lg font-semibold rounded-full hover:border-blue-400 hover:text-blue-400 transition-all duration-300 hover:scale-105 hover:shadow-lg hover:shadow-blue-500/30"
                  >
                    Sign In
                  </button>
                </>
              )}
            </div>
          </div>

          {/* Scroll Indicator */}
          <div className="absolute bottom-10 left-1/2 transform -translate-x-1/2 animate-bounce">
            <svg
              className="w-6 h-6 text-gray-400"
              fill="none"
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth="2"
              viewBox="0 0 24 24"
              stroke="currentColor"
            >
              <path d="M19 14l-7 7m0 0l-7-7m7 7V3"></path>
            </svg>
          </div>
        </div>

        {/* Features Section */}
        <div className="min-h-screen flex items-center justify-center px-4 py-20">
          <div className="max-w-7xl mx-auto">
            <h2 className="text-5xl md:text-6xl font-bold text-center mb-16 text-white">
              Why Choose Ecosphere?
            </h2>

            <div className="grid md:grid-cols-3 gap-8">
              {/* Feature 1 */}
              <div className="group p-8 rounded-2xl bg-gradient-to-br from-gray-900/50 to-gray-800/30 backdrop-blur-sm border border-gray-700/50 hover:border-blue-500/50 transition-all duration-300 hover:scale-105 hover:shadow-2xl hover:shadow-blue-500/20">
                <div className="w-16 h-16 mb-6 rounded-full bg-gradient-to-br from-blue-500 to-blue-600 flex items-center justify-center group-hover:scale-110 transition-transform duration-300">
                  <svg className="w-8 h-8 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 10l4.553-2.276A1 1 0 0121 8.618v6.764a1 1 0 01-1.447.894L15 14M5 18h8a2 2 0 002-2V8a2 2 0 00-2-2H5a2 2 0 00-2 2v8a2 2 0 002 2z" />
                  </svg>
                </div>
                <h3 className="text-2xl font-bold mb-4 text-white">Crystal Clear Video</h3>
                <p className="text-gray-400 leading-relaxed">
                  Experience HD video calls with advanced WebRTC technology and intelligent bandwidth optimization.
                </p>
              </div>

              {/* Feature 2 */}
              <div className="group p-8 rounded-2xl bg-gradient-to-br from-gray-900/50 to-gray-800/30 backdrop-blur-sm border border-gray-700/50 hover:border-purple-500/50 transition-all duration-300 hover:scale-105 hover:shadow-2xl hover:shadow-purple-500/20">
                <div className="w-16 h-16 mb-6 rounded-full bg-gradient-to-br from-purple-500 to-purple-600 flex items-center justify-center group-hover:scale-110 transition-transform duration-300">
                  <svg className="w-8 h-8 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" />
                  </svg>
                </div>
                <h3 className="text-2xl font-bold mb-4 text-white">Team Meetings</h3>
                <p className="text-gray-400 leading-relaxed">
                  Host virtual meetings with real-time collaboration, screen sharing, and seamless participant management.
                </p>
              </div>

              {/* Feature 3 */}
              <div className="group p-8 rounded-2xl bg-gradient-to-br from-gray-900/50 to-gray-800/30 backdrop-blur-sm border border-gray-700/50 hover:border-pink-500/50 transition-all duration-300 hover:scale-105 hover:shadow-2xl hover:shadow-pink-500/20">
                <div className="w-16 h-16 mb-6 rounded-full bg-gradient-to-br from-pink-500 to-pink-600 flex items-center justify-center group-hover:scale-110 transition-transform duration-300">
                  <svg className="w-8 h-8 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
                  </svg>
                </div>
                <h3 className="text-2xl font-bold mb-4 text-white">Secure & Private</h3>
                <p className="text-gray-400 leading-relaxed">
                  End-to-end encryption ensures your conversations stay private with enterprise-grade security.
                </p>
              </div>

              {/* Feature 4 */}
              <div className="group p-8 rounded-2xl bg-gradient-to-br from-gray-900/50 to-gray-800/30 backdrop-blur-sm border border-gray-700/50 hover:border-green-500/50 transition-all duration-300 hover:scale-105 hover:shadow-2xl hover:shadow-green-500/20">
                <div className="w-16 h-16 mb-6 rounded-full bg-gradient-to-br from-green-500 to-green-600 flex items-center justify-center group-hover:scale-110 transition-transform duration-300">
                  <svg className="w-8 h-8 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 12h.01M12 12h.01M16 12h.01M21 12c0 4.418-4.03 8-9 8a9.863 9.863 0 01-4.255-.949L3 20l1.395-3.72C3.512 15.042 3 13.574 3 12c0-4.418 4.03-8 9-8s9 3.582 9 8z" />
                  </svg>
                </div>
                <h3 className="text-2xl font-bold mb-4 text-white">Instant Messaging</h3>
                <p className="text-gray-400 leading-relaxed">
                  Stay connected with real-time chat, file sharing, and contact management in one place.
                </p>
              </div>

              {/* Feature 5 */}
              <div className="group p-8 rounded-2xl bg-gradient-to-br from-gray-900/50 to-gray-800/30 backdrop-blur-sm border border-gray-700/50 hover:border-yellow-500/50 transition-all duration-300 hover:scale-105 hover:shadow-2xl hover:shadow-yellow-500/20">
                <div className="w-16 h-16 mb-6 rounded-full bg-gradient-to-br from-yellow-500 to-yellow-600 flex items-center justify-center group-hover:scale-110 transition-transform duration-300">
                  <svg className="w-8 h-8 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 10V3L4 14h7v7l9-11h-7z" />
                  </svg>
                </div>
                <h3 className="text-2xl font-bold mb-4 text-white">Lightning Fast</h3>
                <p className="text-gray-400 leading-relaxed">
                  Optimized performance with low latency ensures smooth communication without delays.
                </p>
              </div>

              {/* Feature 6 */}
              <div className="group p-8 rounded-2xl bg-gradient-to-br from-gray-900/50 to-gray-800/30 backdrop-blur-sm border border-gray-700/50 hover:border-cyan-500/50 transition-all duration-300 hover:scale-105 hover:shadow-2xl hover:shadow-cyan-500/20">
                <div className="w-16 h-16 mb-6 rounded-full bg-gradient-to-br from-cyan-500 to-cyan-600 flex items-center justify-center group-hover:scale-110 transition-transform duration-300">
                  <svg className="w-8 h-8 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17h14a2 2 0 002-2V5a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
                  </svg>
                </div>
                <h3 className="text-2xl font-bold mb-4 text-white">Cross-Platform</h3>
                <p className="text-gray-400 leading-relaxed">
                  Access Ecosphere from any device - desktop, tablet, or mobile with a consistent experience.
                </p>
              </div>
            </div>

            {/* CTA Section */}
            <div className="mt-20 text-center">
              {isAuthenticated ? (
                <>
                  <h3 className="text-4xl font-bold mb-6 text-white">Welcome Back!</h3>
                  <p className="text-xl text-gray-400 mb-8 max-w-2xl mx-auto">
                    Ready to connect with your team?
                  </p>
                  <button
                    onClick={() => router.push("/dashboard")}
                    className="group relative px-10 py-5 bg-gray-800 text-white text-xl font-bold rounded-full overflow-hidden transition-all duration-300 hover:scale-110 hover:shadow-2xl hover:shadow-purple-500/50"
                  >
                    <span className="relative z-10">Go to Dashboard</span>
                    <div className="absolute inset-0 bg-gradient-to-r from-blue-600 via-purple-600 to-pink-600 opacity-0 group-hover:opacity-100 transition-opacity duration-500"></div>
                  </button>
                </>
              ) : (
                <>
                  <h3 className="text-4xl font-bold mb-6 text-white">Ready to Get Started?</h3>
                  <p className="text-xl text-gray-400 mb-8 max-w-2xl mx-auto">
                    Join thousands of users already connecting through Ecosphere
                  </p>
                  <button
                    onClick={() => router.push("/auth?mode=register")}
                    className="group relative px-10 py-5 bg-gray-800 text-white text-xl font-bold rounded-full overflow-hidden transition-all duration-300 hover:scale-110 hover:shadow-2xl hover:shadow-purple-500/50"
                  >
                    <span className="relative z-10">Create Free Account</span>
                    <div className="absolute inset-0 bg-gradient-to-r from-blue-600 via-purple-600 to-pink-600 opacity-0 group-hover:opacity-100 transition-opacity duration-500"></div>
                  </button>
                </>
              )}
            </div>
          </div>
        </div>

        {/* Footer */}
        <footer className="relative z-10 py-8 border-t border-gray-800/50 backdrop-blur-sm">
          <div className="max-w-7xl mx-auto px-4 text-center text-gray-400">
            <p>&copy; 2026 Ecosphere. All rights reserved.</p>
          </div>
        </footer>
      </div>
    </div>
  );
}
