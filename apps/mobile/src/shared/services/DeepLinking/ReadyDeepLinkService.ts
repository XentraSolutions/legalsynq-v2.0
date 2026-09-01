import type { ResolvedDeepLink } from './DeepLinkTypes';

export type ReadyDeepLinkSubscription = (intent: ResolvedDeepLink) => void;

const listeners = new Set<ReadyDeepLinkSubscription>();

export const ReadyDeepLinkService = {
  emit(intent: ResolvedDeepLink): void {
    for (const listener of listeners) {
      listener(intent);
    }
  },

  subscribe(listener: ReadyDeepLinkSubscription): () => void {
    listeners.add(listener);
    return () => listeners.delete(listener);
  },
};
