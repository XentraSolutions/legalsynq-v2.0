import type { AccessTokenStore }       from '@/domain/interfaces/access-token-store';
import { InMemoryAccessTokenStore }    from './in-memory-access-token-store';
import { RedisAccessTokenStore }       from './redis-access-token-store';
import { config }                      from '@/shared/config';
import { logger }                      from '@/shared/logger';

let _instance: AccessTokenStore | null = null;

export function getAccessTokenStore(): AccessTokenStore {
  if (_instance) return _instance;

  switch (config.ACCESS_TOKEN_STORE) {
    case 'redis':
      _instance = new RedisAccessTokenStore();
      break;
    case 'memory':
    default:
      _instance = new InMemoryAccessTokenStore();
      break;
  }

  logger.info({ store: config.ACCESS_TOKEN_STORE }, 'Access token store initialised');
  return _instance;
}

/** For testing only — reset the singleton so tests get a fresh store. */
export function resetAccessTokenStore(): void {
  if (_instance) {
    _instance.destroy();
    _instance = null;
  }
}
