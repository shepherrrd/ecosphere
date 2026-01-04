

import { getOrCreateClientId } from './clientId';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000';

interface ApiRequestOptions extends RequestInit {
  token?: string;
  skipAuth?: boolean;
  skipClientId?: boolean;
}

/**
 * Makes an authenticated API request with automatic header injection
 *
 * @param endpoint - API endpoint (e.g., '/api/message/send')
 * @param options - Fetch options with additional flags
 * @returns Fetch response
 */
export async function apiRequest(
  endpoint: string,
  options: ApiRequestOptions = {}
): Promise<Response> {
  const { token, skipAuth, skipClientId, headers, ...fetchOptions } = options;

  // Build headers
  const requestHeaders: Record<string, string> = {
    'Content-Type': 'application/json',
    ...(headers as Record<string, string>),
  };

  // Add Authorization header if not skipped
  if (!skipAuth && token) {
    requestHeaders['Authorization'] = `Bearer ${token}`;
  }

  // Add Client ID header if not skipped
  if (!skipClientId) {
    const clientId = getOrCreateClientId();
    requestHeaders['X-ClientId'] = clientId;
  }

  // Make request
  const url = `${API_BASE_URL}${endpoint}`;

  return fetch(url, {
    ...fetchOptions,
    headers: requestHeaders,
  });
}

/**
 * Convenience method for GET requests
 */
export async function apiGet<T = any>(
  endpoint: string,
  token?: string,
  options: ApiRequestOptions = {}
): Promise<T> {
  const response = await apiRequest(endpoint, {
    method: 'GET',
    token,
    ...options,
  });

  if (!response.ok) {
    throw new Error(`API GET ${endpoint} failed: ${response.statusText}`);
  }

  return response.json();
}


export async function apiPost<T = any>(
  endpoint: string,
  data: any,
  token?: string,
  options: ApiRequestOptions = {}
): Promise<T> {
  const response = await apiRequest(endpoint, {
    method: 'POST',
    body: JSON.stringify(data),
    token,
    ...options,
  });

  if (!response.ok) {
    throw new Error(`API POST ${endpoint} failed: ${response.statusText}`);
  }

  return response.json();
}


export async function apiPut<T = any>(
  endpoint: string,
  data: any,
  token?: string,
  options: ApiRequestOptions = {}
): Promise<T> {
  const response = await apiRequest(endpoint, {
    method: 'PUT',
    body: JSON.stringify(data),
    token,
    ...options,
  });

  if (!response.ok) {
    throw new Error(`API PUT ${endpoint} failed: ${response.statusText}`);
  }

  return response.json();
}


export async function apiDelete<T = any>(
  endpoint: string,
  token?: string,
  options: ApiRequestOptions = {}
): Promise<T> {
  const response = await apiRequest(endpoint, {
    method: 'DELETE',
    token,
    ...options,
  });

  if (!response.ok) {
    throw new Error(`API DELETE ${endpoint} failed: ${response.statusText}`);
  }

  return response.json();
}


