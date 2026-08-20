import { DeepLinkAuthCoordinator } from './DeepLinkAuthCoordinator';
import type { DeepLinkIntakeService } from './DeepLinkIntakeService';

export interface DeepLinkAuthLifecycleDependencies {
  intake: Pick<DeepLinkIntakeService, 'processInitialUrl' | 'subscribe'>;
  coordinator: DeepLinkAuthCoordinator;
}

export class DeepLinkAuthLifecycle {
  private cleanup: (() => void) | null = null;
  private generation = 0;

  constructor(private readonly dependencies: DeepLinkAuthLifecycleDependencies) {}

  start(): void {
    if (this.cleanup) {
      return;
    }

    const generation = ++this.generation;
    const deliverIfActive = (
      resolution: Parameters<DeepLinkAuthCoordinator['processResolution']>[0]
    ) => {
      if (this.generation === generation && this.cleanup) {
        this.dependencies.coordinator.processResolution(resolution);
      }
    };

    this.cleanup = this.dependencies.intake.subscribe(deliverIfActive);
    void this.dependencies.intake.processInitialUrl(deliverIfActive).catch(() => undefined);
  }

  stop(): void {
    if (!this.cleanup) {
      return;
    }

    const cleanup = this.cleanup;
    this.cleanup = null;
    this.generation += 1;
    cleanup();
  }
}
