import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  /* config options here */
  reactStrictMode: false, // Disable to prevent double-mounting during development
  allowedDevOrigins: [
    "http://localhost:3000",
    "http://192.168.1.197",
    "http://192.168.1.106",
    "https://2ndpw4s3-3001.uks1.devtunnels.ms/"
  ]
};

export default nextConfig;
