import { ConfigService } from '@/shared/services/Config';

import { DeepLinkDuplicateGuard } from './DeepLinkDuplicateGuard';
import { DeepLinkResolver } from './DeepLinkResolver';
import { DeepLinkingService } from './DeepLinkingService';
import type { DeepLinkResolution, DeepLinkResolutionListener } from './DeepLinkTypes';

export interface DeepLinkPlatformAdapter {
  getInitialUrl(): Promise<string | null>;
  subscribeToUrls(listener: (url: string) => void): () => void;
}

export interface DeepLinkIntakeDependencies {
  resolver: DeepLinkResolver;
  duplicateGuard?: DeepLinkDuplicateGuard;
  platformAdapter?: DeepLinkPlatformAdapter;
}

export class DeepLinkIntakeService {
  private readonly resolver: DeepLinkResolver;
  private readonly duplicateGuard: DeepLinkDuplicateGuard;
  private readonly platformAdapter: DeepLinkPlatformAdapter;

  constructor(dependencies: DeepLinkIntakeDependencies) {
    this.resolver = dependencies.resolver;
    this.duplicateGuard = dependencies.duplicateGuard ?? new DeepLinkDuplicateGuard();
    this.platformAdapter = dependencies.platformAdapter ?? DeepLinkingService;
  }

  static createConfigured(): DeepLinkIntakeService {
    return new DeepLinkIntakeService({
      resolver: new DeepLinkResolver({
        expectedHttpsHost: ConfigService.getDeepLinkHost(),
      }),
    });
  }

  processUrl(url: string): DeepLinkResolution {
    const resolution = this.resolver.resolve(url);
    if (resolution.status !== 'resolved') {
      return resolution;
    }

    if (!this.duplicateGuard.isDuplicate(resolution.normalizedUrl)) {
      return resolution;
    }

    return {
      status: 'duplicate',
      reason: 'This normalized URL was already processed inside the duplicate window.',
      originalUrl: resolution.originalUrl,
      normalizedUrl: resolution.normalizedUrl,
    };
  }

  async processInitialUrl(
    listener: DeepLinkResolutionListener
  ): Promise<DeepLinkResolution | null> {
    const initialUrl = await this.platformAdapter.getInitialUrl();
    if (!initialUrl) {
      return null;
    }

    const resolution = this.processUrl(initialUrl);
    if (resolution.status !== 'duplicate') {
      listener(resolution);
    }

    return resolution;
  }

  subscribe(listener: DeepLinkResolutionListener): () => void {
    return this.platformAdapter.subscribeToUrls((url) => {
      const resolution = this.processUrl(url);
      if (resolution.status !== 'duplicate') {
        listener(resolution);
      }
    });
  }
}
