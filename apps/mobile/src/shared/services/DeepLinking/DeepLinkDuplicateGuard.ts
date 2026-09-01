export interface DeepLinkDuplicateGuardOptions {
  windowMs?: number;
  now?: () => number;
}

const DEFAULT_DUPLICATE_WINDOW_MS = 2_000;

export class DeepLinkDuplicateGuard {
  private readonly processedAt = new Map<string, number>();
  private readonly windowMs: number;
  private readonly now: () => number;

  constructor(options: DeepLinkDuplicateGuardOptions = {}) {
    this.windowMs = options.windowMs ?? DEFAULT_DUPLICATE_WINDOW_MS;
    this.now = options.now ?? Date.now;

    if (!Number.isFinite(this.windowMs) || this.windowMs < 0) {
      throw new Error('Deep-link duplicate window must be a non-negative number.');
    }
  }

  isDuplicate(normalizedUrl: string): boolean {
    const currentTime = this.now();
    for (const [url, processedTime] of this.processedAt) {
      if (currentTime - processedTime > this.windowMs) {
        this.processedAt.delete(url);
      }
    }

    const previousTime = this.processedAt.get(normalizedUrl);

    if (previousTime !== undefined && currentTime - previousTime <= this.windowMs) {
      return true;
    }

    this.processedAt.set(normalizedUrl, currentTime);
    return false;
  }

  clear(): void {
    this.processedAt.clear();
  }
}
