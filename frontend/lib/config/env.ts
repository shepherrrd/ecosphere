/**
 * Environment configuration
 * All environment variables should be accessed through this file
 */

export const env = {
  // API URLs
  apiUrl: process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api',
  hubUrl: process.env.NEXT_PUBLIC_HUB_URL || 'http://localhost:5000/callHub',
  meetingHubUrl: process.env.NEXT_PUBLIC_MEETING_HUB_URL || 'http://localhost:5000/meetingHub',
  sfuHubUrl: process.env.NEXT_PUBLIC_SFU_HUB_URL || 'http://localhost:5000/sfuHub',

  // Feature flags (can be added later)
  features: {
    sfuEnabled: true,
    p2pEnabled: true,
  }
} as const;

// Type-safe environment checker
export function getRequiredEnv(key: keyof typeof env): string {
  const value = env[key];
  if (!value) {
    throw new Error(`Required environment variable ${key} is not set`);
  }
  return value as string;
}

// Validate all required environment variables on app startup
export function validateEnv(): void {
  const required: (keyof typeof env)[] = ['apiUrl', 'sfuHubUrl'];

  const missing = required.filter(key => !env[key]);

  if (missing.length > 0) {
    console.error('Missing required environment variables:', missing);
    throw new Error(`Missing environment variables: ${missing.join(', ')}`);
  }

  console.log('[Config] Environment validated successfully');
  console.log('[Config] API URL:', env.apiUrl);
  console.log('[Config] SFU Hub URL:', env.sfuHubUrl);
}
