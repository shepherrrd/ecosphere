class SoundManager {
  private sounds: Map<string, HTMLAudioElement>;
  private currentLoopingSound: HTMLAudioElement | null = null;
  private initialized: boolean = false;
  private audioEnabled: boolean = false;

  constructor() {
    this.sounds = new Map();
    // Don't initialize during SSR
    if (typeof window !== 'undefined') {
      this.initializeSounds();
      this.setupAudioUnlock();
    }
  }

  /**
   * Setup audio unlock - browsers require user interaction before playing audio
   */
  private setupAudioUnlock() {
    if (typeof window === 'undefined') return;

    const unlockAudio = () => {
      this.audioEnabled = true;
      // Unlock audio context by attempting silent play/pause
      this.sounds.forEach((sound) => {
        // Mute the sound temporarily
        const originalVolume = sound.volume;
        sound.volume = 0;

        const playPromise = sound.play();
        if (playPromise !== undefined) {
          playPromise.then(() => {
            sound.pause();
            sound.currentTime = 0;
            sound.volume = originalVolume; // Restore volume
          }).catch(() => {
            sound.volume = originalVolume; // Restore volume even on error
          });
        }
      });

      // Remove listeners after first interaction
      document.removeEventListener('click', unlockAudio);
      document.removeEventListener('touchstart', unlockAudio);
      document.removeEventListener('keydown', unlockAudio);
    };

    // Listen for first user interaction
    document.addEventListener('click', unlockAudio, { once: true });
    document.addEventListener('touchstart', unlockAudio, { once: true });
    document.addEventListener('keydown', unlockAudio, { once: true });
  }

  private initializeSounds() {
    // Guard against SSR and double initialization
    if (this.initialized || typeof window === 'undefined') {
      return;
    }

    const soundFiles = {
      dialing: "/sounds/dialing.mp3",
      callRinging: "/sounds/callringing.mp3",
      meetingRinging: "/sounds/meetingringing.mp3",
      incomingMessage: "/sounds/incomingmessage.mp3",
      messageSent: "/sounds/messagesent.mp3",
    };

    Object.entries(soundFiles).forEach(([key, path]) => {
      const audio = new Audio(path);
      audio.preload = "auto";
      this.sounds.set(key, audio);
    });

    this.initialized = true;
  }

  /**
   * Ensure sounds are initialized (call this in useEffect hooks)
   */
  private ensureInitialized() {
    if (!this.initialized && typeof window !== 'undefined') {
      this.initializeSounds();
    }
  }

  /**
   * Play a sound once
   */
  play(soundName: string) {
    this.ensureInitialized();

    const sound = this.sounds.get(soundName);
    if (sound) {
      sound.currentTime = 0;
      sound.play().catch((error) => {
        // Only log if it's not the autoplay policy error
        if (error.name !== 'NotAllowedError') {
          console.error(`[SoundManager] Error playing ${soundName}:`, error);
        }
        // Silently ignore autoplay policy errors - audio will work after user interaction
      });
    } else {
      console.warn(`[SoundManager] Sound not found: ${soundName}`);
    }
  }

  /**
   * Play a sound in a loop
   */
  playLoop(soundName: string) {
    this.ensureInitialized();
    this.stopLoop(); // Stop any currently looping sound

    const sound = this.sounds.get(soundName);
    if (sound) {
      sound.loop = true;
      sound.currentTime = 0;
      sound.play().catch((error) => {
        // Only log if it's not the autoplay policy error
        if (error.name !== 'NotAllowedError') {
          console.error(`[SoundManager] Error playing loop ${soundName}:`, error);
        }
        // Silently ignore autoplay policy errors - audio will work after user interaction
      });
      this.currentLoopingSound = sound;
    } else {
      console.warn(`[SoundManager] Sound not found: ${soundName}`);
    }
  }

  /**
   * Stop the currently looping sound
   */
  stopLoop() {
    if (this.currentLoopingSound) {
      this.currentLoopingSound.pause();
      this.currentLoopingSound.currentTime = 0;
      this.currentLoopingSound.loop = false;
      this.currentLoopingSound = null;
    }
  }

  /**
   * Stop a specific sound
   */
  stop(soundName: string) {
    const sound = this.sounds.get(soundName);
    if (sound) {
      sound.pause();
      sound.currentTime = 0;
      sound.loop = false;

      if (this.currentLoopingSound === sound) {
        this.currentLoopingSound = null;
      }
    }
  }

  /**
   * Stop all sounds
   */
  stopAll() {
    this.sounds.forEach((sound) => {
      sound.pause();
      sound.currentTime = 0;
      sound.loop = false;
    });
    this.currentLoopingSound = null;
  }
}

// Export a singleton instance
export const soundManager = new SoundManager();
