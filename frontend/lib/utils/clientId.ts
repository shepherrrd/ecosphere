

const CLIENT_ID_KEY = '__ecosphere_client_id';
const CLIENT_ID_BACKUP_KEY = '__ecosphere_client_id_backup';
const CLIENT_ID_HASH_KEY = '__ecosphere_client_id_hash';


function hashClientId(clientId: string): string {
  let hash = 0;
  for (let i = 0; i < clientId.length; i++) {
    const char = clientId.charCodeAt(i);
    hash = ((hash << 5) - hash) + char;
    hash = hash & hash; // Convert to 32bit integer
  }
  return hash.toString(36);
}

/**
 * Generates a new cryptographically secure client ID
 */
function generateClientId(): string {
  // Use crypto.randomUUID() if available (modern browsers)
  if (typeof crypto !== 'undefined' && crypto.randomUUID) {
    return crypto.randomUUID();
  }

  // Fallback for older browsers
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
    const r = Math.random() * 16 | 0;
    const v = c === 'x' ? r : (r & 0x3 | 0x8);
    return v.toString(16);
  });
}

/**
 * Validates that the stored client ID hasn't been tampered with
 */
function validateClientId(clientId: string, storedHash: string | null): boolean {
  if (!storedHash) return false;
  return hashClientId(clientId) === storedHash;
}

/**
 * Stores the client ID securely with validation hash
 */
function storeClientId(clientId: string): void {
  const hash = hashClientId(clientId);

  try {
    // Store in localStorage (primary storage)
    localStorage.setItem(CLIENT_ID_KEY, clientId);
    localStorage.setItem(CLIENT_ID_HASH_KEY, hash);

    // Store backup in sessionStorage (survives page reloads but not browser close)
    sessionStorage.setItem(CLIENT_ID_BACKUP_KEY, clientId);
  } catch (error) {
    console.error('[ClientID] Failed to store client ID:', error);
  }
}

/**
 * Retrieves the client ID from storage, validating against tampering
 */
function retrieveClientId(): string | null {
  try {
    const clientId = localStorage.getItem(CLIENT_ID_KEY);
    const storedHash = localStorage.getItem(CLIENT_ID_HASH_KEY);
    const backup = sessionStorage.getItem(CLIENT_ID_BACKUP_KEY);

    // Validate primary storage
    if (clientId && validateClientId(clientId, storedHash)) {
      // Also verify against backup if available
      if (backup && backup !== clientId) {
        console.warn('[ClientID] Mismatch detected between primary and backup storage');
        return null; // Tampering detected
      }
      return clientId;
    }

    // Check backup storage
    if (backup) {
      console.warn('[ClientID] Primary storage invalid, attempting backup recovery');
      storeClientId(backup); // Restore from backup
      return backup;
    }

    return null;
  } catch (error) {
    console.error('[ClientID] Failed to retrieve client ID:', error);
    return null;
  }
}


export function getOrCreateClientId(): string {
  let clientId = retrieveClientId();

  if (!clientId) {
    console.info('[ClientID] Generating new client ID');
    clientId = generateClientId();
    storeClientId(clientId);
  }

  return clientId;
}


export function regenerateClientId(): string {
  console.warn('[ClientID] Force regenerating client ID');
  const newClientId = generateClientId();
  storeClientId(newClientId);
  return newClientId;
}

export function clearClientId(): void {
  try {
    localStorage.removeItem(CLIENT_ID_KEY);
    localStorage.removeItem(CLIENT_ID_HASH_KEY);
    sessionStorage.removeItem(CLIENT_ID_BACKUP_KEY);
    console.info('[ClientID] Client ID cleared');
  } catch (error) {
    console.error('[ClientID] Failed to clear client ID:', error);
  }
}
