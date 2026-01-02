import type { Metadata } from "next";
import "./globals.css";
import { AuthProvider } from "@/lib/contexts/AuthContext";
import { CallProvider } from "@/lib/contexts/CallContext";

export const metadata: Metadata = {
  title: "Ecosphere - Peer-to-Peer Video Calling",
  description: "Secure peer-to-peer audio and video calling application",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en" suppressHydrationWarning>
      <body suppressHydrationWarning>
        <AuthProvider>
          <CallProvider>
            {children}
          </CallProvider>
        </AuthProvider>
      </body>
    </html>
  );
}
