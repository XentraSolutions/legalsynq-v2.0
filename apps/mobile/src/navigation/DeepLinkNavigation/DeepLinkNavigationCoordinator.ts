import type { ResolvedDeepLink } from '@/shared/services/DeepLinking';

import {
  mapDeepLinkToNavigation,
  type DeepLinkNavigationMappingResult,
  type DeepLinkNavigationTarget,
} from './DeepLinkNavigationMapper';

export interface ReadyDeepLinkSource {
  subscribe(listener: (intent: ResolvedDeepLink) => void): () => void;
}

export interface DeepLinkNavigationAdapter {
  isReady(): boolean;
  navigate(target: DeepLinkNavigationTarget): void;
}

export type DeepLinkNavigationResult =
  | { status: 'navigated'; routeKey: string }
  | { status: 'queued_until_ready'; routeKey: string }
  | Exclude<DeepLinkNavigationMappingResult, { status: 'mapped' }>
  | { status: 'navigation_failed'; routeKey: string; reason: string };

export interface DeepLinkNavigationCoordinatorDependencies {
  navigation: DeepLinkNavigationAdapter;
  readyIntentSource: ReadyDeepLinkSource;
  onResult?: (result: DeepLinkNavigationResult) => void;
}

interface PendingNavigation {
  intent: ResolvedDeepLink;
  target: DeepLinkNavigationTarget;
}

export class DeepLinkNavigationCoordinator {
  private pendingNavigation: PendingNavigation | null = null;
  private unsubscribe: (() => void) | null = null;

  constructor(private readonly dependencies: DeepLinkNavigationCoordinatorDependencies) {}

  start(): void {
    if (this.unsubscribe) {
      return;
    }

    this.unsubscribe = this.dependencies.readyIntentSource.subscribe((intent) => {
      this.report(this.processIntent(intent));
    });
  }

  stop(): void {
    if (!this.unsubscribe) {
      return;
    }

    const unsubscribe = this.unsubscribe;
    this.unsubscribe = null;
    unsubscribe();
  }

  processIntent(intent: ResolvedDeepLink): DeepLinkNavigationResult {
    const mapping = mapDeepLinkToNavigation(intent);
    if (mapping.status !== 'mapped') {
      return mapping;
    }

    if (!this.dependencies.navigation.isReady()) {
      this.pendingNavigation = { intent, target: mapping.target };
      return { status: 'queued_until_ready', routeKey: intent.routeKey };
    }

    this.pendingNavigation = null;
    return this.dispatch(intent, mapping.target);
  }

  onNavigationReady(): DeepLinkNavigationResult | null {
    if (!this.pendingNavigation || !this.dependencies.navigation.isReady()) {
      return null;
    }

    const pendingNavigation = this.pendingNavigation;
    this.pendingNavigation = null;
    const result = this.dispatch(pendingNavigation.intent, pendingNavigation.target);
    this.report(result);
    return result;
  }

  getPendingIntent(): ResolvedDeepLink | null {
    return this.pendingNavigation?.intent ?? null;
  }

  private dispatch(
    intent: ResolvedDeepLink,
    target: DeepLinkNavigationTarget
  ): DeepLinkNavigationResult {
    try {
      this.dependencies.navigation.navigate(target);
      return { status: 'navigated', routeKey: intent.routeKey };
    } catch (error) {
      return {
        status: 'navigation_failed',
        routeKey: intent.routeKey,
        reason: error instanceof Error ? error.message : 'Navigation dispatch failed.',
      };
    }
  }

  private report(result: DeepLinkNavigationResult): void {
    this.dependencies.onResult?.(result);
  }
}
